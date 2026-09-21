using UnityEditor;

namespace Natori.CityBuilder.Editor
{
    public static class NatoriCityBuilderProjectSettings
    {
        public const string ConfigObjectName = "com.natori.city-builder.group-definitions";

        public static NatoriCityBuilderSettings Settings
        {
            get
            {
                EditorBuildSettings.TryGetConfigObject(
                    ConfigObjectName,
                    out NatoriCityBuilderSettings settings);
                return settings;
            }
            set
            {
                if (value == null)
                {
                    EditorBuildSettings.RemoveConfigObject(ConfigObjectName);
                    NatoriCityBuildingEditorStateRegistry.SynchronizeSettings(null);
                    return;
                }
                EditorBuildSettings.AddConfigObject(ConfigObjectName, value, true);
                NatoriCityBuildingEditorStateRegistry.SynchronizeSettings(value);
            }
        }

        public static void SynchronizeBuildingSettings(NatoriCityBuildingComponent building)
        {
            NatoriCityBuilderSettings settings = Settings;
            if (building.Settings == settings)
            {
                return;
            }
            building.SetSettings(settings);
            EditorUtility.SetDirty(building);
            PrefabUtility.RecordPrefabInstancePropertyModifications(building);
            NatoriCityBuildingEditorStateRegistry.InvalidateModel(building);
        }
    }
}
