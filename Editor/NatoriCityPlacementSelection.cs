using System.Collections.Generic;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    //Undoで配置インスタンスが復元されても、選択は配置IDから現在の階へ解決する。
    internal sealed class NatoriCityPlacementSelection
    {
        private readonly HashSet<string> _identifiers = new();
        private Vector2Int _pivot;

        public int Count => _identifiers.Count;
        public Vector2Int Pivot => _pivot;

        public bool Contains(BuildingPartPlacement placement)
        {
            return _identifiers.Contains(placement.Identifier);
        }

        public void Clear()
        {
            _identifiers.Clear();
        }

        public List<BuildingPartPlacement> Resolve(BuildingFloor floor)
        {
            var placements = new List<BuildingPartPlacement>();
            var survivingIdentifiers = new HashSet<string>();
            foreach (BuildingPartPlacement placement in floor.Placements)
            {
                if (_identifiers.Contains(placement.Identifier))
                {
                    placements.Add(placement);
                    survivingIdentifiers.Add(placement.Identifier);
                }
            }
            _identifiers.IntersectWith(survivingIdentifiers);
            return placements;
        }

        public void Select(BuildingFloor floor, IReadOnlyList<BuildingPartPlacement> placements,
            bool additive, bool toggle)
        {
            if (!additive && !toggle)
            {
                Clear();
            }
            foreach (BuildingPartPlacement placement in placements)
            {
                if (!toggle || !_identifiers.Remove(placement.Identifier))
                {
                    _identifiers.Add(placement.Identifier);
                }
            }
            List<BuildingPartPlacement> selected = Resolve(floor);
            if (selected.Count > 0)
            {
                RectInt bounds = GetBounds(selected);
                //交点を使うため、偶数幅と奇数幅が混在しても90度回転で半セルずれない。
                _pivot = new Vector2Int(bounds.xMin + bounds.width / 2, bounds.yMin + bounds.height / 2);
            }
        }

        public void MovePivot(Vector2Int offset)
        {
            _pivot += offset;
        }

        public static RectInt GetBounds(IReadOnlyList<BuildingPartPlacement> placements)
        {
            Vector2Int minimum = placements[0].AnchorCell;
            Vector2Int maximum = minimum;
            foreach (BuildingPartPlacement placement in placements)
            {
                Vector2Int size = NatoriCityGridGeometry.GetRotatedFootprint(
                    placement.FootprintAtPlacement, placement.QuarterTurnsClockwise);
                minimum = Vector2Int.Min(minimum, placement.AnchorCell);
                maximum = Vector2Int.Max(maximum, placement.AnchorCell + size);
            }
            return new RectInt(minimum, maximum - minimum);
        }

        public static List<BuildingPartPlacement> InRectangle(BuildingFloor floor, Vector2Int first, Vector2Int last)
        {
            Vector2Int minimum = Vector2Int.Min(first, last);
            Vector2Int maximum = Vector2Int.Max(first, last) + Vector2Int.one;
            var result = new List<BuildingPartPlacement>();
            foreach (BuildingPartPlacement placement in floor.Placements)
            {
                Vector2Int size = NatoriCityGridGeometry.GetRotatedFootprint(
                    placement.FootprintAtPlacement, placement.QuarterTurnsClockwise);
                if (placement.AnchorCell.x < maximum.x && placement.AnchorCell.y < maximum.y
                    && placement.AnchorCell.x + size.x > minimum.x && placement.AnchorCell.y + size.y > minimum.y)
                {
                    result.Add(placement);
                }
            }
            return result;
        }
    }
}
