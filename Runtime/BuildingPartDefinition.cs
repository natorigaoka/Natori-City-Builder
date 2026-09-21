using UnityEngine;

namespace Natori.CityBuilder
{
    public sealed class BuildingPartDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("配置候補に表示する名前です。識別には使用されません。")]
        private string _label = "New Building Part";

        [SerializeField]
        [Tooltip("配置時に生成するPrefabです。Prefab原点は占有領域のXZ中心、Y=0は足元として扱います。")]
        private GameObject _prefab;

        [SerializeField]
        [Tooltip("このパーツを配置できる階層グループです。")]
        private BuildingGroupDefinition _buildingGroup;

        [SerializeField]
        [Tooltip("Inspectorでの分類と、同一アンカーへの重複規則を定義します。")]
        private PlacementTypeDefinition _placementType;

        [SerializeField]
        [Min(1)]
        [Tooltip("回転0度で+X方向へ占有するセル数です。")]
        private int _footprintWidth = 1;

        [SerializeField]
        [Min(1)]
        [Tooltip("回転0度で+Z方向へ占有するセル数です。")]
        private int _footprintDepth = 1;

        public string Label => _label;
        public GameObject Prefab => _prefab;
        public BuildingGroupDefinition BuildingGroup => _buildingGroup;
        public PlacementTypeDefinition PlacementType => _placementType;
        public Vector2Int Footprint => new(_footprintWidth, _footprintDepth);
    }
}
