using UnityEngine;

namespace Natori.CityBuilder
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class NatoriCityGeneratedPart : MonoBehaviour
    {
        [SerializeField]
        [HideInInspector]
        private string _floorIdentifier;

        [SerializeField]
        [HideInInspector]
        private string _placementIdentifier;

        [SerializeField]
        [HideInInspector]
        private BuildingPartDefinition _partDefinition;

        [SerializeField]
        [HideInInspector]
        private GameObject _prefabAtGeneration;

        public string FloorIdentifier => _floorIdentifier;
        public string PlacementIdentifier => _placementIdentifier;
        public BuildingPartDefinition PartDefinition => _partDefinition;
        internal GameObject PrefabAtGeneration => _prefabAtGeneration;

        internal void Initialize(
            string floorIdentifier,
            string placementIdentifier,
            BuildingPartDefinition partDefinition)
        {
            _floorIdentifier = floorIdentifier;
            _placementIdentifier = placementIdentifier;
            _partDefinition = partDefinition;
            _prefabAtGeneration = partDefinition.Prefab;
        }
    }
}
