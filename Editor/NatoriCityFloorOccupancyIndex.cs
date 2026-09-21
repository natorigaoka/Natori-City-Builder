using System;
using System.Collections.Generic;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    public sealed class NatoriCityFloorOccupancyIndex
    {
        private readonly Dictionary<Vector2Int, List<BuildingPartPlacement>> _placementsByCell = new();
        private readonly Dictionary<BuildingPartDefinition, Dictionary<Vector2Int, int>>
            _anchorCountsByPart = new();
        private readonly HashSet<Vector2Int> _overlappingCells = new();

        public IEnumerable<Vector2Int> OverlappingCells => _overlappingCells;

        public void Add(BuildingPartPlacement placement)
        {
            Vector2Int rotatedFootprint = NatoriCityGridGeometry.GetRotatedFootprint(
                placement.FootprintAtPlacement,
                placement.QuarterTurnsClockwise);
            for (int xOffset = 0; xOffset < rotatedFootprint.x; xOffset++)
            {
                for (int zOffset = 0; zOffset < rotatedFootprint.y; zOffset++)
                {
                    Vector2Int cell = placement.AnchorCell + new Vector2Int(xOffset, zOffset);
                    if (!_placementsByCell.TryGetValue(cell, out List<BuildingPartPlacement> placements))
                    {
                        placements = new List<BuildingPartPlacement>();
                        _placementsByCell.Add(cell, placements);
                    }
                    placements.Add(placement);
                    if (placements.Count == 2)
                    {
                        _overlappingCells.Add(cell);
                    }
                }
            }

            if (!_anchorCountsByPart.TryGetValue(
                placement.PartDefinition,
                out Dictionary<Vector2Int, int> anchorCounts))
            {
                anchorCounts = new Dictionary<Vector2Int, int>();
                _anchorCountsByPart.Add(placement.PartDefinition, anchorCounts);
            }
            anchorCounts.TryGetValue(placement.AnchorCell, out int anchorCount);
            anchorCounts[placement.AnchorCell] = anchorCount + 1;
        }

        public void Remove(BuildingPartPlacement placement, int quarterTurnsClockwise)
        {
            Vector2Int rotatedFootprint = NatoriCityGridGeometry.GetRotatedFootprint(
                placement.FootprintAtPlacement,
                quarterTurnsClockwise);
            for (int xOffset = 0; xOffset < rotatedFootprint.x; xOffset++)
            {
                for (int zOffset = 0; zOffset < rotatedFootprint.y; zOffset++)
                {
                    Vector2Int cell = placement.AnchorCell + new Vector2Int(xOffset, zOffset);
                    List<BuildingPartPlacement> placements = _placementsByCell[cell];
                    placements.Remove(placement);
                    if (placements.Count == 1)
                    {
                        _overlappingCells.Remove(cell);
                    }
                    if (placements.Count == 0)
                    {
                        _placementsByCell.Remove(cell);
                    }
                }
            }

            Dictionary<Vector2Int, int> anchorCounts =
                _anchorCountsByPart[placement.PartDefinition];
            int anchorCount = anchorCounts[placement.AnchorCell] - 1;
            if (anchorCount == 0)
            {
                anchorCounts.Remove(placement.AnchorCell);
            }
            else
            {
                anchorCounts[placement.AnchorCell] = anchorCount;
            }
            if (anchorCounts.Count == 0)
            {
                _anchorCountsByPart.Remove(placement.PartDefinition);
            }
        }

        public bool ContainsAnchor(BuildingPartDefinition part, Vector2Int anchorCell)
        {
            return _anchorCountsByPart.TryGetValue(
                part,
                out Dictionary<Vector2Int, int> anchorCounts)
                && anchorCounts.ContainsKey(anchorCell);
        }

        public IReadOnlyList<BuildingPartPlacement> GetPlacements(Vector2Int cell)
        {
            if (_placementsByCell.TryGetValue(cell, out List<BuildingPartPlacement> placements))
            {
                return placements;
            }
            return Array.Empty<BuildingPartPlacement>();
        }

        public int GetPlacementCount(Vector2Int cell)
        {
            return _placementsByCell.TryGetValue(cell, out List<BuildingPartPlacement> placements)
                ? placements.Count
                : 0;
        }
    }
}
