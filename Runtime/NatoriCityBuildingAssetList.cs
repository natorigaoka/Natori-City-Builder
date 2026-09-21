using System.Collections.Generic;
using UnityEngine;

namespace Natori.CityBuilder
{
    [CreateAssetMenu(fileName = "NatoriCityBuildingAssetList", menuName = "Natori City Builder/Building Asset List")]
    public sealed class NatoriCityBuildingAssetList : ScriptableObject
    {
        [SerializeField]
        [HideInInspector]
        private List<BuildingPartDefinition> _parts = new();

        public IReadOnlyList<BuildingPartDefinition> Parts => _parts;
    }
}
