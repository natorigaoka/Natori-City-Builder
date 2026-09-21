using System;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    //重複規則は占有セルの交差ではなく、同じパーツとアンカーの組に対して適用する。
    internal readonly struct NatoriCityPlacementAnchor : IEquatable<NatoriCityPlacementAnchor>
    {
        private readonly BuildingPartDefinition _part;
        private readonly Vector2Int _cell;

        public NatoriCityPlacementAnchor(BuildingPartPlacement placement)
        {
            _part = placement.PartDefinition;
            _cell = placement.AnchorCell;
        }

        public bool Equals(NatoriCityPlacementAnchor other)
        {
            return _part == other._part && _cell == other._cell;
        }

        public override bool Equals(object other)
        {
            return other is NatoriCityPlacementAnchor anchor && Equals(anchor);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_part, _cell);
        }
    }
}
