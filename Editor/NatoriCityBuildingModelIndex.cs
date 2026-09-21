using System;
using System.Collections.Generic;

namespace Natori.CityBuilder.Editor
{
    public sealed class NatoriCityBuildingModelIndex
    {
        private readonly NatoriCityBuildingComponent _building;
        private readonly Dictionary<string, BuildingFloor> _floorsByIdentifier = new();
        private readonly Dictionary<BuildingFloor, int> _floorIndices = new();
        private readonly Dictionary<BuildingFloor, float> _floorBaseHeights = new();
        private readonly Dictionary<string, BuildingPartPlacement> _placementsByIdentifier = new();
        private readonly Dictionary<BuildingPartPlacement, BuildingFloor> _floorsByPlacement = new();
        private readonly Dictionary<BuildingFloor, NatoriCityFloorOccupancyIndex> _occupancyByFloor = new();
        private readonly HashSet<BuildingPartDefinition> _availableParts = new();
        private readonly List<BuildingPartDefinition> _availablePartsInOrder = new();
        private readonly Dictionary<BuildingGroupDefinition, List<PlacementTypeDefinition>>
            _placementTypesByGroup = new();
        private readonly Dictionary<(BuildingGroupDefinition, PlacementTypeDefinition), List<BuildingPartDefinition>>
            _partsByGroupAndType = new();

        public IEnumerable<BuildingPartPlacement> Placements => _placementsByIdentifier.Values;
        public int PlacementCount => _placementsByIdentifier.Count;

        public NatoriCityBuildingModelIndex(NatoriCityBuildingComponent building)
        {
            _building = building;
            Build();
        }

        public bool IsPartAvailable(BuildingPartDefinition part)
        {
            return _availableParts.Contains(part);
        }

        public bool TryGetFloor(string floorIdentifier, out BuildingFloor floor)
        {
            return _floorsByIdentifier.TryGetValue(floorIdentifier, out floor);
        }

        public bool TryGetPlacement(
            string placementIdentifier,
            out BuildingPartPlacement placement)
        {
            return _placementsByIdentifier.TryGetValue(placementIdentifier, out placement);
        }

        public BuildingFloor GetFloor(BuildingPartPlacement placement)
        {
            return _floorsByPlacement[placement];
        }

        public float GetFloorBaseHeight(BuildingFloor floor)
        {
            return _floorBaseHeights[floor];
        }

        public int GetFloorIndex(BuildingFloor floor)
        {
            return _floorIndices[floor];
        }

        public NatoriCityFloorOccupancyIndex GetOccupancy(BuildingFloor floor)
        {
            return _occupancyByFloor[floor];
        }

        public IReadOnlyList<BuildingPartPlacement> GetPlacements(
            BuildingFloor floor,
            UnityEngine.Vector2Int cell)
        {
            return GetOccupancy(floor).GetPlacements(cell);
        }

        public IReadOnlyList<PlacementTypeDefinition> GetPlacementTypes(BuildingGroupDefinition group)
        {
            if (_placementTypesByGroup.TryGetValue(group, out List<PlacementTypeDefinition> types))
            {
                return types;
            }
            return Array.Empty<PlacementTypeDefinition>();
        }

        public IReadOnlyList<BuildingPartDefinition> GetParts(
            BuildingGroupDefinition group,
            PlacementTypeDefinition placementType)
        {
            if (_partsByGroupAndType.TryGetValue(
                (group, placementType),
                out List<BuildingPartDefinition> parts))
            {
                return parts;
            }
            return Array.Empty<BuildingPartDefinition>();
        }

        public void AddPlacement(BuildingFloor floor, BuildingPartPlacement placement)
        {
            _placementsByIdentifier.Add(placement.Identifier, placement);
            _floorsByPlacement.Add(placement, floor);
            GetOccupancy(floor).Add(placement);
        }

        public void RotatePlacement(BuildingPartPlacement placement, int previousQuarterTurnsClockwise)
        {
            BuildingFloor floor = GetFloor(placement);
            NatoriCityFloorOccupancyIndex occupancy = GetOccupancy(floor);
            occupancy.Remove(placement, previousQuarterTurnsClockwise);
            occupancy.Add(placement);
        }

        public void RemovePlacement(BuildingPartPlacement placement, int previousQuarterTurnsClockwise)
        {
            BuildingFloor floor = GetFloor(placement);
            GetOccupancy(floor).Remove(placement, previousQuarterTurnsClockwise);
            _floorsByPlacement.Remove(placement);
            _placementsByIdentifier.Remove(placement.Identifier);
        }

