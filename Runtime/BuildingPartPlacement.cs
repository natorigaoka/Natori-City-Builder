using System;
using UnityEngine;

namespace Natori.CityBuilder
{
    [Serializable]
    public sealed class BuildingPartPlacement
    {
        [SerializeField]
        private string _identifier;

        [SerializeField]
        private BuildingPartDefinition _partDefinition;

        [SerializeField]
        private BuildingGroupDefinition _buildingGroupAtPlacement;

        [SerializeField]
        private PlacementTypeDefinition _placementTypeAtPlacement;

        [SerializeField]
        private Vector2Int _footprintAtPlacement;

        [SerializeField]
        private bool _disallowSamePartAtSameAnchorAtPlacement;

        [SerializeField]
        private Vector2Int _anchorCell;

        [SerializeField]
        private int _quarterTurnsClockwise;

        public string Identifier => _identifier;
        public BuildingPartDefinition PartDefinition => _partDefinition;
        public BuildingGroupDefinition BuildingGroupAtPlacement => _buildingGroupAtPlacement;
        public PlacementTypeDefinition PlacementTypeAtPlacement => _placementTypeAtPlacement;
        public Vector2Int FootprintAtPlacement => _footprintAtPlacement;
        public bool DisallowSamePartAtSameAnchorAtPlacement => _disallowSamePartAtSameAnchorAtPlacement;
        public Vector2Int AnchorCell => _anchorCell;
        public int QuarterTurnsClockwise => _quarterTurnsClockwise;

        internal BuildingPartPlacement(BuildingPartDefinition partDefinition, Vector2Int anchorCell)
        {
            _identifier = Guid.NewGuid().ToString("N");
            _partDefinition = partDefinition;
            _buildingGroupAtPlacement = partDefinition.BuildingGroup;
            _placementTypeAtPlacement = partDefinition.PlacementType;
            _footprintAtPlacement = partDefinition.Footprint;
            _disallowSamePartAtSameAnchorAtPlacement =
                partDefinition.PlacementType.DisallowSamePartAtSameAnchor;
            _anchorCell = anchorCell;
            _quarterTurnsClockwise = 0;
        }

        internal void RotateClockwise()
        {
            _quarterTurnsClockwise = (_quarterTurnsClockwise + 1) % 4;
        }

        //選択編集の候補をモデルから切り離す。複製時だけ新しい配置IDを採番する。
        internal BuildingPartPlacement(BuildingPartPlacement source, bool duplicate)
        {
            _identifier = duplicate ? Guid.NewGuid().ToString("N") : source._identifier;
            _partDefinition = source._partDefinition;
            _buildingGroupAtPlacement = source._buildingGroupAtPlacement;
            _placementTypeAtPlacement = source._placementTypeAtPlacement;
            _footprintAtPlacement = source._footprintAtPlacement;
            _disallowSamePartAtSameAnchorAtPlacement = source._disallowSamePartAtSameAnchorAtPlacement;
            _anchorCell = source._anchorCell;
            _quarterTurnsClockwise = source._quarterTurnsClockwise;
        }

        internal void SetGridTransform(Vector2Int anchorCell, int quarterTurnsClockwise)
        {
            _anchorCell = anchorCell;
            _quarterTurnsClockwise = ((quarterTurnsClockwise % 4) + 4) % 4;
        }

        internal void RotateCounterClockwise()
        {
            _quarterTurnsClockwise = (_quarterTurnsClockwise + 3) % 4;
        }
    }
}
