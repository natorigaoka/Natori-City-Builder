using UnityEngine;

namespace Natori.CityBuilder
{
    public sealed class PlacementTypeDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Inspectorの配置パーツ選択タブに表示する名前です。識別には使用されません。")]
        private string _label = "New Placement Type";

        [SerializeField]
        [Tooltip("有効な場合、同じ登録パーツを同じアンカーセルへ複数配置できません。回転は区別しません。")]
        private bool _disallowSamePartAtSameAnchor;

        public string Label => _label;
        public bool DisallowSamePartAtSameAnchor => _disallowSamePartAtSameAnchor;
    }
}
