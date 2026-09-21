namespace Natori.CityBuilder.Editor
{
    public sealed class NatoriCityBuildingChangeSet
    {
        private readonly NatoriCityBuildingChangeKind _kind;
        private readonly string _placementIdentifier;
        private readonly string _clearedFloorIdentifier;
        private readonly int _firstTransformFloorIndex;

        public NatoriCityBuildingChangeKind Kind => _kind;
        public string PlacementIdentifier => _placementIdentifier;
        public string ClearedFloorIdentifier => _clearedFloorIdentifier;
        public int FirstTransformFloorIndex => _firstTransformFloorIndex;

        private NatoriCityBuildingChangeSet(
            NatoriCityBuildingChangeKind kind,
            string placementIdentifier,
            string clearedFloorIdentifier,
            int firstTransformFloorIndex)
        {
            _kind = kind;
            _placementIdentifier = placementIdentifier;
            _clearedFloorIdentifier = clearedFloorIdentifier;
            _firstTransformFloorIndex = firstTransformFloorIndex;
        }

        public static NatoriCityBuildingChangeSet PlacementAdded(string placementIdentifier)
        {
            return new NatoriCityBuildingChangeSet(
                NatoriCityBuildingChangeKind.PlacementAdded,
                placementIdentifier,
                null,
                -1);
        }

        public static NatoriCityBuildingChangeSet PlacementRotated(string placementIdentifier)
        {
            return new NatoriCityBuildingChangeSet(
                NatoriCityBuildingChangeKind.PlacementRotated,
                placementIdentifier,
                null,
                -1);
        }

        public static NatoriCityBuildingChangeSet PlacementRemoved(string placementIdentifier)
        {
            return new NatoriCityBuildingChangeSet(
                NatoriCityBuildingChangeKind.PlacementRemoved,
                placementIdentifier,
                null,
                -1);
        }

        public static NatoriCityBuildingChangeSet FloorStructureChanged(
            string clearedFloorIdentifier,
            int firstTransformFloorIndex)
        {
            return new NatoriCityBuildingChangeSet(
                NatoriCityBuildingChangeKind.FloorStructureChanged,
                null,
                clearedFloorIdentifier,
                firstTransformFloorIndex);
        }

        public static NatoriCityBuildingChangeSet GridResized()
        {
            return new NatoriCityBuildingChangeSet(
                NatoriCityBuildingChangeKind.GridResized,
                null,
                null,
                0);
        }
    }
}
