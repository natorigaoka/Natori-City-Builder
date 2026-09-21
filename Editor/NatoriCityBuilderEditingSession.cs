using UnityEditor;

namespace Natori.CityBuilder.Editor
{
    public static class NatoriCityBuilderEditingSession
    {
        private static NatoriCityBuildingComponent _building;
        private static string _floorIdentifier;
        private static BuildingPartDefinition _selectedPart;

        public static NatoriCityBuildingComponent Building => _building;
        public static string FloorIdentifier => _floorIdentifier;
        public static BuildingPartDefinition SelectedPart => _selectedPart;
        public static bool IsEditing => _building != null && !string.IsNullOrEmpty(_floorIdentifier);

        public static void Begin(NatoriCityBuildingComponent building, BuildingFloor floor)
        {
            End();
            NatoriCityBuilderProjectSettings.SynchronizeBuildingSettings(building);
            _building = building;
            _floorIdentifier = floor.Identifier;
            _selectedPart = null;
            NatoriCityBuildingVisibility.DisableGeneratedPartPicking(building);
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
            NatoriCityBuilderSceneTool.Begin();
        }

        public static void End()
        {
            NatoriCityBuilderSceneTool.End();
            Selection.selectionChanged -= OnSelectionChanged;
            if (_building != null)
            {
                NatoriCityBuildingVisibility.EnableGeneratedPartPicking(_building);
            }
            _building = null;
            _floorIdentifier = null;
            _selectedPart = null;
        }

        public static void SelectPart(BuildingPartDefinition part)
        {
            _selectedPart = part;
        }

        public static BuildingFloor GetEditingFloor()
        {
            if (_building == null)
            {
                return null;
            }
            NatoriCityBuildingModelIndex modelIndex =
                NatoriCityBuildingEditorStateRegistry.Get(_building).ModelIndex;
            return modelIndex.TryGetFloor(_floorIdentifier, out BuildingFloor floor)
                ? floor
                : null;
        }

        private static void OnSelectionChanged()
        {
            if (_building == null || Selection.activeGameObject == _building.gameObject)
            {
                return;
            }
            End();
        }
    }
}
