using System;
using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    [Serializable]
    internal sealed class NatoriCityClipboardPlacement
    {
        [SerializeField] private string _partIdentifier;
        [SerializeField] private string _groupIdentifier;
        [SerializeField] private string _typeIdentifier;
        [SerializeField] private Vector2Int _footprint;
        [SerializeField] private bool _disallowDuplicate;
        [SerializeField] private Vector2Int _offset;
        [SerializeField] private int _quarterTurns;

        public NatoriCityClipboardPlacement(BuildingPartPlacement placement, Vector2Int origin)
        {
            //サブアセットを含む永続IDを保存し、ドメイン再読込後も同じパーツへ解決する。
            _partIdentifier = GlobalObjectId.GetGlobalObjectIdSlow(placement.PartDefinition).ToString();
            _groupIdentifier = GlobalObjectId.GetGlobalObjectIdSlow(placement.BuildingGroupAtPlacement).ToString();
            _typeIdentifier = GlobalObjectId.GetGlobalObjectIdSlow(placement.PlacementTypeAtPlacement).ToString();
            _footprint = placement.FootprintAtPlacement;
            _disallowDuplicate = placement.DisallowSamePartAtSameAnchorAtPlacement;
            _offset = placement.AnchorCell - origin;
            _quarterTurns = placement.QuarterTurnsClockwise;
        }

        public bool TryCreate(Vector2Int origin, out BuildingPartPlacement placement)
        {
            placement = null;
            if (!GlobalObjectId.TryParse(_partIdentifier, out GlobalObjectId identifier))
            {
                return false;
            }
            var part = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(identifier) as BuildingPartDefinition;
            if (part == null || part.BuildingGroup == null || part.PlacementType == null
                || GlobalObjectId.GetGlobalObjectIdSlow(part.BuildingGroup).ToString() != _groupIdentifier
                || GlobalObjectId.GetGlobalObjectIdSlow(part.PlacementType).ToString() != _typeIdentifier
                || part.Footprint != _footprint || _footprint.x <= 0 || _footprint.y <= 0
                || part.PlacementType.DisallowSamePartAtSameAnchor != _disallowDuplicate
                || _quarterTurns < 0 || _quarterTurns > 3)
            {
                return false;
            }
            placement = new BuildingPartPlacement(part, origin + _offset);
            placement.SetGridTransform(origin + _offset, _quarterTurns);
            return true;
        }
    }
}
