using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Natori.CityBuilder
{
    [MovedFrom(true, "Natori.CityBuilder", "Natori.CityBuilder", "BuildingGroupDefinitions")]
    public sealed class NatoriCityBuilderSettings : ScriptableObject
    {
        [SerializeField]
        [Min(float.Epsilon)]
        [Tooltip("グリッド1セルのX/Z共通サイズです。Unityのローカル座標単位で指定します。")]
        private float _horizontalCellSize = 1.0f;

        [SerializeField]
        [HideInInspector]
        private List<BuildingGroupDefinition> _buildingGroups = new();

        [SerializeField]
        [HideInInspector]
        private List<PlacementTypeDefinition> _placementTypes = new();

        public float HorizontalCellSize => _horizontalCellSize;
        public IReadOnlyList<BuildingGroupDefinition> BuildingGroups => _buildingGroups;
        public IReadOnlyList<PlacementTypeDefinition> PlacementTypes => _placementTypes;
    }
}
