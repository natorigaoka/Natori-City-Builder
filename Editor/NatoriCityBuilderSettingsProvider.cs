using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    public sealed class NatoriCityBuilderSettingsProvider : SettingsProvider
    {
        private NatoriCityBuilderSettings _settings;
        private SerializedObject _serializedSettings;

        private NatoriCityBuilderSettingsProvider()
            : base("Project/Natori City Builder", SettingsScope.Project)
        {
            label = "Natori City Builder";
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new NatoriCityBuilderSettingsProvider();
        }

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            _settings = NatoriCityBuilderProjectSettings.Settings;
            UpdateSerializedSettings();
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.HelpBox(
                "このプロジェクトで使用する水平セルサイズ、階層グループ、配置種別を保持する設定アセットを1つ登録します。"
                + "パス検索は行わず、この参照だけを正として使用します。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            var selectedSettings = (NatoriCityBuilderSettings)EditorGUILayout.ObjectField(
                "共通設定アセット",
                _settings,
                typeof(NatoriCityBuilderSettings),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                _settings = selectedSettings;
                NatoriCityBuilderProjectSettings.Settings = selectedSettings;
                UpdateSerializedSettings();
            }

            if (_settings == null)
            {
                EditorGUILayout.HelpBox(
                    "共通設定アセットが未設定のため、Asset Listと建物の編集を開始できません。",
                    MessageType.Error);
                if (GUILayout.Button("共通設定アセットを作成"))
                {
                    CreateSettings();
                }
                return;
            }

            _serializedSettings.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                _serializedSettings.FindProperty("_horizontalCellSize"),
                new GUIContent("水平セルサイズ"));
            _serializedSettings.ApplyModifiedProperties();
            if (EditorGUI.EndChangeCheck())
            {
                NatoriCityBuildingEditorStateRegistry.InvalidateAllModels();
            }
            EditorGUILayout.HelpBox(
                "グリッド1セルのX/Z共通サイズです。変更後に建物をRebuildすると、アンカーセルを保ったまま配置間隔が変わります。",
                MessageType.None);

            if (GUILayout.Button("共通設定アセットを選択"))
            {
                Selection.activeObject = _settings;
                EditorGUIUtility.PingObject(_settings);
            }
        }

        private void CreateSettings()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Natori City Builder Settingsを作成",
                "NatoriCityBuilderSettings",
                "asset",
                "保存場所を選択してください。");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            var settings = ScriptableObject.CreateInstance<NatoriCityBuilderSettings>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            _settings = settings;
            NatoriCityBuilderProjectSettings.Settings = settings;
            UpdateSerializedSettings();
            Selection.activeObject = settings;
        }

        private void UpdateSerializedSettings()
        {
            _serializedSettings = _settings == null ? null : new SerializedObject(_settings);
        }
    }
}
