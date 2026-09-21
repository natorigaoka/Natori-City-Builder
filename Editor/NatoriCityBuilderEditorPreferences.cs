using UnityEditor;

namespace Natori.CityBuilder.Editor
{
    public static class NatoriCityBuilderEditorPreferences
    {
        private const string StrokeModeEnabledKey =
            "com.natori.city-builder.stroke-mode-enabled";

        public static bool StrokeModeEnabled
        {
            get => EditorPrefs.GetBool(StrokeModeEnabledKey, false);
            set => EditorPrefs.SetBool(StrokeModeEnabledKey, value);
        }
    }
}
