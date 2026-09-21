using System.Collections.Generic;
using UnityEngine;

namespace Natori.CityBuilder
{
    public static class NatoriCityBuildingRuntimeRebuilder
    {
        public static bool Rebuild(NatoriCityBuildingComponent building)
        {
            NatoriCityBuildingValidationResult validation =
                NatoriCityBuildingValidator.ValidateBeforeGridPruning(building);
            for (int errorSeek = 0; errorSeek < validation.Errors.Count; errorSeek++)
            {
                Debug.LogError($"Natori City Builder: {building.name}: {validation.Errors[errorSeek]}", building);
            }
            for (int warningSeek = 0; warningSeek < validation.Warnings.Count; warningSeek++)
            {
                Debug.LogWarning($"Natori City Builder: {building.name}: {validation.Warnings[warningSeek]}", building);
            }
            if (!validation.IsValid)
            {
                return false;
            }

            building.RemovePlacementsOutsideGrid();

            DisableAndDestroyExistingRoots(building);
            var generatedRootObject = new GameObject(NatoriCityGeneratedRoot.GeneratedRootName);
            generatedRootObject.transform.SetParent(building.transform, false);
            generatedRootObject.AddComponent<NatoriCityGeneratedRoot>();
            building.SetGeneratedRootObject(generatedRootObject);

            for (int floorSeek = 0; floorSeek < building.Floors.Count; floorSeek++)
            {
                BuildingFloor floor = building.Floors[floorSeek];
                float floorBaseHeight = NatoriCityGridGeometry.GetFloorBaseHeight(building, floorSeek);
                for (int placementSeek = 0; placementSeek < floor.Placements.Count; placementSeek++)
                {
                    BuildingPartPlacement placement = floor.Placements[placementSeek];
                    if (placement.PartDefinition.Prefab == null)
                    {
                        continue;
                    }
                    CreatePlacement(building, generatedRootObject.transform, floor, placement, floorBaseHeight);
                }
            }
            return true;
        }

        private static void DisableAndDestroyExistingRoots(NatoriCityBuildingComponent building)
        {
            var rootsToDestroy = new HashSet<GameObject>();
            GameObject referencedRoot = building.GeneratedRootObject;
            if (referencedRoot != null
                && referencedRoot != building.gameObject
                && referencedRoot.transform.IsChildOf(building.transform))
            {
                rootsToDestroy.Add(referencedRoot);
            }
            NatoriCityGeneratedRoot[] existingRoots =
                building.GetComponentsInChildren<NatoriCityGeneratedRoot>(true);
            for (int rootSeek = 0; rootSeek < existingRoots.Length; rootSeek++)
            {
                NatoriCityGeneratedRoot existingRoot = existingRoots[rootSeek];
                if (existingRoot.transform.parent != building.transform)
                {
                    continue;
                }
                rootsToDestroy.Add(existingRoot.gameObject);
            }
            foreach (GameObject rootToDestroy in rootsToDestroy)
            {
                rootToDestroy.SetActive(false);
                Object.Destroy(rootToDestroy);
            }
            building.SetGeneratedRootObject(null);
        }

        private static void CreatePlacement(
            NatoriCityBuildingComponent building,
            Transform generatedRoot,
            BuildingFloor floor,
            BuildingPartPlacement placement,
            float floorBaseHeight)
        {
            var wrapper = new GameObject(placement.PartDefinition.Label);
            wrapper.transform.SetParent(generatedRoot, false);
            var marker = wrapper.AddComponent<NatoriCityGeneratedPart>();
            marker.Initialize(floor.Identifier, placement.Identifier, placement.PartDefinition);

            Vector2Int rotatedFootprint = NatoriCityGridGeometry.GetRotatedFootprint(
                placement.FootprintAtPlacement,
                placement.QuarterTurnsClockwise);
            wrapper.transform.localPosition = NatoriCityGridGeometry.GetPlacementLocalPosition(
                building.GridSize,
                placement.AnchorCell,
                rotatedFootprint,
                floorBaseHeight,
                building.Settings.HorizontalCellSize);
            wrapper.transform.localRotation = Quaternion.Euler(
                0.0f,
                placement.QuarterTurnsClockwise * 90.0f,
                0.0f);

            GameObject partInstance = Object.Instantiate(placement.PartDefinition.Prefab, wrapper.transform);
            partInstance.name = placement.PartDefinition.Prefab.name;
            partInstance.transform.localPosition = Vector3.zero;
            partInstance.transform.localRotation = Quaternion.identity;
            partInstance.transform.localScale = Vector3.one;
        }
    }
}
