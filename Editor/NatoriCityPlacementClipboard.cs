using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    [Serializable]
    internal sealed class NatoriCityPlacementClipboard
    {
        private const string Prefix = "NatoriCityBuilder.Placements\n";
        [SerializeField] private List<NatoriCityClipboardPlacement> _placements = new();

        public static bool HasContents => EditorGUIUtility.systemCopyBuffer.StartsWith(Prefix, StringComparison.Ordinal);

        public static void Copy(IReadOnlyList<BuildingPartPlacement> placements)
        {
            if (placements.Count == 0)
            {
                return;
            }
            Vector2Int origin = NatoriCityPlacementSelection.GetBounds(placements).min;
            var clipboard = new NatoriCityPlacementClipboard();
            foreach (BuildingPartPlacement placement in placements)
            {
                clipboard._placements.Add(new NatoriCityClipboardPlacement(placement, origin));
            }
            EditorGUIUtility.systemCopyBuffer = Prefix + JsonUtility.ToJson(clipboard);
        }

        public static bool TryRead(Vector2Int origin, out List<BuildingPartPlacement> placements, out string error)
        {
            placements = new List<BuildingPartPlacement>();
            error = "クリップボードにNatori City Builderの配置がありません。";
            if (!HasContents)
            {
                return false;
            }
            NatoriCityPlacementClipboard clipboard;
            try
            {
                //OSのクリップボードは他アプリから書き換え可能なため、壊れたJSONも通常の入力として扱う。
                clipboard = JsonUtility.FromJson<NatoriCityPlacementClipboard>(EditorGUIUtility.systemCopyBuffer.Substring(Prefix.Length));
            }
            catch (ArgumentException)
            {
                error = "クリップボードの配置データを読み取れません。";
                return false;
            }
            if (clipboard == null || clipboard._placements == null || clipboard._placements.Count == 0)
            {
                return false;
            }
            foreach (NatoriCityClipboardPlacement entry in clipboard._placements)
            {
                if (entry == null || !entry.TryCreate(origin, out BuildingPartPlacement placement))
                {
                    error = "コピー元のパーツが見つからないか、コピー後に定義が変更されています。";
                    placements.Clear();
                    return false;
                }
                placements.Add(placement);
            }
            error = null;
            return true;
        }
    }
}
