using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    [InitializeOnLoad]
    public sealed class NatoriCityBuilderSceneTool
    {
        private static readonly NatoriCityBuilderSceneTool Instance = new();
        private static bool _isRegistered;
        private readonly NatoriCitySelectionSceneTool _selectionTool = new();
        private static bool _previousToolsHidden;
        private bool _isTrackingRightClick;
        private bool _rightClickBecameDrag;
        private Vector2Int _rightMouseDownCell;
        private bool _isPainting;
        private Vector2Int _lastPaintedCell;
        private int _placementStrokeUndoGroup = -1;
        private GUIStyle _placementCountStyle;
        internal static bool IsRegistered => _isRegistered;

        static NatoriCityBuilderSceneTool()
        {
            AssemblyReloadEvents.beforeAssemblyReload += End;
            EditorApplication.quitting += End;
        }

        public static void Begin()
        {
            End();
            if (!NatoriCityBuilderEditingSession.IsEditing)
            {
                return;
            }
            SceneView.duringSceneGui += Instance.OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedo;
            _previousToolsHidden = Tools.hidden;
            Tools.hidden = true;
            _isRegistered = true;
            SceneView.RepaintAll();
        }

        public static void End()
        {
            SceneView.duringSceneGui -= Instance.OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (_isRegistered)
            {
                Tools.hidden = _previousToolsHidden;
            }
            _isRegistered = false;
            Instance.FinishPaintingStroke();
            Instance._selectionTool.Reset();
            Instance._isTrackingRightClick = false;
            SceneView.RepaintAll();
        }

        private static void OnUndoRedo()
        {
            Instance._selectionTool.OnUndoRedo(NatoriCityBuilderEditingSession.GetEditingFloor());
        }

        internal static bool DrawEditingTools(NatoriCityBuildingEditorState state, BuildingFloor floor)
        {
            Instance._selectionTool.DrawToolbar(state, floor);
            if (!Instance._selectionTool.IsPen)
            {
                Instance.FinishPaintingStroke();
            }
            return Instance._selectionTool.IsPen;
        }

        internal static void DrawSceneToolbar()
        {
            BuildingFloor floor = NatoriCityBuilderEditingSession.GetEditingFloor();
            if (floor == null)
            {
                return;
            }
            Instance._selectionTool.DrawSceneToolbar(floor);
            if (!Instance._selectionTool.IsPen)
            {
                Instance.FinishPaintingStroke();
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            NatoriCityBuildingComponent building = NatoriCityBuilderEditingSession.Building;
            BuildingFloor floor = NatoriCityBuilderEditingSession.GetEditingFloor();
            if (building == null || floor == null)
            {
                NatoriCityBuilderEditingSession.End();
                return;
            }
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(building);
            if (!ValidateEditingState(state))
            {
                return;
            }

            float floorBaseHeight = state.ModelIndex.GetFloorBaseHeight(floor);
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.Repaint)
            {
                DrawGrid(building, floorBaseHeight);
                DrawPlacementCounts(state, floor, floorBaseHeight);
            }
            bool hasGridPoint = TryGetHoveredCell(building, floorBaseHeight, out Vector2Int hoveredCell);
            bool hasHoveredCell = hasGridPoint && hoveredCell.x >= 0 && hoveredCell.y >= 0
                && hoveredCell.x < building.GridSize.x && hoveredCell.y < building.GridSize.y;
            if (Instance._selectionTool.OnSceneGUI(sceneView, state, floor, floorBaseHeight, hasGridPoint, hoveredCell))
            {
                FinishPaintingStroke();
                _isTrackingRightClick = false;
                return;
            }
            if (currentEvent.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }
            if (currentEvent.alt || !hasHoveredCell)
            {
                TrackLeftMouseOutsideGrid(currentEvent);
                TrackRightMouseOutsideGrid(currentEvent);
                return;
            }

            BuildingPartDefinition selectedPart = NatoriCityBuilderEditingSession.SelectedPart;
            if (selectedPart == null)
            {
                DrawUnselectedHoveredCell(building, floorBaseHeight, hoveredCell);
            }
            else
            {
                bool canPlace = state.CanPlace(
                    floor,
                    selectedPart,
                    hoveredCell);
                DrawHoveredFootprint(building, floorBaseHeight, hoveredCell, selectedPart.Footprint, canPlace);
                if (NatoriCityBuilderEditorPreferences.StrokeModeEnabled)
                {
                    TrackLeftMousePainting(
                        currentEvent,
                        building,
                        floor,
                        selectedPart,
                        hoveredCell,
                        canPlace);
                }
                else
                {
                    FinishPaintingStroke();
                    TrackSingleClickPlacement(
                        currentEvent,
                        building,
                        floor,
                        selectedPart,
                        hoveredCell,
                        canPlace);
                }
            }

            TrackRightMouseOnGrid(currentEvent, building, floor, hoveredCell);
        }

        private bool ValidateEditingState(NatoriCityBuildingEditorState state)
        {
            NatoriCityBuildingValidationResult validation = state.Validation;
            if (validation.IsValid)
            {
                return true;
            }

            FinishPaintingStroke();
            Debug.LogError(
                $"Natori City Builder: Editを終了しました: {validation.Errors[0]}",
                state.Building);
            NatoriCityBuilderEditingSession.End();
            return false;
        }

        private static void TrackSingleClickPlacement(
            Event currentEvent,
            NatoriCityBuildingComponent building,
            BuildingFloor floor,
            BuildingPartDefinition selectedPart,
            Vector2Int hoveredCell,
            bool canPlace)
        {
            if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
            {
                return;
            }
            if (canPlace)
            {
                NatoriCityBuildingEditorActions.AddPlacement(
                    building,
                    floor,
                    selectedPart,
                    hoveredCell);
            }
            currentEvent.Use();
        }

        private void TrackLeftMousePainting(
            Event currentEvent,
            NatoriCityBuildingComponent building,
            BuildingFloor floor,
            BuildingPartDefinition selectedPart,
            Vector2Int hoveredCell,
            bool canPlace)
        {
            if (currentEvent.button != 0)
            {
                return;
            }
            if (currentEvent.type == EventType.MouseDown)
            {
                FinishPaintingStroke();
                _placementStrokeUndoGroup =
                    NatoriCityBuildingEditorActions.BeginPlacementStroke(building);
                _isPainting = _placementStrokeUndoGroup >= 0;
                _lastPaintedCell = hoveredCell;
                if (_isPainting && canPlace && !NatoriCityBuildingEditorActions.AddPlacementToStroke(
                    building,
                    floor,
                    selectedPart,
                    hoveredCell,
                    _placementStrokeUndoGroup))
                {
                    FinishPaintingStroke();
                }
                currentEvent.Use();
                return;
            }
            if (!_isPainting)
            {
                return;
            }
            if (currentEvent.type == EventType.MouseDrag)
            {
                if (hoveredCell != _lastPaintedCell)
                {
                    _lastPaintedCell = hoveredCell;
                    if (canPlace && !NatoriCityBuildingEditorActions.AddPlacementToStroke(
                        building,
                        floor,
                        selectedPart,
                        hoveredCell,
                        _placementStrokeUndoGroup))
                    {
                        FinishPaintingStroke();
                    }
                }
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp)
            {
                FinishPaintingStroke();
                currentEvent.Use();
            }
        }

        private void TrackLeftMouseOutsideGrid(Event currentEvent)
        {
            if (_isPainting && currentEvent.button == 0 && currentEvent.type == EventType.MouseUp)
            {
                FinishPaintingStroke();
            }
        }

        private void FinishPaintingStroke()
        {
            if (_placementStrokeUndoGroup >= 0)
            {
                NatoriCityBuildingEditorActions.EndPlacementStroke(_placementStrokeUndoGroup);
            }
            _placementStrokeUndoGroup = -1;
            _isPainting = false;
        }

        private void DrawPlacementCounts(
            NatoriCityBuildingEditorState state,
            BuildingFloor floor,
            float floorBaseHeight)
        {
            _placementCountStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    textColor = Color.white,
                },
            };
            NatoriCityBuildingComponent building = state.Building;
            float horizontalCellSize = building.Settings.HorizontalCellSize;
            Vector2 gridMinimum = NatoriCityGridGeometry.GetGridMinimum(
                building.GridSize,
                horizontalCellSize);
            Matrix4x4 previousMatrix = Handles.matrix;
            Handles.matrix = building.transform.localToWorldMatrix;
            NatoriCityFloorOccupancyIndex occupancy = state.ModelIndex.GetOccupancy(floor);
            foreach (Vector2Int cell in occupancy.OverlappingCells)
            {
                int placementCount = occupancy.GetPlacementCount(cell);
                var cellCenter = new Vector3(
                    gridMinimum.x + (cell.x + 0.5f) * horizontalCellSize,
                    floorBaseHeight + 0.01f,
                    gridMinimum.y + (cell.y + 0.5f) * horizontalCellSize);
                Handles.Label(cellCenter, placementCount.ToString(), _placementCountStyle);
            }
            Handles.matrix = previousMatrix;
        }

        private static void DrawUnselectedHoveredCell(
            NatoriCityBuildingComponent building,
            float floorBaseHeight,
            Vector2Int hoveredCell)
        {
            float horizontalCellSize = building.Settings.HorizontalCellSize;
            Vector2 gridMinimum = NatoriCityGridGeometry.GetGridMinimum(
                building.GridSize,
                horizontalCellSize);
            float minimumX = gridMinimum.x + hoveredCell.x * horizontalCellSize;
            float minimumZ = gridMinimum.y + hoveredCell.y * horizontalCellSize;
            var vertices = new[]
            {
                new Vector3(minimumX, floorBaseHeight + 0.002f, minimumZ),
                new Vector3(minimumX, floorBaseHeight + 0.002f, minimumZ + horizontalCellSize),
                new Vector3(
                    minimumX + horizontalCellSize,
                    floorBaseHeight + 0.002f,
                    minimumZ + horizontalCellSize),
                new Vector3(minimumX + horizontalCellSize, floorBaseHeight + 0.002f, minimumZ),
            };
            Matrix4x4 previousMatrix = Handles.matrix;
            Handles.matrix = building.transform.localToWorldMatrix;
            Handles.DrawSolidRectangleWithOutline(
                vertices,
                new Color(1.0f, 1.0f, 1.0f, 0.10f),
                new Color(1.0f, 1.0f, 1.0f, 0.38f));
            Handles.matrix = previousMatrix;
        }

        private void TrackRightMouseOnGrid(
            Event currentEvent,
            NatoriCityBuildingComponent building,
            BuildingFloor floor,
            Vector2Int hoveredCell)
        {
            if (currentEvent.button != 1)
            {
                return;
            }
            if (currentEvent.type == EventType.MouseDown)
            {
                _isTrackingRightClick = true;
                _rightClickBecameDrag = false;
                _rightMouseDownCell = hoveredCell;
                return;
            }
            if (!_isTrackingRightClick)
            {
                return;
            }
            if (currentEvent.type == EventType.MouseDrag)
            {
                _rightClickBecameDrag = true;
                return;
            }
            if (currentEvent.type != EventType.MouseUp)
            {
                return;
            }

            bool showPopup = !_rightClickBecameDrag;
            _isTrackingRightClick = false;
            if (showPopup)
            {
                PopupWindow.Show(
                    new Rect(currentEvent.mousePosition, Vector2.zero),
                    new NatoriCityCellPopupContent(building, floor.Identifier, _rightMouseDownCell));
                currentEvent.Use();
            }
        }

        private void TrackRightMouseOutsideGrid(Event currentEvent)
        {
            if (!_isTrackingRightClick || currentEvent.button != 1)
            {
                return;
            }
            if (currentEvent.type == EventType.MouseDrag)
            {
                _rightClickBecameDrag = true;
            }
            else if (currentEvent.type == EventType.MouseUp)
            {
                _isTrackingRightClick = false;
            }
        }

        private static void DrawGrid(NatoriCityBuildingComponent building, float floorBaseHeight)
        {
            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;
            Handles.matrix = building.transform.localToWorldMatrix;
            Handles.color = new Color(1.0f, 1.0f, 1.0f, 0.28f);
            float horizontalCellSize = building.Settings.HorizontalCellSize;
            Vector2 gridMinimum = NatoriCityGridGeometry.GetGridMinimum(
                building.GridSize,
                horizontalCellSize);
            float maximumX = gridMinimum.x + building.GridSize.x * horizontalCellSize;
            float maximumZ = gridMinimum.y + building.GridSize.y * horizontalCellSize;
            for (int xLineSeek = 0; xLineSeek <= building.GridSize.x; xLineSeek++)
            {
                float x = gridMinimum.x + xLineSeek * horizontalCellSize;
                Handles.DrawLine(
                    new Vector3(x, floorBaseHeight, gridMinimum.y),
                    new Vector3(x, floorBaseHeight, maximumZ));
            }
            for (int zLineSeek = 0; zLineSeek <= building.GridSize.y; zLineSeek++)
            {
                float z = gridMinimum.y + zLineSeek * horizontalCellSize;
                Handles.DrawLine(
                    new Vector3(gridMinimum.x, floorBaseHeight, z),
                    new Vector3(maximumX, floorBaseHeight, z));
            }
            Handles.color = previousColor;
            Handles.matrix = previousMatrix;
        }

        private static bool TryGetHoveredCell(
            NatoriCityBuildingComponent building,
            float floorBaseHeight,
            out Vector2Int hoveredCell)
        {
            Ray worldRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            Matrix4x4 worldToLocal = building.transform.worldToLocalMatrix;
            Vector3 localRayOrigin = worldToLocal.MultiplyPoint3x4(worldRay.origin);
            Vector3 localRayDirection = worldToLocal.MultiplyVector(worldRay.direction);
            if (Mathf.Abs(localRayDirection.y) < 0.000001f)
            {
                hoveredCell = default;
                return false;
            }
            float rayDistance = (floorBaseHeight - localRayOrigin.y) / localRayDirection.y;
            if (rayDistance < 0.0f)
            {
                hoveredCell = default;
                return false;
            }
            Vector3 localPoint = localRayOrigin + localRayDirection * rayDistance;
            float horizontalCellSize = building.Settings.HorizontalCellSize;
            Vector2 gridMinimum = NatoriCityGridGeometry.GetGridMinimum(
                building.GridSize,
                horizontalCellSize);
            hoveredCell = new Vector2Int(
                Mathf.FloorToInt((localPoint.x - gridMinimum.x) / horizontalCellSize),
                Mathf.FloorToInt((localPoint.z - gridMinimum.y) / horizontalCellSize));
            //範囲選択ではグリッド外のマウス位置も必要になる。内外判定は操作側で行う。
            return true;
        }

        private static void DrawHoveredFootprint(
            NatoriCityBuildingComponent building,
            float floorBaseHeight,
            Vector2Int anchorCell,
            Vector2Int footprint,
            bool canPlace)
        {
            float horizontalCellSize = building.Settings.HorizontalCellSize;
            Vector2 gridMinimum = NatoriCityGridGeometry.GetGridMinimum(
                building.GridSize,
                horizontalCellSize);
            float minimumX = gridMinimum.x + anchorCell.x * horizontalCellSize;
            float minimumZ = gridMinimum.y + anchorCell.y * horizontalCellSize;
            float footprintWidth = footprint.x * horizontalCellSize;
            float footprintDepth = footprint.y * horizontalCellSize;
            var vertices = new[]
            {
                new Vector3(minimumX, floorBaseHeight + 0.002f, minimumZ),
                new Vector3(minimumX, floorBaseHeight + 0.002f, minimumZ + footprintDepth),
                new Vector3(minimumX + footprintWidth, floorBaseHeight + 0.002f, minimumZ + footprintDepth),
                new Vector3(minimumX + footprintWidth, floorBaseHeight + 0.002f, minimumZ),
            };
            Matrix4x4 previousMatrix = Handles.matrix;
            Handles.matrix = building.transform.localToWorldMatrix;
            Color fillColor = canPlace
                ? new Color(1.0f, 1.0f, 1.0f, 0.24f)
                : new Color(1.0f, 0.15f, 0.15f, 0.28f);
            Color outlineColor = canPlace ? Color.white : Color.red;
            Handles.DrawSolidRectangleWithOutline(vertices, fillColor, outlineColor);
            Handles.matrix = previousMatrix;
        }
    }
}
