using System.Collections.Generic;
using UnityEngine;

namespace Natori.CityBuilder
{
    public static class NatoriCityBuildingValidator
    {
        public static NatoriCityBuildingValidationResult Validate(NatoriCityBuildingComponent building)
        {
            return Validate(building, false);
        }

        internal static NatoriCityBuildingValidationResult ValidateBeforeGridPruning(
            NatoriCityBuildingComponent building)
        {
            return Validate(building, true);
        }

        private static NatoriCityBuildingValidationResult Validate(
            NatoriCityBuildingComponent building,
            bool allowOutOfGridPlacements)
        {
            var result = new NatoriCityBuildingValidationResult();
            if (building.Settings == null)
            {
                result.AddError("共通設定アセットが設定されていません。");
            }
            else if (building.Settings.HorizontalCellSize <= 0.0f)
            {
                result.AddError("水平セルサイズは0より大きい必要があります。");
            }
            if (building.GridSize.x <= 0 || building.GridSize.y <= 0)
            {
                result.AddError("グリッド範囲はX、Zともに1以上である必要があります。");
            }

            var availableParts = CollectAvailableParts(building, result);
            var floorIdentifiers = new HashSet<string>();
            var placementIdentifiers = new HashSet<string>();
            for (int floorSeek = 0; floorSeek < building.Floors.Count; floorSeek++)
            {
                BuildingFloor floor = building.Floors[floorSeek];
                if (floor == null)
                {
                    result.AddError($"階{floorSeek}のデータがnullです。");
                    continue;
                }
                ValidateFloor(building, floor, floorSeek, availableParts, floorIdentifiers,
                    placementIdentifiers, allowOutOfGridPlacements, result);
            }
            return result;
        }

        private static HashSet<BuildingPartDefinition> CollectAvailableParts(
            NatoriCityBuildingComponent building,
            NatoriCityBuildingValidationResult result)
        {
            var availableParts = new HashSet<BuildingPartDefinition>();
            for (int assetListSeek = 0; assetListSeek < building.AssetLists.Count; assetListSeek++)
            {
                NatoriCityBuildingAssetList assetList = building.AssetLists[assetListSeek];
                if (assetList == null)
                {
                    result.AddError($"Asset List {assetListSeek}がMissingです。");
                    continue;
                }
                for (int partSeek = 0; partSeek < assetList.Parts.Count; partSeek++)
                {
                    BuildingPartDefinition part = assetList.Parts[partSeek];
                    if (part == null)
                    {
                        result.AddError($"{assetList.name}の登録パーツ{partSeek}がMissingです。");
                        continue;
                    }
                    availableParts.Add(part);
                }
            }
            return availableParts;
        }

        private static void ValidateFloor(
            NatoriCityBuildingComponent building,
            BuildingFloor floor,
            int floorIndex,
            HashSet<BuildingPartDefinition> availableParts,
            HashSet<string> floorIdentifiers,
            HashSet<string> placementIdentifiers,
            bool allowOutOfGridPlacements,
            NatoriCityBuildingValidationResult result)
        {
            if (string.IsNullOrEmpty(floor.Identifier) || !floorIdentifiers.Add(floor.Identifier))
            {
                result.AddError($"階{floorIndex}の識別子が空、または建物内で重複しています。");
            }
            if (floor.BuildingGroup == null)
            {
                result.AddError($"階{floorIndex}の階層グループがMissingです。");
            }
            else if (floor.BuildingGroup.Height <= 0.0f)
            {
                result.AddError($"階{floorIndex}の階層グループ高さが0以下です。");
            }

            var duplicateAnchors =
                new Dictionary<BuildingPartDefinition, HashSet<Vector2Int>>();
            for (int placementSeek = 0; placementSeek < floor.Placements.Count; placementSeek++)
            {
                ValidatePlacement(building, floor, floorIndex, placementSeek, floor.Placements[placementSeek],
                    availableParts, placementIdentifiers, duplicateAnchors, allowOutOfGridPlacements, result);
            }
        }

        private static void ValidatePlacement(
            NatoriCityBuildingComponent building,
            BuildingFloor floor,
            int floorIndex,
            int placementIndex,
            BuildingPartPlacement placement,
            HashSet<BuildingPartDefinition> availableParts,
            HashSet<string> placementIdentifiers,
            Dictionary<BuildingPartDefinition, HashSet<Vector2Int>> duplicateAnchors,
            bool allowOutOfGridPlacements,
            NatoriCityBuildingValidationResult result)
        {
            string location = $"階{floorIndex}の配置{placementIndex}";
            if (placement == null)
            {
                result.AddError($"{location}がnullです。");
                return;
            }
            if (string.IsNullOrEmpty(placement.Identifier) || !placementIdentifiers.Add(placement.Identifier))
            {
                result.AddError($"{location}の識別子が空、または建物内で重複しています。");
            }
            BuildingPartDefinition part = placement.PartDefinition;
            if (part == null)
            {
                result.AddError($"{location}のPart DefinitionがMissingです。");
                return;
            }
            if (!availableParts.Contains(part))
            {
                result.AddError($"{location}が参照する「{part.Label}」のAsset Listは建物から外されています。");
            }
            if (part.BuildingGroup != placement.BuildingGroupAtPlacement)
            {
                result.AddError($"{location}「{part.Label}」の所属階層グループが配置時から変更されています。");
            }
            if (part.PlacementType != placement.PlacementTypeAtPlacement)
            {
                result.AddError($"{location}「{part.Label}」の配置種別が配置時から変更されています。");
            }
            if (part.Footprint != placement.FootprintAtPlacement)
            {
                result.AddError($"{location}「{part.Label}」の占有サイズが配置時から変更されています。");
            }
            if (placement.FootprintAtPlacement.x <= 0 || placement.FootprintAtPlacement.y <= 0)
            {
                result.AddError($"{location}「{part.Label}」の配置時占有サイズが0以下です。");
            }
            if (part.PlacementType == null)
            {
                result.AddError($"{location}「{part.Label}」の配置種別がMissingです。");
            }
            else if (part.PlacementType.DisallowSamePartAtSameAnchor
                != placement.DisallowSamePartAtSameAnchorAtPlacement)
            {
                result.AddError($"{location}「{part.Label}」の重複規則が配置時から変更されています。");
            }
            if (part.BuildingGroup != floor.BuildingGroup)
            {
                result.AddError($"{location}「{part.Label}」は階{floorIndex}の階層グループに属していません。");
            }
            Vector2Int rotatedFootprint = NatoriCityGridGeometry.GetRotatedFootprint(
                placement.FootprintAtPlacement,
                placement.QuarterTurnsClockwise);
            if (!allowOutOfGridPlacements && !NatoriCityGridGeometry.IsFootprintInsideGrid(
                placement.AnchorCell,
                rotatedFootprint,
                building.GridSize))
            {
                result.AddError($"{location}「{part.Label}」が現在のグリッド範囲外です。");
            }
            if (placement.DisallowSamePartAtSameAnchorAtPlacement)
            {
                if (!duplicateAnchors.TryGetValue(part, out HashSet<Vector2Int> partAnchors))
                {
                    partAnchors = new HashSet<Vector2Int>();
                    duplicateAnchors.Add(part, partAnchors);
                }
                if (!partAnchors.Add(placement.AnchorCell))
                {
                    result.AddError($"{location}「{part.Label}」が同じアンカーセルへ重複配置されています。");
                }
            }
            if (part.Prefab == null)
            {
                result.AddWarning($"{location}「{part.Label}」のPrefabがMissingのため生成されません。");
            }
        }
    }
}
