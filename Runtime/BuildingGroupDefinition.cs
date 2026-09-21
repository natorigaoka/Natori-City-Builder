using UnityEngine;

namespace Natori.CityBuilder
{
    public sealed class BuildingGroupDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Inspectorに表示する階層グループ名です。識別には使用されません。")]
        private string _label = "New Building Group";

        [SerializeField]
        [Min(float.Epsilon)]
        [Tooltip("この階層グループを使う階の高さです。Unityのローカル座標単位で指定します。")]
        private float _height = 3.0f;

        public string Label => _label;
        public float Height => _height;
    }
}
