using System.Collections.Generic;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    public sealed class NatoriCityBuildingEditorState
    {
        private readonly NatoriCityBuildingComponent _building;
        private NatoriCityBuildingModelIndex _modelIndex;
        private NatoriCityGeneratedHierarchyIndex _generatedIndex;
        private NatoriCityBuildingVisibilityIndex _visibilityIndex;
        private NatoriCityBuildingValidationResult _validation;
        private bool _modelInvalidated = true;
        private bool _generatedInvalidated = true;
        private bool _visibilityInvalidated = true;
        private bool _generatedHierarchyRequiresRebuild;
        private int _observedEditorChangeVersion;

        public NatoriCityBuildingComponent Building => _building;
        public NatoriCityBuildingModelIndex ModelIndex
        {
            get
            {
                EnsureModelIndex();
                return _modelIndex;
            }
        }
        public NatoriCityGeneratedHierarchyIndex GeneratedIndex
        {
            get
            {
                EnsureGeneratedIndex();
                return _generatedIndex;
            }
        }
        public NatoriCityBuildingValidationResult Validation
        {
            get
            {
                EnsureModelIndex();
                return _validation;
            }
        }
        public NatoriCityBuildingVisibilityIndex VisibilityIndex
        {
            get
            {
                EnsureVisibilityIndex();
                return _visibilityIndex;
            }
        }
        public bool GeneratedHierarchyRequiresRebuild => _generatedHierarchyRequiresRebuild;

        public NatoriCityBuildingEditorState(NatoriCityBuildingComponent building)
        {
            _building = building;
        }

        public void InvalidateModel()
        {
            _modelInvalidated = true;
        }

        public void InvalidateGeneratedHierarchy()
        {
            _generatedInvalidated = true;
            _visibilityInvalidated = true;
        }

        public void InvalidateVisibility()
        {
            _visibilityInvalidated = true;
        }

        internal void ResetGeneratedHierarchyKnowledge()
        {
            _generatedHierarchyRequiresRebuild = false;
            InvalidateGeneratedHierarchy();
        }

        internal void ObserveGeneratedObjectStructure(UnityEngine.GameObject changedObject)
        {
            if (_generatedInvalidated || _generatedIndex == null)
            {
                return;
            }
            if (!_generatedIndex.IsChangedObjectStructurallyConsistent(changedObject))
            {
                _generatedHierarchyRequiresRebuild = true;
                InvalidateGeneratedHierarchy();
            }
        }

        public void RefreshModel()
        {
            _modelInvalidated = true;
            EnsureModelIndex();
        }

        public void RefreshGeneratedHierarchy()
        {
            InvalidateGeneratedHierarchy();
            EnsureGeneratedIndex();
            _generatedHierarchyRequiresRebuild = false;
        }

        public bool CanPlace(
            BuildingFloor floor,
            BuildingPartDefinition part,
            Vector2Int anchorCell)
        {
            EnsureModelIndex();
            if (!_validation.IsValid
                || floor == null
                || part == null
                || !_modelIndex.IsPartAvailable(part)
                || part.Prefab == null
                || part.BuildingGroup != floor.BuildingGroup
                || part.PlacementType == null
                || part.Footprint.x <= 0
                || part.Footprint.y <= 0
                || !NatoriCityGridGeometry.IsFootprintInsideGrid(
                    anchorCell,
                    part.Footprint,
                    _building.GridSize))
            {
                return false;
            }
            return !part.PlacementType.DisallowSamePartAtSameAnchor
                || !_modelIndex.GetOccupancy(floor).ContainsAnchor(part, anchorCell);
        }

        public void AddPlacement(BuildingFloor floor, BuildingPartPlacement placement)
        {
            ModelIndex.AddPlacement(floor, placement);
            _observedEditorChangeVersion = _building.EditorChangeVersion;
        }

        public void RotatePlacement(BuildingPartPlacement placement, int previousQuarterTurnsClockwise)
        {
            ModelIndex.RotatePlacement(placement, previousQuarterTurnsClockwise);
            _observedEditorChangeVersion = _building.EditorChangeVersion;
        }

        public void RemovePlacement(BuildingPartPlacement placement, int previousQuarterTurnsClockwise)
        {
            ModelIndex.RemovePlacement(placement, previousQuarterTurnsClockwise);
            _observedEditorChangeVersion = _building.EditorChangeVersion;
        }

        private void EnsureModelIndex()
        {
            if (_observedEditorChangeVersion != _building.EditorChangeVersion)
            {
                _modelInvalidated = true;
            }
            if (!_modelInvalidated && _modelIndex != null)
            {
                return;
            }
            _modelIndex = new NatoriCityBuildingModelIndex(_building);
            _validation = NatoriCityBuildingValidator.Validate(_building);
            NatoriCityBuilderDefinitionValidation.ValidateBuilding(_building, _validation);
            _observedEditorChangeVersion = _building.EditorChangeVersion;
            _modelInvalidated = false;
        }

        private void EnsureGeneratedIndex()
        {
            if (!_generatedInvalidated && _generatedIndex != null)
            {
                return;
            }
            _generatedIndex = new NatoriCityGeneratedHierarchyIndex(_building);
            _generatedInvalidated = false;
        }

        private void EnsureVisibilityIndex()
        {
            EnsureGeneratedIndex();
            if (!_visibilityInvalidated && _visibilityIndex != null)
            {
                return;
            }
            _visibilityIndex = new NatoriCityBuildingVisibilityIndex(_generatedIndex);
            _visibilityInvalidated = false;
        }
    }
}