        private void Build()
        {
            CollectAvailableParts();
            BuildPaletteIndex();
            float floorBaseHeight = 0.0f;
            for (int floorSeek = 0; floorSeek < _building.Floors.Count; floorSeek++)
            {
                BuildingFloor floor = _building.Floors[floorSeek];
                if (floor == null || string.IsNullOrEmpty(floor.Identifier))
                {
                    continue;
                }
                if (!_floorsByIdentifier.ContainsKey(floor.Identifier))
                {
                    _floorsByIdentifier.Add(floor.Identifier, floor);
                }
                _floorIndices.Add(floor, floorSeek);
                _floorBaseHeights.Add(floor, floorBaseHeight);
                var occupancy = new NatoriCityFloorOccupancyIndex();
                _occupancyByFloor.Add(floor, occupancy);

                for (int placementSeek = 0; placementSeek < floor.Placements.Count; placementSeek++)
                {
                    BuildingPartPlacement placement = floor.Placements[placementSeek];
                    if (placement == null
                        || string.IsNullOrEmpty(placement.Identifier)
                        || placement.PartDefinition == null)
                    {
                        continue;
                    }
                    if (!_placementsByIdentifier.ContainsKey(placement.Identifier))
                    {
                        _placementsByIdentifier.Add(placement.Identifier, placement);
                    }
                    _floorsByPlacement.Add(placement, floor);
                    occupancy.Add(placement);
                }

                if (floor.BuildingGroup != null)
                {
                    floorBaseHeight += floor.BuildingGroup.Height;
                }
            }
        }

        private void CollectAvailableParts()
        {
            for (int assetListSeek = 0; assetListSeek < _building.AssetLists.Count; assetListSeek++)
            {
                NatoriCityBuildingAssetList assetList = _building.AssetLists[assetListSeek];
                if (assetList == null)
                {
                    continue;
                }
                for (int partSeek = 0; partSeek < assetList.Parts.Count; partSeek++)
                {
                    BuildingPartDefinition part = assetList.Parts[partSeek];
                    if (part != null && _availableParts.Add(part))
                    {
                        _availablePartsInOrder.Add(part);
                    }
                }
            }
        }

        private void BuildPaletteIndex()
        {
            var availableTypesByGroup = new Dictionary<BuildingGroupDefinition, HashSet<PlacementTypeDefinition>>();
            for (int partSeek = 0; partSeek < _availablePartsInOrder.Count; partSeek++)
            {
                BuildingPartDefinition part = _availablePartsInOrder[partSeek];
                if (part.BuildingGroup == null || part.PlacementType == null)
                {
                    continue;
                }
                if (!availableTypesByGroup.TryGetValue(
                    part.BuildingGroup,
                    out HashSet<PlacementTypeDefinition> availableTypes))
                {
                    availableTypes = new HashSet<PlacementTypeDefinition>();
                    availableTypesByGroup.Add(part.BuildingGroup, availableTypes);
                }
                availableTypes.Add(part.PlacementType);

                var partKey = (part.BuildingGroup, part.PlacementType);
                if (!_partsByGroupAndType.TryGetValue(
                    partKey,
                    out List<BuildingPartDefinition> parts))
                {
                    parts = new List<BuildingPartDefinition>();
                    _partsByGroupAndType.Add(partKey, parts);
                }
                parts.Add(part);
            }

            if (_building.Settings == null)
            {
                return;
            }
            for (int groupSeek = 0; groupSeek < _building.Settings.BuildingGroups.Count; groupSeek++)
            {
                BuildingGroupDefinition group = _building.Settings.BuildingGroups[groupSeek];
                if (group == null
                    || !availableTypesByGroup.TryGetValue(
                        group,
                        out HashSet<PlacementTypeDefinition> availableTypes))
                {
                    continue;
                }
                var orderedTypes = new List<PlacementTypeDefinition>();
                for (int typeSeek = 0; typeSeek < _building.Settings.PlacementTypes.Count; typeSeek++)
                {
                    PlacementTypeDefinition type = _building.Settings.PlacementTypes[typeSeek];
                    if (type != null && availableTypes.Contains(type))
                    {
                        orderedTypes.Add(type);
                    }
                }
                _placementTypesByGroup.Add(group, orderedTypes);
            }
        }
    }
}
