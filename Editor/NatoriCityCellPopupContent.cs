using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    public sealed class NatoriCityCellPopupContent : PopupWindowContent
    {
        private readonly NatoriCityBuildingComponent _building;
        private readonly string _floorIdentifier;
        private readonly Vector2Int _cell;

        public NatoriCityCellPopupContent(
            NatoriCityBuildingComponent building,
            string floorIdentifier,
            Vector2Int cell)
        {
            _building = building;
            _floorIdentifier = floorIdentifier;
            _cell = cell;
        }

        public override Vector2 GetWindowSize()
        {
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(_building);
            int placementCount = state.ModelIndex.TryGetFloor(_floorIdentifier, out BuildingFloor floor)
                ? state.ModelIndex.GetPlacements(floor, _cell).Count
                : 0;
            return new Vector2(430.0f, 52.0f + Mathf.Max(1, placementCount) * 25.0f);
        }

        public override void OnGUI(Rect rectangle)
        {
            EditorGUILayout.LabelField($"セル ({_cell.x}, {_cell.y})", EditorStyles.boldLabel);
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(_building);
            if (!state.ModelIndex.TryGetFloor(_floorIdentifier, out BuildingFloor floor))
            {
                EditorGUILayout.HelpBox("編集中の階が削除されています。", MessageType.Error);
                return;
            }
            IReadOnlyList<BuildingPartPlacement> placements =
                state.ModelIndex.GetPlacements(floor, _cell);
            if (placements.Count == 0)
            {
                EditorGUILayout.HelpBox("このセルを占有するパーツはありません。", MessageType.Info);
                return;
            }

            for (int placementSeek = 0; placementSeek < placements.Count; placementSeek++)
            {
                BuildingPartPlacement placement = placements[placementSeek];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(placement.PartDefinition.Label, GUILayout.Width(210.0f));
                if (GUILayout.Button("左90°", GUILayout.Width(60.0f)))
                {
                    NatoriCityBuildingEditorActions.RotatePlacement(_building, placement, false);
                    editorWindow.Repaint();
                }
                if (GUILayout.Button("右90°", GUILayout.Width(60.0f)))
                {
                    NatoriCityBuildingEditorActions.RotatePlacement(_building, placement, true);
                    editorWindow.Repaint();
                }
                if (GUILayout.Button("削除", GUILayout.Width(60.0f)))
                {
                    NatoriCityBuildingEditorActions.RemovePlacement(_building, floor, placement);
                    editorWindow.Repaint();
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

    }
}
