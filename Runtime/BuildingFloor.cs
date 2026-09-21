using System;
using System.Collections.Generic;
using UnityEngine;

namespace Natori.CityBuilder
{
    [Serializable]
    public sealed class BuildingFloor
    {
        [SerializeField]
        private string _identifier;

        [SerializeField]
        private BuildingGroupDefinition _buildingGroup;

        [SerializeField]
        private List<BuildingPartPlacement> _placements = new();

        public string Identifier => _identifier;
        public BuildingGroupDefinition BuildingGroup => _buildingGroup;
        public IReadOnlyList<BuildingPartPlacement> Placements => _placements;

        internal BuildingFloor(BuildingGroupDefinition buildingGroup)
        {
            _identifier = Guid.NewGuid().ToString("N");
            _buildingGroup = buildingGroup;
        }

        internal void SetBuildingGroup(BuildingGroupDefinition buildingGroup)
        {
            _buildingGroup = buildingGroup;
        }

        internal void AddPlacement(BuildingPartPlacement placement)
        {
            _placements.Add(placement);
        }

        internal void RemovePlacement(BuildingPartPlacement placement)
        {
            _placements.Remove(placement);
        }

        internal void ClearPlacements()
        {
            _placements.Clear();
        }
    }
}
