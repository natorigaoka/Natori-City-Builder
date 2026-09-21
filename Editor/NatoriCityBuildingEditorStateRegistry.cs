using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    [InitializeOnLoad]
    public static class NatoriCityBuildingEditorStateRegistry
    {
        private static readonly Dictionary<int, NatoriCityBuildingEditorState> States = new();
        private static readonly List<int> StaleStateIdentifiers = new();

        static NatoriCityBuildingEditorStateRegistry()
        {
            Undo.undoRedoPerformed += InvalidateAll;
            ObjectChangeEvents.changesPublished += OnObjectChangesPublished;
            SceneVisibilityManager.visibilityChanged += InvalidateAllVisibility;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.quitting += Clear;
        }

        public static NatoriCityBuildingEditorState Get(NatoriCityBuildingComponent building)
        {
            int instanceIdentifier = building.GetInstanceID();
            if (!States.TryGetValue(instanceIdentifier, out NatoriCityBuildingEditorState state)
                || state.Building != building)
            {
                state = new NatoriCityBuildingEditorState(building);
                States[instanceIdentifier] = state;
            }
            return state;
        }

        public static void SynchronizeSettings(NatoriCityBuilderSettings settings)
        {
            RemoveDestroyedStates();
            foreach (NatoriCityBuildingEditorState state in States.Values)
            {
                NatoriCityBuildingComponent building = state.Building;
                if (building == null || building.Settings == settings)
                {
                    continue;
                }
                building.SetSettings(settings);
                EditorUtility.SetDirty(building);
                PrefabUtility.RecordPrefabInstancePropertyModifications(building);
                state.InvalidateModel();
            }
            SceneView.RepaintAll();
        }

        public static void InvalidateModel(NatoriCityBuildingComponent building)
        {
            if (building != null
                && States.TryGetValue(
                    building.GetInstanceID(),
                    out NatoriCityBuildingEditorState state))
            {
                state.InvalidateModel();
            }
        }

        public static void InvalidateGeneratedHierarchy(NatoriCityBuildingComponent building)
        {
            if (building != null
                && States.TryGetValue(
                    building.GetInstanceID(),
                    out NatoriCityBuildingEditorState state))
            {
                state.InvalidateGeneratedHierarchy();
            }
        }

        public static void InvalidateAllModels()
        {
            RemoveDestroyedStates();
            foreach (NatoriCityBuildingEditorState state in States.Values)
            {
                state.InvalidateModel();
            }
            SceneView.RepaintAll();
        }

        public static void InvalidateAll()
        {
            RemoveDestroyedStates();
            foreach (NatoriCityBuildingEditorState state in States.Values)
            {
                state.InvalidateModel();
                state.ResetGeneratedHierarchyKnowledge();
            }
            SceneView.RepaintAll();
        }

        private static void InvalidateAllVisibility()
        {
            RemoveDestroyedStates();
            foreach (NatoriCityBuildingEditorState state in States.Values)
            {
                state.InvalidateVisibility();
            }
            SceneView.RepaintAll();
        }

        private static void OnObjectChangesPublished(ref ObjectChangeEventStream changeStream)
        {
            for (int changeSeek = 0; changeSeek < changeStream.length; changeSeek++)
            {
                ObjectChangeKind changeKind = changeStream.GetEventType(changeSeek);
                if (changeKind == ObjectChangeKind.ChangeAssetObjectProperties)
                {
                    changeStream.GetChangeAssetObjectPropertiesEvent(
                        changeSeek,
                        out ChangeAssetObjectPropertiesEventArgs arguments);
                    InvalidateIfDefinitionChanged(arguments.instanceId);
                }
                else if (changeKind == ObjectChangeKind.ChangeGameObjectStructure)
                {
                    changeStream.GetChangeGameObjectStructureEvent(
                        changeSeek,
                        out ChangeGameObjectStructureEventArgs arguments);
                    ObserveGeneratedObjectStructure(arguments.instanceId);
                }
                else if (changeKind == ObjectChangeKind.ChangeGameObjectOrComponentProperties)
                {
                    changeStream.GetChangeGameObjectOrComponentPropertiesEvent(
                        changeSeek,
                        out ChangeGameObjectOrComponentPropertiesEventArgs arguments);
                    ObserveObjectPropertyChange(arguments.instanceId);
                }
            }
        }

        private static void InvalidateIfDefinitionChanged(int instanceIdentifier)
        {
            Object changedObject = EditorUtility.InstanceIDToObject(instanceIdentifier);
            if (changedObject is NatoriCityBuilderSettings
                || changedObject is BuildingGroupDefinition
                || changedObject is PlacementTypeDefinition
                || changedObject is NatoriCityBuildingAssetList
                || changedObject is BuildingPartDefinition)
            {
                InvalidateAllModels();
            }
        }

        private static void ObserveObjectPropertyChange(int instanceIdentifier)
        {
            Object changedObject = EditorUtility.InstanceIDToObject(instanceIdentifier);
            if (changedObject is Component component)
            {
                ObserveGeneratedObjectStructure(component.gameObject);
            }
            else if (changedObject is GameObject gameObject)
            {
                ObserveGeneratedObjectStructure(gameObject);
            }
        }

        private static void ObserveGeneratedObjectStructure(int instanceIdentifier)
        {
            ObserveGeneratedObjectStructure(
                EditorUtility.InstanceIDToObject(instanceIdentifier) as GameObject);
        }

        private static void ObserveGeneratedObjectStructure(GameObject changedObject)
        {
            if (changedObject == null)
            {
                return;
            }
            RemoveDestroyedStates();
            foreach (NatoriCityBuildingEditorState state in States.Values)
            {
                state.ObserveGeneratedObjectStructure(changedObject);
            }
        }

        private static void Clear()
        {
            States.Clear();
            StaleStateIdentifiers.Clear();
        }

        private static void RemoveDestroyedStates()
        {
            StaleStateIdentifiers.Clear();
            foreach (KeyValuePair<int, NatoriCityBuildingEditorState> stateByIdentifier in States)
            {
                if (stateByIdentifier.Value.Building == null)
                {
                    StaleStateIdentifiers.Add(stateByIdentifier.Key);
                }
            }
            for (int identifierSeek = 0;
                identifierSeek < StaleStateIdentifiers.Count;
                identifierSeek++)
            {
                States.Remove(StaleStateIdentifiers[identifierSeek]);
            }
            StaleStateIdentifiers.Clear();
        }
    }
}
