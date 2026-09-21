using System;
using System.Collections.Generic;

namespace Natori.CityBuilder.Editor
{
    public sealed class NatoriCityGeneratedHierarchyIndex
    {
        private readonly NatoriCityBuildingComponent _building;
        private readonly Dictionary<string, NatoriCityGeneratedPart> _partsByPlacementIdentifier = new();
        private readonly Dictionary<string, List<NatoriCityGeneratedPart>> _partsByFloorIdentifier = new();
        private NatoriCityGeneratedRoot _root;
        private bool _isValid;

        public NatoriCityGeneratedRoot Root => _root;
        public bool IsValid => _isValid;
        public IEnumerable<NatoriCityGeneratedPart> Parts => _partsByPlacementIdentifier.Values;
        public int PartCount => _partsByPlacementIdentifier.Count;

        public NatoriCityGeneratedHierarchyIndex(NatoriCityBuildingComponent building)
        {
            _building = building;
            Build();
        }

        public bool IsStructurallyConsistent()
        {
            return _isValid
                && _root != null
                && _root.transform.parent == _building.transform
                && _root.transform.childCount == _partsByPlacementIdentifier.Count;
        }

        public bool TryGetPart(
            string placementIdentifier,
            out NatoriCityGeneratedPart generatedPart)
        {
            return _partsByPlacementIdentifier.TryGetValue(
                placementIdentifier,
                out generatedPart)
                && generatedPart != null
                && generatedPart.transform.parent == _root.transform;
        }

        internal bool IsChangedObjectStructurallyConsistent(UnityEngine.GameObject changedObject)
        {
            if (!_isValid || changedObject == null || _root == null)
            {
                return false;
            }
            if (changedObject == _root.gameObject)
            {
                return changedObject.GetComponent<NatoriCityGeneratedRoot>() == _root;
            }
            if (changedObject.transform.parent != _root.transform)
            {
                return true;
            }
            NatoriCityGeneratedPart marker = changedObject.GetComponent<NatoriCityGeneratedPart>();
            return marker != null
                && TryGetPart(marker.PlacementIdentifier, out NatoriCityGeneratedPart indexedMarker)
                && indexedMarker == marker;
        }

        public IReadOnlyList<NatoriCityGeneratedPart> GetFloorParts(string floorIdentifier)
        {
            if (_partsByFloorIdentifier.TryGetValue(
                floorIdentifier,
                out List<NatoriCityGeneratedPart> parts))
            {
                return parts;
            }
            return Array.Empty<NatoriCityGeneratedPart>();
        }

        public void Add(NatoriCityGeneratedPart generatedPart)
        {
            _partsByPlacementIdentifier.Add(generatedPart.PlacementIdentifier, generatedPart);
            if (!_partsByFloorIdentifier.TryGetValue(
                generatedPart.FloorIdentifier,
                out List<NatoriCityGeneratedPart> floorParts))
            {
                floorParts = new List<NatoriCityGeneratedPart>();
                _partsByFloorIdentifier.Add(generatedPart.FloorIdentifier, floorParts);
            }
            floorParts.Add(generatedPart);
        }

        public void Remove(NatoriCityGeneratedPart generatedPart)
        {
            _partsByPlacementIdentifier.Remove(generatedPart.PlacementIdentifier);
            List<NatoriCityGeneratedPart> floorParts =
                _partsByFloorIdentifier[generatedPart.FloorIdentifier];
            floorParts.Remove(generatedPart);
            if (floorParts.Count == 0)
            {
                _partsByFloorIdentifier.Remove(generatedPart.FloorIdentifier);
            }
        }

        private void Build()
        {
            if (_building.GeneratedRootObject == null
                || _building.GeneratedRootObject.transform.parent != _building.transform)
            {
                _isValid = false;
                return;
            }
            _root = _building.GeneratedRootObject.GetComponent<NatoriCityGeneratedRoot>();
            if (_root == null)
            {
                _isValid = false;
                return;
            }

            _isValid = true;
            for (int childSeek = 0; childSeek < _root.transform.childCount; childSeek++)
            {
                NatoriCityGeneratedPart generatedPart =
                    _root.transform.GetChild(childSeek).GetComponent<NatoriCityGeneratedPart>();
                if (generatedPart == null
                    || string.IsNullOrEmpty(generatedPart.PlacementIdentifier)
                    || string.IsNullOrEmpty(generatedPart.FloorIdentifier)
                    || _partsByPlacementIdentifier.ContainsKey(generatedPart.PlacementIdentifier))
                {
                    _isValid = false;
                    continue;
                }
                Add(generatedPart);
            }
        }
    }
}
