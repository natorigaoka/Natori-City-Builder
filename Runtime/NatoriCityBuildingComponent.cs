using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Natori.CityBuilder
{
    [DisallowMultipleComponent]
    public sealed class NatoriCityBuildingComponent : MonoBehaviour
    {
        [SerializeField]
        [FormerlySerializedAs("_reloadInAwake")]
        [Tooltip("有効な場合、Unityのゲームプレイ開始時にComponentの配置情報から生成オブジェクトをRebuildします。")]
        private bool _rebuildInAwake;

        [SerializeField]
        [FormerlySerializedAs("_reloadWhenEditingBegins")]
        [Tooltip("有効な場合、InspectorのEditを開始するときにComponentの配置情報から生成オブジェクトをRebuildします。")]
        private bool _rebuildWhenEditingBegins = true;

        [SerializeField]
        [FormerlySerializedAs("_definitions")]
        [HideInInspector]
        private NatoriCityBuilderSettings _settings;

        [SerializeField]
        [Tooltip("建物ローカル空間でのグリッド範囲です。GameObject位置がXZ範囲全体の中心になります。")]
        private Vector2Int _gridSize = new(100, 100);

        [SerializeField]
        [Tooltip("この建物で使用できるパーツ一覧です。複数指定した場合は和集合を使用します。")]
        private List<NatoriCityBuildingAssetList> _assetLists = new();

        [SerializeField]
        [HideInInspector]
        private List<BuildingFloor> _floors = new();

        [SerializeField]
        [HideInInspector]
        private GameObject _generatedRootObject;

        [System.NonSerialized]
        private int _editorChangeVersion;

        public bool RebuildInAwake => _rebuildInAwake;
        public bool RebuildWhenEditingBegins => _rebuildWhenEditingBegins;
        public NatoriCityBuilderSettings Settings => _settings;
        public Vector2Int GridSize => _gridSize;
        public IReadOnlyList<NatoriCityBuildingAssetList> AssetLists => _assetLists;
        public IReadOnlyList<BuildingFloor> Floors => _floors;
        internal GameObject GeneratedRootObject => _generatedRootObject;
        internal int EditorChangeVersion => _editorChangeVersion;

        private void Awake()
        {
            if (_rebuildInAwake)
            {
                NatoriCityBuildingRuntimeRebuilder.Rebuild(this);
            }
        }

        private void OnValidate()
        {
            unchecked
            {
                _editorChangeVersion++;
            }
        }

        internal void AddFloor(BuildingGroupDefinition buildingGroup)
        {
            _floors.Add(new BuildingFloor(buildingGroup));
        }

        internal void RemoveFloorAt(int floorIndex)
        {
            _floors.RemoveAt(floorIndex);
        }

        internal int RemovePlacementsOutsideGrid()
        {
            int removedPlacementCount = 0;
            for (int floorSeek = 0; floorSeek < _floors.Count; floorSeek++)
            {
                BuildingFloor floor = _floors[floorSeek];
                for (int placementSeek = floor.Placements.Count - 1; placementSeek >= 0; placementSeek--)
                {
                    BuildingPartPlacement placement = floor.Placements[placementSeek];
                    Vector2Int rotatedFootprint = NatoriCityGridGeometry.GetRotatedFootprint(
                        placement.FootprintAtPlacement,
                        placement.QuarterTurnsClockwise);
                    if (NatoriCityGridGeometry.IsFootprintInsideGrid(
                        placement.AnchorCell,
                        rotatedFootprint,
                        _gridSize))
                    {
                        continue;
                    }
                    floor.RemovePlacement(placement);
                    removedPlacementCount++;
                }
            }
            return removedPlacementCount;
        }

        internal void SetGeneratedRootObject(GameObject generatedRootObject)
        {
            _generatedRootObject = generatedRootObject;
        }

        internal void SetSettings(NatoriCityBuilderSettings settings)
        {
            _settings = settings;
        }
    }
}
