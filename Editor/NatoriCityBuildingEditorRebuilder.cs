using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    public static class NatoriCityBuildingEditorRebuilder
    {
        public static bool Rebuild(NatoriCityBuildingComponent building, string undoName)
        {
            NatoriCityBuilderProjectSettings.SynchronizeBuildingSettings(building);
            NatoriCityBuildingValidationResult validation =
                NatoriCityBuildingValidator.ValidateBeforeGridPruning(building);
            NatoriCityBuilderDefinitionValidation.ValidateBuilding(building, validation);
            LogValidation(building, validation);
            if (!validation.IsValid)
            {
                return false;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObject(building, undoName);
            building.RemovePlacementsOutsideGrid();
            DestroyExistingGeneratedRoots(building);
            NatoriCityGeneratedRoot generatedRoot = CreateGeneratedRoot(building, undoName);
            building.SetGeneratedRootObject(generatedRoot.gameObject);

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
                    CreateGeneratedPart(
                        building,
                        generatedRoot,
                        floor,
                        placement,
                        floorBaseHeight,
                        undoName);
                }
            }

            EditorUtility.SetDirty(building);
            PrefabUtility.RecordPrefabInstancePropertyModifications(building);
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(building);
            state.RefreshModel();
            state.RefreshGeneratedHierarchy();
            Undo.CollapseUndoOperations(undoGroup);
            return true;
        }

        private static void LogValidation(
            NatoriCityBuildingComponent building,
            NatoriCityBuildingValidationResult validation)
        {
            for (int errorSeek = 0; errorSeek < validation.Errors.Count; errorSeek++)
            {
                Debug.LogError($"Natori City Builder: {building.name}: {validation.Errors[errorSeek]}", building);
            }
            for (int warningSeek = 0; warningSeek < validation.Warnings.Count; warningSeek++)
            {
                Debug.LogWarning($"Natori City Builder: {building.name}: {validation.Warnings[warningSeek]}", building);
            }
        }

        private static void DestroyExistingGeneratedRoots(NatoriCityBuildingComponent building)
        {
            var rootsToDestroy = new HashSet<GameObject>();
            GameObject referencedRoot = building.GeneratedRootObject;
            if (IsOwnedByBuilding(building, referencedRoot))
            {
                rootsToDestroy.Add(referencedRoot);
            }

            NatoriCityGeneratedRoot[] markedRoots =
                building.GetComponentsInChildren<NatoriCityGeneratedRoot>(true);
            for (int rootSeek = 0; rootSeek < markedRoots.Length; rootSeek++)
            {
                NatoriCityGeneratedRoot markedRoot = markedRoots[rootSeek];
                if (markedRoot.transform.parent == building.transform)
                {
                    rootsToDestroy.Add(markedRoot.gameObject);
                }
            }

            foreach (GameObject rootToDestroy in rootsToDestroy)
            {
                Undo.DestroyObjectImmediate(rootToDestroy);
            }
            building.SetGeneratedRootObject(null);
        }

        private static bool IsOwnedByBuilding(
            NatoriCityBuildingComponent building,
            GameObject generatedRootObject)
        {
            return generatedRootObject != null
                && generatedRootObject != building.gameObject
                && generatedRootObject.transform.IsChildOf(building.transform);
        }

        private static NatoriCityGeneratedRoot CreateGeneratedRoot(
            NatoriCityBuildingComponent building,
            string undoName)
        {
            var rootObject = new GameObject(NatoriCityGeneratedRoot.GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(rootObject, undoName);
            Undo.SetTransformParent(rootObject.transform, building.transform, undoName);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            return Undo.AddComponent<NatoriCityGeneratedRoot>(rootObject);
        }

        internal static NatoriCityGeneratedPart CreateGeneratedPart(
            NatoriCityBuildingComponent building,
            NatoriCityGeneratedRoot generatedRoot,
            BuildingFloor floor,
            BuildingPartPlacement placement,
            float floorBaseHeight,
            string undoName)
        {
            var wrapper = new GameObject(placement.PartDefinition.Label);
            Undo.RegisterCreatedObjectUndo(wrapper, undoName);
            Undo.SetTransformParent(wrapper.transform, generatedRoot.transform, undoName);
            var marker = Undo.AddComponent<NatoriCityGeneratedPart>(wrapper);
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
            wrapper.transform.localScale = Vector3.one;

            var partInstance = (GameObject)PrefabUtility.InstantiatePrefab(
                placement.PartDefinition.Prefab,
                building.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(partInstance, undoName);
            Undo.SetTransformParent(partInstance.transform, wrapper.transform, undoName);
            partInstance.transform.localPosition = Vector3.zero;
            partInstance.transform.localRotation = Quaternion.identity;
            partInstance.transform.localScale = Vector3.one;
            return marker;
        }
    }
}
