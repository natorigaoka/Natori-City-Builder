using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    public static class NatoriCityBuildingGeneratedSynchronizer
    {
        public static bool Apply(
            NatoriCityBuildingEditorState state,
            NatoriCityBuildingChangeSet changeSet,
            string undoName)
        {
            if (state.GeneratedHierarchyRequiresRebuild)
            {
                return RebuildBrokenHierarchy(state, undoName);
            }
            switch (changeSet.Kind)
            {
                case NatoriCityBuildingChangeKind.PlacementAdded:
                    return AddPlacement(state, changeSet, undoName);
                case NatoriCityBuildingChangeKind.PlacementRotated:
                    return RotatePlacement(state, changeSet, undoName);
                case NatoriCityBuildingChangeKind.PlacementRemoved:
                    return RemovePlacement(state, changeSet, undoName);
                case NatoriCityBuildingChangeKind.FloorStructureChanged:
                    return SynchronizeFloorStructure(state, changeSet, undoName);
                case NatoriCityBuildingChangeKind.GridResized:
                    return SynchronizeGridToModel(state, undoName);
                default:
                    return false;
            }
        }

        private static bool AddPlacement(
            NatoriCityBuildingEditorState state,
            NatoriCityBuildingChangeSet changeSet,
            string undoName)
        {
            NatoriCityGeneratedHierarchyIndex generatedIndex = state.GeneratedIndex;
            if (!generatedIndex.IsStructurallyConsistent())
            {
                return RebuildBrokenHierarchy(state, undoName);
            }
            if (!state.ModelIndex.TryGetPlacement(
                changeSet.PlacementIdentifier,
                out BuildingPartPlacement placement))
            {
                return false;
            }
            BuildingFloor floor = state.ModelIndex.GetFloor(placement);
            NatoriCityGeneratedPart generatedPart = NatoriCityBuildingEditorRebuilder.CreateGeneratedPart(
                state.Building,
                generatedIndex.Root,
                floor,
                placement,
                state.ModelIndex.GetFloorBaseHeight(floor),
                undoName);
            generatedIndex.Add(generatedPart);
            state.InvalidateVisibility();
            return true;
        }

        private static bool RotatePlacement(
            NatoriCityBuildingEditorState state,
            NatoriCityBuildingChangeSet changeSet,
            string undoName)
        {
            NatoriCityGeneratedHierarchyIndex generatedIndex = state.GeneratedIndex;
            if (!generatedIndex.IsStructurallyConsistent())
            {
                return RebuildBrokenHierarchy(state, undoName);
            }
            BuildingPartPlacement placement =
                state.ModelIndex.TryGetPlacement(changeSet.PlacementIdentifier, out BuildingPartPlacement found)
                    ? found
                    : null;
            if (placement == null)
            {
                return false;
            }
            return SynchronizePlacement(state, placement, undoName);
        }

        private static bool RemovePlacement(
            NatoriCityBuildingEditorState state,
            NatoriCityBuildingChangeSet changeSet,
            string undoName)
        {
            if (state.Building.GeneratedRootObject == null)
            {
                return true;
            }
            NatoriCityGeneratedHierarchyIndex generatedIndex = state.GeneratedIndex;
            if (!generatedIndex.IsStructurallyConsistent())
            {
                return RebuildBrokenHierarchy(state, undoName);
            }
            if (generatedIndex.TryGetPart(
                changeSet.PlacementIdentifier,
                out NatoriCityGeneratedPart generatedPart))
            {
                DestroyGeneratedPart(state, generatedPart);
            }
            return true;
        }

        private static bool SynchronizeFloorStructure(
            NatoriCityBuildingEditorState state,
            NatoriCityBuildingChangeSet changeSet,
            string undoName)
        {
            NatoriCityGeneratedHierarchyIndex generatedIndex = state.GeneratedIndex;
            if (!generatedIndex.IsStructurallyConsistent())
            {
                if (state.ModelIndex.PlacementCount == 0
                    && state.Building.GeneratedRootObject == null)
                {
                    return true;
                }
                return RebuildBrokenHierarchy(state, undoName);
            }

            if (!string.IsNullOrEmpty(changeSet.ClearedFloorIdentifier))
            {
                var removedFloorParts = new List<NatoriCityGeneratedPart>(
                    generatedIndex.GetFloorParts(changeSet.ClearedFloorIdentifier));
                for (int partSeek = 0; partSeek < removedFloorParts.Count; partSeek++)
                {
                    DestroyGeneratedPart(state, removedFloorParts[partSeek]);
                }
            }

            int firstFloorIndex = Mathf.Max(0, changeSet.FirstTransformFloorIndex);
            for (int floorSeek = firstFloorIndex;
                floorSeek < state.Building.Floors.Count;
                floorSeek++)
            {
                BuildingFloor floor = state.Building.Floors[floorSeek];
                for (int placementSeek = 0;
                    placementSeek < floor.Placements.Count;
                    placementSeek++)
                {
                    if (!SynchronizePlacement(
                        state,
                        floor.Placements[placementSeek],
                        undoName))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool SynchronizeGridToModel(
            NatoriCityBuildingEditorState state,
            string undoName)
        {
            NatoriCityGeneratedHierarchyIndex generatedIndex = state.GeneratedIndex;
            if (!generatedIndex.IsStructurallyConsistent())
            {
                if (state.ModelIndex.PlacementCount == 0
                    && state.Building.GeneratedRootObject == null)
                {
                    return true;
                }
                return RebuildBrokenHierarchy(state, undoName);
            }

            var existingParts = new List<NatoriCityGeneratedPart>(generatedIndex.Parts);
            for (int partSeek = 0; partSeek < existingParts.Count; partSeek++)
            {
                NatoriCityGeneratedPart generatedPart = existingParts[partSeek];
                if (state.ModelIndex.TryGetPlacement(
                    generatedPart.PlacementIdentifier,
                    out BuildingPartPlacement placement))
                {
                    if (!SynchronizePlacement(state, placement, undoName))
                    {
                        return false;
                    }
                    continue;
                }
                DestroyGeneratedPart(state, generatedPart);
            }

            foreach (BuildingPartPlacement placement in state.ModelIndex.Placements)
            {
                if (generatedIndex.TryGetPart(placement.Identifier, out _))
                {
                    continue;
                }
                if (!SynchronizePlacement(state, placement, undoName))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool SynchronizePlacement(
            NatoriCityBuildingEditorState state,
            BuildingPartPlacement placement,
            string undoName)
        {
            NatoriCityGeneratedHierarchyIndex generatedIndex = state.GeneratedIndex;
            bool hasGeneratedPart = generatedIndex.TryGetPart(
                placement.Identifier,
                out NatoriCityGeneratedPart generatedPart);
            GameObject currentPrefab = placement.PartDefinition.Prefab;
            if (currentPrefab == null)
            {
                if (hasGeneratedPart)
                {
                    DestroyGeneratedPart(state, generatedPart);
                }
                return true;
            }
            if (hasGeneratedPart
                && generatedPart.PartDefinition == placement.PartDefinition
                && generatedPart.PrefabAtGeneration == currentPrefab)
            {
                UpdateGeneratedPartTransform(state, generatedPart, placement, undoName);
                return true;
            }
            if (hasGeneratedPart)
            {
                DestroyGeneratedPart(state, generatedPart);
            }

            BuildingFloor floor = state.ModelIndex.GetFloor(placement);
            NatoriCityGeneratedPart createdPart = NatoriCityBuildingEditorRebuilder.CreateGeneratedPart(
                state.Building,
                generatedIndex.Root,
                floor,
                placement,
                state.ModelIndex.GetFloorBaseHeight(floor),
                undoName);
            generatedIndex.Add(createdPart);
            state.InvalidateVisibility();
            return true;
        }

        private static void DestroyGeneratedPart(
            NatoriCityBuildingEditorState state,
            NatoriCityGeneratedPart generatedPart)
        {
            state.GeneratedIndex.Remove(generatedPart);
            Undo.DestroyObjectImmediate(generatedPart.gameObject);
            state.InvalidateVisibility();
        }

        private static void UpdateGeneratedPartTransform(
            NatoriCityBuildingEditorState state,
            NatoriCityGeneratedPart generatedPart,
            BuildingPartPlacement placement,
            string undoName)
        {
            BuildingFloor floor = state.ModelIndex.GetFloor(placement);
            Vector2Int rotatedFootprint = NatoriCityGridGeometry.GetRotatedFootprint(
                placement.FootprintAtPlacement,
                placement.QuarterTurnsClockwise);
            Vector3 targetPosition = NatoriCityGridGeometry.GetPlacementLocalPosition(
                state.Building.GridSize,
                placement.AnchorCell,
                rotatedFootprint,
                state.ModelIndex.GetFloorBaseHeight(floor),
                state.Building.Settings.HorizontalCellSize);
            Quaternion targetRotation = Quaternion.Euler(
                0.0f,
                placement.QuarterTurnsClockwise * 90.0f,
                0.0f);
            if (generatedPart.transform.localPosition == targetPosition
                && generatedPart.transform.localRotation == targetRotation)
            {
                return;
            }
            Undo.RecordObject(generatedPart.transform, undoName);
            generatedPart.transform.localPosition = targetPosition;
            generatedPart.transform.localRotation = targetRotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(generatedPart.transform);
        }

        private static bool RebuildBrokenHierarchy(
            NatoriCityBuildingEditorState state,
            string undoName)
        {
            bool rebuilt = NatoriCityBuildingEditorRebuilder.Rebuild(state.Building, undoName);
            if (rebuilt)
            {
                state.RefreshGeneratedHierarchy();
            }
            return rebuilt;
        }
    }
}
