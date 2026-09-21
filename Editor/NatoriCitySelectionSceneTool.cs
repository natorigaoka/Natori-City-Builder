using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    //SceneViewの一時操作だけを所有する。保存対象はBuildingComponent、選択の寿命は階の編集セッション。
    internal sealed class NatoriCitySelectionSceneTool
    {
        private readonly NatoriCityPlacementSelection _selection = new();
        private readonly NatoriCityRotationDrag _rotationDrag = new();
        private NatoriCityBuilderInteractionMode _mode;
        private NatoriCitySelectionToolKind _tool;
        private bool _rectangleDragging;
        private Vector2 _rectangleStart;
        private Vector2 _rectangleEnd;
        private bool _additive;
        private bool _toggle;
        private string _lastPickedIdentifier;
        private List<BuildingPartPlacement> _pasteSource;
        private int _gestureUndoGroup = -1;
        private int _ownedControl;
        private string _message;

        internal static event Action Changed;

        public bool IsPen => _mode == NatoriCityBuilderInteractionMode.Pen && _pasteSource == null;

        private static void RepaintSelection()
        {
            SceneView.RepaintAll();
            Changed?.Invoke();
        }

        public void OnUndoRedo(BuildingFloor floor)
        {
            //Undo復元後の座標から中心を取り直し、ドラッグ中の座標を次の操作へ持ち越さない。
            _gestureUndoGroup = -1;
            _rotationDrag.Reset();
            ReleaseControl();
            _rectangleDragging = false;
            _pasteSource = null;
            _message = null;
            if (floor != null)
            {
                _selection.Select(floor, _selection.Resolve(floor), false, false);
            }
        }

        public void Reset()
        {
            FinishGesture();
            ReleaseControl();
            _selection.Clear();
            _rectangleDragging = false;
            _pasteSource = null;
            _lastPickedIdentifier = null;
            _mode = NatoriCityBuilderInteractionMode.Pen;
            _message = null;
        }

        private void ReleaseControl()
        {
            if (_ownedControl != 0 && GUIUtility.hotControl == _ownedControl)
            {
                GUIUtility.hotControl = 0;
            }
            _ownedControl = 0;
        }

        public void DrawToolbar(NatoriCityBuildingEditorState state, BuildingFloor floor)
        {
            int mode = GUILayout.Toolbar((int)_mode, new[] { "ペン", "単一選択", "範囲選択" });
            SetMode((NatoriCityBuilderInteractionMode)mode);
            if (IsPen)
            {
                return;
            }
            List<BuildingPartPlacement> selected = _selection.Resolve(floor);
            var tool = (NatoriCitySelectionToolKind)GUILayout.Toolbar((int)_tool,
                new[] { "グリッド移動", "配置全体の回転", "各配置の回転" });
            SetTool(tool);
            EditorGUILayout.LabelField($"選択: {selected.Count}件");
            EditorGUILayout.HelpBox(_tool == NatoriCitySelectionToolKind.Move
                ? "赤・青の矢印でX/Z方向、中央の四角でXZ平面を1セル単位で移動します。"
                : _tool == NatoriCitySelectionToolKind.RotateGroup
                    ? "黄色の円周をドラッグすると、位置と向きをまとめて90度刻みで回転します。"
                    : "紫の円周をドラッグすると、各アンカーセルを固定して向きを90度刻みで変更します。",
                MessageType.None);
            EditorGUILayout.HelpBox(
                "Shift: 追加 / Ctrl: 選択切替 / 空白クリック: 解除\n"
                + "範囲選択: 画面の矩形と占有面の投影が交差する配置。単一選択: 重なりをクリック順に選択。\n"
                + "Ctrl+C/V: コピー・貼り付け / Delete: 削除 / Esc: キャンセル",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(selected.Count == 0 || _pasteSource != null))
            {
                EditorGUILayout.BeginHorizontal();
                if (_tool != NatoriCitySelectionToolKind.Move && GUILayout.Button("−90°"))
                {
                    Rotate(state, floor, selected, -1);
                }
                if (_tool != NatoriCitySelectionToolKind.Move && GUILayout.Button("＋90°"))
                {
                    Rotate(state, floor, selected, 1);
                }
                if (GUILayout.Button("削除"))
                {
                    Delete(state, floor, selected);
                }
                EditorGUILayout.EndHorizontal();
            }
            if (_pasteSource != null)
            {
                EditorGUILayout.HelpBox("貼り付け先へマウスを移動し、左クリックで確定します。Escでキャンセル。", MessageType.Info);
            }
            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, MessageType.Warning);
            }
        }

        private void SetMode(NatoriCityBuilderInteractionMode mode)
        {
            if (_mode == mode)
            {
                return;
            }
            FinishGesture();
            ReleaseControl();
            _mode = mode;
            _rectangleDragging = false;
            _pasteSource = null;
            _message = null;
            RepaintSelection();
        }

        private void SetTool(NatoriCitySelectionToolKind tool)
        {
            if (_tool == tool)
            {
                return;
            }
            FinishGesture();
            ReleaseControl();
            _tool = tool;
            RepaintSelection();
        }

        public void DrawSceneToolbar(BuildingFloor floor)
        {
            //Inspectorと同じ命令で切り替える。どちらから操作しても状態は一つだけ保持する。
            GUILayout.BeginVertical(GUILayout.Width(232));
            GUILayout.BeginHorizontal();
            SetMode((NatoriCityBuilderInteractionMode)NatoriCityToolbarIcons.DrawMode((int)_mode));
            GUILayout.Space(8);
            int tool = NatoriCityToolbarIcons.DrawTool(IsPen ? -1 : (int)_tool);
            if (tool >= 0)
            {
                if (IsPen)
                {
                    SetMode(NatoriCityBuilderInteractionMode.SingleSelection);
                }
                SetTool((NatoriCitySelectionToolKind)tool);
            }
            GUILayout.EndHorizontal();
            int selectedCount = _selection.Resolve(floor).Count;
            string modeLabel = IsPen ? "ペン" : _mode == NatoriCityBuilderInteractionMode.SingleSelection ? "単一選択" : "範囲選択";
            GUILayout.Label(modeLabel + (IsPen ? "" : $"  /  選択: {selectedCount}件"));
            if (!IsPen && selectedCount > 0)
            {
                GUILayout.Label(_tool == NatoriCitySelectionToolKind.Move ? "矢印・四角をドラッグして移動"
                    : "円周をドラッグして90°刻みで回転");
            }
            if (_pasteSource != null)
            {
                EditorGUILayout.HelpBox("左クリックで貼り付け / Escでキャンセル", MessageType.Info);
            }
            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, MessageType.Warning);
            }
            GUILayout.EndVertical();
        }

        public bool OnSceneGUI(SceneView sceneView, NatoriCityBuildingEditorState state, BuildingFloor floor,
            float floorHeight, bool hasGridPoint, Vector2Int hoveredCell)
        {
            Event currentEvent = Event.current;
            bool hasCell = hasGridPoint && hoveredCell.x >= 0 && hoveredCell.y >= 0
                && hoveredCell.x < state.Building.GridSize.x && hoveredCell.y < state.Building.GridSize.y;
            List<BuildingPartPlacement> selected = _selection.Resolve(floor);
            if ((_gestureUndoGroup >= 0 || _ownedControl != 0) && GUIUtility.hotControl == 0)
            {
                FinishGesture();
                _ownedControl = 0;
            }
            HandleCommands(currentEvent, state, floor, selected);
            if (IsPen)
            {
                return false;
            }
            if (currentEvent.type == EventType.MouseMove)
            {
                sceneView.Repaint();
            }
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (currentEvent.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(control);
            }
            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;
            Handles.matrix = state.Building.transform.localToWorldMatrix;
            DrawPlacements(state.Building, selected, floorHeight, Color.cyan);
            if (_pasteSource != null)
            {
                HandlePaste(currentEvent, state, floor, floorHeight, hasCell, hoveredCell, control);
            }
            else
            {
                if (selected.Count > 0 && !_rectangleDragging && !currentEvent.alt)
                {
                    DrawGizmos(state, floor, selected, floorHeight);
                }
                HandleSelection(currentEvent, sceneView, state, floor, floorHeight, hasCell, hoveredCell, control);
                if (_rectangleDragging)
                {
                    DrawScreenRectangle();
                }
            }
            Handles.color = previousColor;
            Handles.matrix = previousMatrix;
            return true;
        }

        private void HandleCommands(Event currentEvent, NatoriCityBuildingEditorState state, BuildingFloor floor,
            List<BuildingPartPlacement> selected)
        {
            if (IsPen || EditorGUIUtility.editingTextField)
            {
                return;
            }
            bool commandEvent = currentEvent.type == EventType.ValidateCommand || currentEvent.type == EventType.ExecuteCommand;
            bool keyboard = currentEvent.type == EventType.KeyDown;
            bool copy = (commandEvent && currentEvent.commandName == "Copy")
                || (keyboard && (currentEvent.control || currentEvent.command) && currentEvent.keyCode == KeyCode.C);
            bool paste = (commandEvent && currentEvent.commandName == "Paste")
                || (keyboard && (currentEvent.control || currentEvent.command) && currentEvent.keyCode == KeyCode.V);
            bool delete = (commandEvent && (currentEvent.commandName == "Delete" || currentEvent.commandName == "SoftDelete"))
                || (keyboard && (currentEvent.keyCode == KeyCode.Delete || currentEvent.keyCode == KeyCode.Backspace));
            if (copy || paste || delete)
            {
                if (currentEvent.type != EventType.ValidateCommand)
                {
                    FinishGesture();
                    if (copy)
                    {
                        NatoriCityPlacementClipboard.Copy(selected);
                    }
                    else if (paste)
                    {
                        if (NatoriCityPlacementClipboard.TryRead(Vector2Int.zero, out var placements, out _message))
                        {
                            _pasteSource = placements;
                            _rectangleDragging = false;
                            ReleaseControl();
                        }
                    }
                    else if (_pasteSource == null)
                    {
                        Delete(state, floor, selected);
                    }
                }
                currentEvent.Use();
                RepaintSelection();
            }
            else if (keyboard && currentEvent.keyCode == KeyCode.Escape)
            {
                if (_gestureUndoGroup >= 0)
                {
                    Undo.RevertAllDownToGroup(_gestureUndoGroup);
                    _gestureUndoGroup = -1;
                    NatoriCityBuildingEditorStateRegistry.InvalidateAll();
                }
                if (_pasteSource == null && !_rectangleDragging)
                {
                    _selection.Clear();
                }
                _pasteSource = null;
                _rectangleDragging = false;
                ReleaseControl();
                _message = null;
                _rotationDrag.Reset();
                currentEvent.Use();
                RepaintSelection();
            }
        }

        private void HandleSelection(Event currentEvent, SceneView sceneView, NatoriCityBuildingEditorState state, BuildingFloor floor, float floorHeight,
            bool hasCell, Vector2Int cell, int control)
        {
            if (_rectangleDragging && GUIUtility.hotControl != control)
            {
                _rectangleDragging = false;
                _ownedControl = 0;
            }
            if (_rectangleDragging)
            {
                _rectangleEnd = currentEvent.mousePosition;
                if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
                {
                    _selection.Select(floor, InScreenRectangle(sceneView, state.Building, floor, floorHeight),
                        _additive, _toggle);
                    _rectangleDragging = false;
                    ReleaseControl();
                    currentEvent.Use();
                }
                else if (currentEvent.type == EventType.MouseDrag)
                {
                    currentEvent.Use();
                }
                RepaintSelection();
                return;
            }
            if (currentEvent.alt || currentEvent.type != EventType.MouseDown
                || currentEvent.button != 0 || GUIUtility.hotControl != 0 || HandleUtility.nearestControl != control)
            {
                return;
            }
            _message = null;
            if (_mode == NatoriCityBuilderInteractionMode.RectangleSelection)
            {
                _rectangleDragging = true;
                _rectangleStart = currentEvent.mousePosition;
                _rectangleEnd = currentEvent.mousePosition;
                _additive = currentEvent.shift;
                _toggle = currentEvent.control || currentEvent.command;
                GUIUtility.hotControl = control;
                _ownedControl = control;
            }
            else
            {
                if (!hasCell)
                {
                    return;
                }
                IReadOnlyList<BuildingPartPlacement> hits = state.ModelIndex.GetOccupancy(floor).GetPlacements(cell);
                var picked = new List<BuildingPartPlacement>();
                if (hits.Count > 0)
                {
                    int next = 0;
                    for (int hitSeek = 0; hitSeek < hits.Count; hitSeek++)
                    {
                        if (hits[hitSeek].Identifier == _lastPickedIdentifier)
                        {
                            next = (hitSeek + 1) % hits.Count;
                            break;
                        }
                    }
                    picked.Add(hits[next]);
                    _lastPickedIdentifier = hits[next].Identifier;
                }
                _selection.Select(floor, picked, currentEvent.shift, currentEvent.control || currentEvent.command);
            }
            currentEvent.Use();
            RepaintSelection();
        }

        private void HandlePaste(Event currentEvent, NatoriCityBuildingEditorState state, BuildingFloor floor,
            float floorHeight, bool hasCell, Vector2Int cell, int control)
        {
            if (!hasCell)
            {
                return;
            }
            List<BuildingPartPlacement> candidates = NatoriCityPlacementBatch.Transform(
                _pasteSource, cell, 0, false, Vector2Int.zero);
            bool valid = NatoriCityPlacementBatch.Validate(state, floor, Array.Empty<BuildingPartPlacement>(), candidates, out string error);
            DrawPlacements(state.Building, candidates, floorHeight, valid ? Color.green : Color.red);
            if (!currentEvent.alt && currentEvent.type == EventType.MouseDown && currentEvent.button == 0
                && GUIUtility.hotControl == 0 && HandleUtility.nearestControl == control)
            {
                if (valid && NatoriCityPlacementBatch.Apply(state, floor, Array.Empty<BuildingPartPlacement>(), candidates,
                    "Paste Natori City Builder Selection", out error))
                {
                    _selection.Select(floor, candidates, false, false);
                    _pasteSource = null;
                }
                _message = error;
                currentEvent.Use();
                RepaintSelection();
            }
        }

        private void DrawGizmos(NatoriCityBuildingEditorState state, BuildingFloor floor,
            List<BuildingPartPlacement> selected, float height)
        {
            Vector3 pivot = ToLocal(state.Building, _selection.Pivot, height);
            float size = HandleUtility.GetHandleSize(pivot) * 0.7f;
            if (_tool == NatoriCitySelectionToolKind.Move)
            {
                EditorGUI.BeginChangeCheck();
                Handles.color = Handles.xAxisColor;
                Vector3 position = Handles.Slider(pivot, Vector3.right, size, Handles.ArrowHandleCap, state.Building.Settings.HorizontalCellSize);
                Handles.color = Handles.zAxisColor;
                position += Handles.Slider(pivot, Vector3.forward, size, Handles.ArrowHandleCap, state.Building.Settings.HorizontalCellSize) - pivot;
                Handles.color = Color.cyan;
                position += Handles.Slider2D(pivot, Vector3.up, Vector3.right, Vector3.forward,
                    size * 0.16f, Handles.RectangleHandleCap, state.Building.Settings.HorizontalCellSize) - pivot;
                if (EditorGUI.EndChangeCheck())
                {
                    float cellSize = state.Building.Settings.HorizontalCellSize;
                    Vector2Int offset = new(Mathf.RoundToInt((position.x - pivot.x) / cellSize),
                        Mathf.RoundToInt((position.z - pivot.z) / cellSize));
                    if (offset != Vector2Int.zero)
                    {
                        if (_gestureUndoGroup < 0)
                        {
                            Undo.IncrementCurrentGroup();
                            _gestureUndoGroup = Undo.GetCurrentGroup();
                            Undo.SetCurrentGroupName("Move Natori City Builder Selection");
                        }
                        if (NatoriCityPlacementBatch.Apply(state, floor, selected,
                            NatoriCityPlacementBatch.Transform(selected, offset, 0, false, _selection.Pivot),
                            "Move Natori City Builder Selection", out _message))
                        {
                            _selection.MovePivot(offset);
                        }
                    }
                    _ownedControl = GUIUtility.hotControl;
                    RepaintSelection();
                }
                return;
            }
            Handles.color = _tool == NatoriCitySelectionToolKind.RotateGroup ? Color.yellow : Color.magenta;
            if (_tool == NatoriCitySelectionToolKind.RotateEach)
            {
                foreach (BuildingPartPlacement placement in selected)
                {
                    Vector3 anchor = ToLocal(state.Building, placement.AnchorCell, height);
                    Handles.DrawWireDisc(anchor, Vector3.up, state.Building.Settings.HorizontalCellSize * 0.3f);
                }
            }
            //表示専用の円ではなく、円周全体で入力を取得する回転ハンドルを使用する。
            //標準ハンドルがhotControlを所有するため、範囲選択のドラッグと競合しない。
            //円周上に別のボタンを置くとその地点からドラッグできなくなるので、±90度ボタンはInspectorだけに置く。
            EditorGUI.BeginChangeCheck();
            Quaternion rotation = Handles.Disc(_rotationDrag.HandleRotation, pivot, Vector3.up, size, false, 90.0f);
            if (EditorGUI.EndChangeCheck())
            {
                int turns = _rotationDrag.Update(rotation);
                if (turns != 0)
                {
                    if (_gestureUndoGroup < 0)
                    {
                        Undo.IncrementCurrentGroup();
                        _gestureUndoGroup = Undo.GetCurrentGroup();
                        Undo.SetCurrentGroupName("Rotate Natori City Builder Selection");
                    }
                    if (Rotate(state, floor, selected, turns))
                    {
                        _rotationDrag.Accept(turns);
                    }
                }
            }
            if (GUIUtility.hotControl != 0)
            {
                _ownedControl = GUIUtility.hotControl;
            }
            Handles.Label(pivot + Vector3.forward * size,
                _tool == NatoriCitySelectionToolKind.RotateGroup ? "配置全体の回転中心" : "各配置のアンカーを固定");
        }

        private List<BuildingPartPlacement> InScreenRectangle(SceneView sceneView,
            NatoriCityBuildingComponent building, BuildingFloor floor, float height)
        {
            Rect rectangle = NatoriCityScreenRectangle.FromPoints(_rectangleStart, _rectangleEnd);
            var viewport = new Rect(Vector2.zero, sceneView.camera.pixelRect.size / EditorGUIUtility.pixelsPerPoint);
            var result = new List<BuildingPartPlacement>();
            var corners = new Vector3[4];
            foreach (BuildingPartPlacement placement in floor.Placements)
            {
                Vector2Int size = NatoriCityGridGeometry.GetRotatedFootprint(
                    placement.FootprintAtPlacement, placement.QuarterTurnsClockwise);
                Vector2Int minimum = placement.AnchorCell;
                corners[0] = building.transform.TransformPoint(ToLocal(building, minimum, height));
                corners[1] = building.transform.TransformPoint(ToLocal(building, minimum + new Vector2Int(0, size.y), height));
                corners[2] = building.transform.TransformPoint(ToLocal(building, minimum + size, height));
                corners[3] = building.transform.TransformPoint(ToLocal(building, minimum + new Vector2Int(size.x, 0), height));
                if (NatoriCityScreenRectangle.Intersects(sceneView.camera, viewport, rectangle, corners))
                {
                    result.Add(placement);
                }
            }
            return result;
        }

        private void DrawScreenRectangle()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }
            //床へのレイ交差とは無関係に、ドラッグ開始点と現在点をGUI座標でそのまま描く。
            Rect rectangle = NatoriCityScreenRectangle.FromPoints(_rectangleStart, _rectangleEnd);
            Handles.BeginGUI();
            EditorGUI.DrawRect(rectangle, new Color(0.2f, 0.7f, 1, 0.12f));
            Color border = new Color(0.3f, 0.8f, 1, 1);
            EditorGUI.DrawRect(new Rect(rectangle.xMin, rectangle.yMin, rectangle.width, 1), border);
            EditorGUI.DrawRect(new Rect(rectangle.xMin, rectangle.yMax - 1, rectangle.width, 1), border);
            EditorGUI.DrawRect(new Rect(rectangle.xMin, rectangle.yMin, 1, rectangle.height), border);
            EditorGUI.DrawRect(new Rect(rectangle.xMax - 1, rectangle.yMin, 1, rectangle.height), border);
            Handles.EndGUI();
        }

        private void FinishGesture()
        {
            if (_gestureUndoGroup >= 0)
            {
                Undo.CollapseUndoOperations(_gestureUndoGroup);
                _gestureUndoGroup = -1;
            }
            _rotationDrag.Reset();
        }

        private bool Rotate(NatoriCityBuildingEditorState state, BuildingFloor floor,
            List<BuildingPartPlacement> selected, int turns)
        {
            bool asGroup = _tool != NatoriCitySelectionToolKind.RotateEach;
            bool applied = NatoriCityPlacementBatch.Apply(state, floor, selected,
                NatoriCityPlacementBatch.Transform(selected, Vector2Int.zero, turns, asGroup, _selection.Pivot),
                asGroup ? "Rotate Natori City Builder Selection" : "Rotate Each Natori City Builder Part", out _message);
            RepaintSelection();
            return applied;
        }

        private void Delete(NatoriCityBuildingEditorState state, BuildingFloor floor, List<BuildingPartPlacement> selected)
        {
            if (selected.Count == 0)
            {
                return;
            }
            if (NatoriCityPlacementBatch.Apply(state, floor, selected, Array.Empty<BuildingPartPlacement>(),
                "Delete Natori City Builder Selection", out _message))
            {
                _selection.Clear();
            }
        }

        private static Vector3 ToLocal(NatoriCityBuildingComponent building, Vector2 cell, float height)
        {
            float cellSize = building.Settings.HorizontalCellSize;
            Vector2 minimum = NatoriCityGridGeometry.GetGridMinimum(building.GridSize, cellSize);
            return new Vector3(minimum.x + cell.x * cellSize, height + 0.025f, minimum.y + cell.y * cellSize);
        }

        private static void DrawPlacements(NatoriCityBuildingComponent building, IReadOnlyList<BuildingPartPlacement> placements,
            float height, Color color)
        {
            foreach (BuildingPartPlacement placement in placements)
            {
                Vector2Int size = NatoriCityGridGeometry.GetRotatedFootprint(placement.FootprintAtPlacement, placement.QuarterTurnsClockwise);
                DrawRectangle(building, placement.AnchorCell, size, height, color);
                Vector3 center = ToLocal(building, (Vector2)placement.AnchorCell + (Vector2)size * 0.5f, height);
                Vector3 direction = Quaternion.Euler(0, placement.QuarterTurnsClockwise * 90, 0) * Vector3.forward;
                Handles.color = color;
                if (Event.current.type == EventType.Repaint)
                {
                    Handles.ArrowHandleCap(0, center, Quaternion.LookRotation(direction),
                        building.Settings.HorizontalCellSize * 0.45f, EventType.Repaint);
                }
            }
        }

        private static void DrawRectangle(NatoriCityBuildingComponent building, Vector2Int minimum, Vector2Int size,
            float height, Color color)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }
            var vertices = new[]
            {
                ToLocal(building, minimum, height),
                ToLocal(building, minimum + new Vector2Int(0, size.y), height),
                ToLocal(building, minimum + size, height),
                ToLocal(building, minimum + new Vector2Int(size.x, 0), height),
            };
            Handles.DrawSolidRectangleWithOutline(vertices, new Color(color.r, color.g, color.b, 0.15f), color);
        }
    }
}
