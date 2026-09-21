using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    public static class NatoriCityBuildingEditorActions
    {
        public static void AddFloor(
            NatoriCityBuildingComponent building,
            BuildingGroupDefinition buildingGroup)
        {
            const string undoName = "Add Natori City Builder Floor";
            int undoGroup = BeginBuildingChange(building, undoName);
            building.AddFloor(buildingGroup);
            FinishBuildingChange(building);
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(building);
            state.RefreshModel();
            if (!state.Validation.IsValid)
            {
                RevertChange(undoGroup);
                return;
            }
            Undo.CollapseUndoOperations(undoGroup);
        }

        public static void RemoveFloor(NatoriCityBuildingComponent building, int floorIndex)
        {
            const string undoName = "Remove Natori City Builder Floor";
            int undoGroup = BeginBuildingChange(building, undoName);
            BuildingFloor removedFloor = building.Floors[floorIndex];
            string removedFloorIdentifier = removedFloor == null ? null : removedFloor.Identifier;
            building.RemoveFloorAt(floorIndex);
            FinishBuildingChange(building);
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(building);
            state.RefreshModel();
            ApplyChangeOrRevert(
                state,
                NatoriCityBuildingChangeSet.FloorStructureChanged(
                    removedFloorIdentifier,
                    floorIndex),
                undoName,
                undoGroup);
        }

        public static void SetFloorBuildingGroup(
            NatoriCityBuildingComponent building,
            BuildingFloor floor,
            BuildingGroupDefinition buildingGroup)
        {
            const string undoName = "Change Natori City Builder Floor Group";
            int undoGroup = BeginBuildingChange(building, undoName);
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(building);
            int floorIndex = state.ModelIndex.GetFloorIndex(floor);
            string clearedFloorIdentifier = floor.Identifier;
            floor.ClearPlacements();
            floor.SetBuildingGroup(buildingGroup);
            FinishBuildingChange(building);
            state.RefreshModel();
            ApplyChangeOrRevert(
                state,
                NatoriCityBuildingChangeSet.FloorStructureChanged(
                    clearedFloorIdentifier,
                    floorIndex + 1),
                undoName,
                undoGroup);
        }

        public static bool AddPlacement(
            NatoriCityBuildingComponent building,
            BuildingFloor floor,
            BuildingPartDefinition part,
            Vector2Int anchorCell)
        {
            if (building == null)
            {
                return false;
            }
            NatoriCityBuilderProjectSettings.SynchronizeBuildingSettings(building);
            if (!CanPlace(building, floor, part, anchorCell))
            {
                return false;
            }
            int undoGroup = BeginBuildingChange(building, "Paint Natori City Builder Parts");
            bool added = AddPlacementToStroke(building, floor, part, anchorCell, undoGroup);
            EndPlacementStroke(undoGroup);
            return added;
        }

        public static int BeginPlacementStroke(NatoriCityBuildingComponent building)
        {
            if (building == null)
            {
                return -1;
            }
            NatoriCityBuilderProjectSettings.SynchronizeBuildingSettings(building);
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(building);
            if (!CanModifyPlacements(state, true))
            {
                return -1;
            }
            return BeginBuildingChange(building, "Paint Natori City Builder Parts");
        }

        public static bool AddPlacementToStroke(
            NatoriCityBuildingComponent building,
            BuildingFloor floor,
            BuildingPartDefinition part,
            Vector2Int anchorCell,
            int undoGroup)
        {
            if (undoGroup < 0 || !CanPlace(building, floor, part, anchorCell))
            {
                return false;
            }
            Undo.RecordObject(building, "Paint Natori City Builder Parts");
            var placement = new BuildingPartPlacement(part, anchorCell);
            floor.AddPlacement(placement);
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(building);
            state.AddPlacement(floor, placement);
            FinishBuildingChange(building);
            if (NatoriCityBuildingGeneratedSynchronizer.Apply(
                state,
                NatoriCityBuildingChangeSet.PlacementAdded(placement.Identifier),
                "Paint Natori City Builder Parts"))
            {
                return true;
            }
            RevertChange(undoGroup);
            return false;
        }

        public static void EndPlacementStroke(int undoGroup)
        {
            if (undoGroup >= 0)
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        public static bool RotatePlacement(
            NatoriCityBuildingComponent building,
            BuildingPartPlacement placement,
            bool clockwise)
        {
            if (building == null || placement == null)
            {
                return false;
            }
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(building);
            if (!CanModifyPlacements(state, true))
            {
                return false;
            }
            var selected = new[] { placement };
            return NatoriCityPlacementBatch.Apply(state, state.ModelIndex.GetFloor(placement), selected,
                NatoriCityPlacementBatch.Transform(selected, Vector2Int.zero, clockwise ? 1 : -1, false, Vector2Int.zero),
                "Rotate Natori City Builder Part", out _);
        }

        public static bool RemovePlacement(
            NatoriCityBuildingComponent building,
            BuildingFloor floor,
            BuildingPartPlacement placement)
        {
            if (building == null
                || floor == null
                || placement == null)
            {
                return false;
            }
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(building);
            if (!CanModifyPlacements(state, true))
            {
                return false;
            }
            return NatoriCityPlacementBatch.Apply(state, floor, new[] { placement },
                System.Array.Empty<BuildingPartPlacement>(), "Remove Natori City Builder Part", out _);
        }

        public static void RebuildAfterGridResize(NatoriCityBuildingComponent building)
        {
            const string undoName = "Resize Natori City Builder Grid";
            int undoGroup = Undo.GetCurrentGroup();
            NatoriCityBuilderProjectSettings.SynchronizeBuildingSettings(building);
            NatoriCityBuildingValidationResult validation =
                NatoriCityBuildingValidator.ValidateBeforeGridPruning(building);
            NatoriCityBuilderDefinitionValidation.ValidateBuilding(building, validation);
            if (!validation.IsValid)
            {
                RevertChange(undoGroup);
                return;
            }
            Undo.RecordObject(building, undoName);
            building.RemovePlacementsOutsideGrid();
            FinishBuildingChange(building);
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(building);
            state.RefreshModel();
            ApplyChangeOrRevert(
                state,
                NatoriCityBuildingChangeSet.GridResized(),
                undoName,
                undoGroup);
        }

        public static bool CanPlace(
            NatoriCityBuildingComponent building,
            BuildingFloor floor,
            BuildingPartDefinition part,
            Vector2Int anchorCell)
        {
            if (building == null)
            {
                return false;
            }
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(building);
            return state.CanPlace(floor, part, anchorCell);
        }

        private static bool CanModifyPlacements(
            NatoriCityBuildingEditorState state,
            bool logError)
        {
            NatoriCityBuildingComponent building = state.Building;
            NatoriCityBuilderProjectSettings.SynchronizeBuildingSettings(building);
            NatoriCityBuildingValidationResult validation = state.Validation;
            if (validation.IsValid)
            {
                return true;
            }
            if (logError)
            {
                Debug.LogError(
                    $"Natori City Builder: 配置を変更できません: {validation.Errors[0]}",
                    building);
            }
            return false;
        }

        private static bool ApplyChangeOrRevert(
            NatoriCityBuildingEditorState state,
            NatoriCityBuildingChangeSet changeSet,
            string undoName,
            int undoGroup)
        {
            if (!state.Validation.IsValid
                || !NatoriCityBuildingGeneratedSynchronizer.Apply(state, changeSet, undoName))
            {
                RevertChange(undoGroup);
                return false;
            }
            Undo.CollapseUndoOperations(undoGroup);
            return true;
        }

        private static void RevertChange(int undoGroup)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            NatoriCityBuildingEditorStateRegistry.InvalidateAll();
        }

        private static int BeginBuildingChange(NatoriCityBuildingComponent building, string undoName)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObject(building, undoName);
            return undoGroup;
        }

        private static void FinishBuildingChange(NatoriCityBuildingComponent building)
        {
            EditorUtility.SetDirty(building);
            PrefabUtility.RecordPrefabInstancePropertyModifications(building);
        }
    }
}
