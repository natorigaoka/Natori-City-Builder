using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    //候補の計算・検証中はシーンを変更しない。確定時だけモデルと生成物を同じUndoへ記録する。
    internal static class NatoriCityPlacementBatch
    {
        public static List<BuildingPartPlacement> Transform(IReadOnlyList<BuildingPartPlacement> source,
            Vector2Int offset, int quarterTurns, bool rotateAsGroup, Vector2Int pivot)
        {
            var result = new List<BuildingPartPlacement>(source.Count);
            int turns = ((quarterTurns % 4) + 4) % 4;
            foreach (BuildingPartPlacement placement in source)
            {
                var candidate = new BuildingPartPlacement(placement, false);
                Vector2Int anchor = placement.AnchorCell;
                Vector2Int size = NatoriCityGridGeometry.GetRotatedFootprint(
                    placement.FootprintAtPlacement, placement.QuarterTurnsClockwise);
                if (rotateAsGroup)
                {
                    for (int turnSeek = 0; turnSeek < turns; turnSeek++)
                    {
                        //Unityの+Y回転はXZ平面で(x,z)->(z,-x)。矩形の最小隅も占有幅を含めて回す。
                        Vector2Int relative = anchor - pivot;
                        anchor = pivot + new Vector2Int(relative.y, -relative.x - size.x);
                        size = new Vector2Int(size.y, size.x);
                    }
                }
                candidate.SetGridTransform(anchor + offset, placement.QuarterTurnsClockwise + turns);
                result.Add(candidate);
            }
            return result;
        }

        public static bool Validate(NatoriCityBuildingEditorState state, BuildingFloor floor,
            IReadOnlyList<BuildingPartPlacement> removed, IReadOnlyList<BuildingPartPlacement> candidates,
            out string error)
        {
            error = null;
            if (!state.Validation.IsValid)
            {
                error = state.Validation.Errors[0];
                return false;
            }
            if (!state.ModelIndex.TryGetFloor(floor.Identifier, out BuildingFloor currentFloor) || currentFloor != floor)
            {
                error = "編集する階が変更されました。選択し直してください。";
                return false;
            }
            var removedIdentifiers = new HashSet<string>();
            foreach (BuildingPartPlacement placement in removed)
            {
                if (!state.ModelIndex.TryGetPlacement(placement.Identifier, out BuildingPartPlacement current)
                    || state.ModelIndex.GetFloor(current) != floor || current != placement)
                {
                    error = "選択した配置が変更されました。選択し直してください。";
                    return false;
                }
                removedIdentifiers.Add(placement.Identifier);
            }
            var anchors = new HashSet<NatoriCityPlacementAnchor>();
            foreach (BuildingPartPlacement placement in floor.Placements)
            {
                if (!removedIdentifiers.Contains(placement.Identifier))
                {
                    anchors.Add(new NatoriCityPlacementAnchor(placement));
                }
            }
            foreach (BuildingPartPlacement candidate in candidates)
            {
                BuildingPartDefinition part = candidate.PartDefinition;
                if (part == null || !state.ModelIndex.IsPartAvailable(part) || part.Prefab == null
                    || part.BuildingGroup != floor.BuildingGroup
                    || part.BuildingGroup != candidate.BuildingGroupAtPlacement
                    || part.PlacementType == null || part.PlacementType != candidate.PlacementTypeAtPlacement
                    || part.Footprint != candidate.FootprintAtPlacement
                    || part.PlacementType.DisallowSamePartAtSameAnchor != candidate.DisallowSamePartAtSameAnchorAtPlacement)
                {
                    error = "パーツの定義・階層グループ・Asset Listがコピー元または配置時と一致しません。";
                    return false;
                }
                Vector2Int size = NatoriCityGridGeometry.GetRotatedFootprint(
                    candidate.FootprintAtPlacement, candidate.QuarterTurnsClockwise);
                if (!NatoriCityGridGeometry.IsFootprintInsideGrid(candidate.AnchorCell, size, state.Building.GridSize))
                {
                    error = "選択した配置がグリッド範囲外になります。";
                    return false;
                }
                bool isNewAnchor = anchors.Add(new NatoriCityPlacementAnchor(candidate));
                if (candidate.DisallowSamePartAtSameAnchorAtPlacement && !isNewAnchor)
                {
                    error = "同じパーツを同じアンカーセルへ重複配置できません。";
                    return false;
                }
            }
            return true;
        }

        public static bool Apply(NatoriCityBuildingEditorState state, BuildingFloor floor,
            IReadOnlyList<BuildingPartPlacement> removed, IReadOnlyList<BuildingPartPlacement> candidates,
            string undoName, out string error)
        {
            if (!Validate(state, floor, removed, candidates, out error))
            {
                return false;
            }
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RegisterCompleteObjectUndo(state.Building, undoName);
            var existing = new Dictionary<string, BuildingPartPlacement>();
            foreach (BuildingPartPlacement placement in removed)
            {
                existing.Add(placement.Identifier, placement);
            }
            var retained = new HashSet<string>();
            foreach (BuildingPartPlacement candidate in candidates)
            {
                retained.Add(candidate.Identifier);
                if (existing.TryGetValue(candidate.Identifier, out BuildingPartPlacement placement))
                {
                    placement.SetGridTransform(candidate.AnchorCell, candidate.QuarterTurnsClockwise);
                }
                else
                {
                    floor.AddPlacement(candidate);
                }
            }
            foreach (BuildingPartPlacement placement in removed)
            {
                if (!retained.Contains(placement.Identifier))
                {
                    floor.RemovePlacement(placement);
                }
            }
            EditorUtility.SetDirty(state.Building);
            PrefabUtility.RecordPrefabInstancePropertyModifications(state.Building);
            state.RefreshModel();
            //選択対象だけ生成物へ反映し、無関係な階やPrefabインスタンスを作り直さない。
            bool success = state.Validation.IsValid;
            foreach (BuildingPartPlacement placement in removed)
            {
                if (success && !retained.Contains(placement.Identifier))
                {
                    success = NatoriCityBuildingGeneratedSynchronizer.Apply(state,
                        NatoriCityBuildingChangeSet.PlacementRemoved(placement.Identifier), undoName);
                }
            }
            foreach (BuildingPartPlacement candidate in candidates)
            {
                if (success)
                {
                    success = NatoriCityBuildingGeneratedSynchronizer.Apply(state,
                        NatoriCityBuildingChangeSet.PlacementRotated(candidate.Identifier), undoName);
                }
            }
            if (!success)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                NatoriCityBuildingEditorStateRegistry.InvalidateAll();
                error = "生成物の更新に失敗したため、操作全体を元に戻しました。";
                return false;
            }
            Undo.CollapseUndoOperations(undoGroup);
            SceneView.RepaintAll();
            return true;
        }
    }
}
