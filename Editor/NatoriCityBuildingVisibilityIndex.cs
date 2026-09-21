using System.Collections.Generic;
using UnityEditor;

namespace Natori.CityBuilder.Editor
{
    public sealed class NatoriCityBuildingVisibilityIndex
    {
        private readonly HashSet<string> _hiddenFloorIdentifiers = new();

        public NatoriCityBuildingVisibilityIndex(NatoriCityGeneratedHierarchyIndex generatedIndex)
        {
            foreach (NatoriCityGeneratedPart generatedPart in generatedIndex.Parts)
            {
                if (generatedPart != null
                    && SceneVisibilityManager.instance.IsHidden(generatedPart.gameObject))
                {
                    _hiddenFloorIdentifiers.Add(generatedPart.FloorIdentifier);
                }
            }
        }

        public bool IsFloorHidden(string floorIdentifier)
        {
            return _hiddenFloorIdentifiers.Contains(floorIdentifier);
        }
    }
}
