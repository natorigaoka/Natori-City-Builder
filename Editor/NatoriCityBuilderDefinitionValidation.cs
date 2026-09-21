using System.Collections.Generic;

namespace Natori.CityBuilder.Editor
{
    public static class NatoriCityBuilderDefinitionValidation
    {
        public static void ValidateBuilding(
            NatoriCityBuildingComponent building,
            NatoriCityBuildingValidationResult result)
        {
            NatoriCityBuilderSettings settings = NatoriCityBuilderProjectSettings.Settings;
            if (settings == null)
            {
                result.AddError("Project Settingsに共通設定アセットが登録されていません。");
                return;
            }

            var registeredGroups = new HashSet<BuildingGroupDefinition>();
            for (int groupSeek = 0; groupSeek < settings.BuildingGroups.Count; groupSeek++)
            {
                BuildingGroupDefinition group = settings.BuildingGroups[groupSeek];
                if (group != null)
                {
                    registeredGroups.Add(group);
                }
            }
            var registeredPlacementTypes = new HashSet<PlacementTypeDefinition>();
            for (int typeSeek = 0; typeSeek < settings.PlacementTypes.Count; typeSeek++)
            {
                PlacementTypeDefinition placementType = settings.PlacementTypes[typeSeek];
                if (placementType != null)
                {
                    registeredPlacementTypes.Add(placementType);
                }
            }

            for (int floorSeek = 0; floorSeek < building.Floors.Count; floorSeek++)
            {
                BuildingFloor floor = building.Floors[floorSeek];
                if (floor == null)
                {
                    continue;
                }
                BuildingGroupDefinition group = floor.BuildingGroup;
                if (group != null && !registeredGroups.Contains(group))
                {
                    result.AddError($"階{floorSeek}の階層グループは現在の共通設定アセットに登録されていません。");
                }
            }

            var validatedParts = new HashSet<BuildingPartDefinition>();
            for (int listSeek = 0; listSeek < building.AssetLists.Count; listSeek++)
            {
                NatoriCityBuildingAssetList assetList = building.AssetLists[listSeek];
                if (assetList == null)
                {
                    continue;
                }
                for (int partSeek = 0; partSeek < assetList.Parts.Count; partSeek++)
                {
                    BuildingPartDefinition part = assetList.Parts[partSeek];
                    if (part == null || !validatedParts.Add(part))
                    {
                        continue;
                    }
                    ValidatePart(part, registeredGroups, registeredPlacementTypes, result);
                }
            }
        }

        private static void ValidatePart(
            BuildingPartDefinition part,
            HashSet<BuildingGroupDefinition> registeredGroups,
            HashSet<PlacementTypeDefinition> registeredPlacementTypes,
            NatoriCityBuildingValidationResult result)
        {
            if (part.BuildingGroup == null)
            {
                result.AddError($"登録パーツ「{part.Label}」の階層グループがMissingです。");
            }
            else if (!registeredGroups.Contains(part.BuildingGroup))
            {
                result.AddError($"登録パーツ「{part.Label}」の階層グループは現在の共通設定アセットに登録されていません。");
            }
            if (part.PlacementType == null)
            {
                result.AddError($"登録パーツ「{part.Label}」の配置種別がMissingです。");
            }
            else if (!registeredPlacementTypes.Contains(part.PlacementType))
            {
                result.AddError($"登録パーツ「{part.Label}」の配置種別は現在の共通設定アセットに登録されていません。");
            }
            if (part.Footprint.x <= 0 || part.Footprint.y <= 0)
            {
                result.AddError($"登録パーツ「{part.Label}」の占有サイズが0以下です。");
            }
        }
    }
}
