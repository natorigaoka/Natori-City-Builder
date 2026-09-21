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
        private NatoriCityBuilderInteractionMode _mode;
        private NatoriCitySelectionToolKind _tool;
        private bool _rectangleDragging;
        private Vector2Int _rectangleStart;
        private Vector2Int _rectangleEnd;
        private bool _additive;
        private bool _toggle;
        private string _lastPickedIdentifier;
        private List<BuildingPartPlacement> _pasteSource;
        private int _moveUndoGroup = -1;
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
            _moveUndoGroup = -1;
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
            FinishMove();
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
            if (mode != (int)_mode)
            {
                FinishMove();
                ReleaseControl();
                _mode = (NatoriCityBuilderInteractionMode)mode;
                _rectangleDragging = false;
                _pasteSource = null;
                _message = null;
                RepaintSelection();
            }
            if (IsPen)
            {
                return;
            }
            List<BuildingPartPlacement> selected = _selection.Resolve(floor);
            _tool = (NatoriCitySelectionToolKind)GUILayout.Toolbar((int)_tool,
                new[] { "グリッド移動", "配置全体の回転", "各配置の回転" });
            EditorGUILayout.LabelField($"選択: {selected.Count}件");
            EditorGUILayout.HelpBox(_tool == NatoriCitySelectionToolKind.Move
                ? "赤・青の矢印でX/Z方向、中央の四角でXZ平面を1セル単位で移動します。"
                : _tool == NatoriCitySelectionToolKind.RotateGroup
                    ? "黄色の中心交点の周りに、相対配置と各パーツの向きをまとめて90度回転します。"
                    : "各配置のアンカーセル（占有範囲の最小X/Z）を固定し、向きを90度変更します。",
                MessageType.None);
            EditorGUILayout.HelpBox(
                "Shift: 追加 / Ctrl: 選択切替 / 空白クリック: 解除\n"
                + "範囲選択: 占有範囲が触れた配置すべて。単一選択: 重なりをクリック順に選択。\n"
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

        public bool OnSceneGUI(SceneView sceneView, NatoriCityBuildingEditorState state, BuildingFloor floor,
            float floorHeight, bool hasGridPoint, Vector2Int hoveredCell)
        {
            Event currentEvent = Event.current;
            bool hasCell = hasGridPoint && hoveredCell.x >= 0 && hoveredCell.y >= 0
                && hoveredCell.x < state.Building.GridSize.x && hoveredCell.y < state.Building.GridSize.y;
            if (_rectangleDragging && hasGridPoint)
            {
                _rectangleEnd = Vector2Int.Max(Vector2Int.zero,
                    Vector2Int.Min(state.Building.GridSize - Vector2Int.one, hoveredCell));
            }
            List<BuildingPartPlacement> selected = _selection.Resolve(floor);
            if (_moveUndoGroup >= 0 && GUIUtility.hotControl == 0)
            {
                FinishMove();
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
                HandleSelection(currentEvent, state, floor, hasCell, hoveredCell, control);
                if (_rectangleDragging)
                {
                    Vector2Int minimum = Vector2Int.Min(_rectangleStart, _rectangleEnd);
                    Vector2Int size = Vector2Int.Max(_rectangleStart, _rectangleEnd) - minimum + Vector2Int.one;
                    DrawRectangle(state.Building, minimum, size, floorHeight, Color.yellow);
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
                    FinishMove();
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
                if (_moveUndoGroup >= 0)
                {
                    Undo.RevertAllDownToGroup(_moveUndoGroup);
                    _moveUndoGroup = -1;
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
                currentEvent.Use();
                RepaintSelection();
            }
        }

        private void HandleSelection(Event currentEvent, NatoriCityBuildingEditorState state, BuildingFloor floor,
            bool hasCell, Vector2Int cell, int control)
        {
            if (_rectangleDragging && GUIUtility.hotControl != control)
            {
                _rectangleDragging = false;
                _ownedControl = 0;
            }
            if (_rectangleDragging)
            {
                if (hasCell)
                {
                    _rectangleEnd = cell;
                }
                if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
                {
                    _selection.Select(floor, NatoriCityPlacementSelection.InRectangle(floor, _rectangleStart, _rectangleEnd),
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
            if (currentEvent.alt || !hasCell || currentEvent.type != EventType.MouseDown
                || currentEvent.button != 0 || GUIUtility.hotControl != 0 || HandleUtility.nearestControl != control)
            {
                return;
            }
            _message = null;
            if (_mode == NatoriCityBuilderInteractionMode.RectangleSelection)
            {
                _rectangleDragging = true;
                _rectangleStart = cell;
                _rectangleEnd = cell;
                _additive = currentEvent.shift;
                _toggle = currentEvent.control || currentEvent.command;
                GUIUtility.hotControl = control;
                _ownedControl = control;
            }
            else
            {
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
                        if (_moveUndoGroup < 0)
                        {
                            Undo.IncrementCurrentGroup();
                            _moveUndoGroup = Undo.GetCurrentGroup();
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
            Handles.DrawWireDisc(pivot, Vector3.up, size);
            Handles.Label(pivot + Vector3.forward * size,
                _tool == NatoriCitySelectionToolKind.RotateGroup ? "配置全体の回転中心" : "各配置のアンカーを固定");
            Quaternion clockwise = Quaternion.LookRotation(Vector3.back, Vector3.up);
            Quaternion counterClockwise = Quaternion.LookRotation(Vector3.back, Vector3.up);
            Vector3 positiveButton = pivot + Vector3.right * size;
            Vector3 negativeButton = pivot - Vector3.right * size;
            Handles.Label(positiveButton, "+90°");
            Handles.Label(negativeButton, "−90°");
            if (Handles.Button(positiveButton, clockwise, size * 0.18f, size * 0.22f, Handles.ConeHandleCap))
            {
                Rotate(state, floor, selected, 1);
            }
            if (Handles.Button(negativeButton, counterClockwise, size * 0.18f, size * 0.22f, Handles.ConeHandleCap))
            {
                Rotate(state, floor, selected, -1);
            }
        }

        private void FinishMove()
        {
            if (_moveUndoGroup >= 0)
            {
                Undo.CollapseUndoOperations(_moveUndoGroup);
                _moveUndoGroup = -1;
            }
        }

        private void Rotate(NatoriCityBuildingEditorState state, BuildingFloor floor,
            List<BuildingPartPlacement> selected, int turns)
        {
            bool asGroup = _tool != NatoriCitySelectionToolKind.RotateEach;
            NatoriCityPlacementBatch.Apply(state, floor, selected,
                NatoriCityPlacementBatch.Transform(selected, Vector2Int.zero, turns, asGroup, _selection.Pivot),
                asGroup ? "Rotate Natori City Builder Selection" : "Rotate Each Natori City Builder Part", out _message);
            RepaintSelection();
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
