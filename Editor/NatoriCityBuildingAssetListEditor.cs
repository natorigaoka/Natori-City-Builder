using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    [CustomEditor(typeof(NatoriCityBuildingAssetList))]
    public sealed class NatoriCityBuildingAssetListEditor : UnityEditor.Editor
    {
        private ReorderableList _parts;

        private void OnEnable()
        {
            SerializedProperty partsProperty = serializedObject.FindProperty("_parts");
            _parts = new ReorderableList(serializedObject, partsProperty, true, true, true, true);
            _parts.drawHeaderCallback = rectangle => EditorGUI.LabelField(rectangle, "建物パーツ");
            _parts.elementHeight = EditorGUIUtility.singleLineHeight * 6.0f + 24.0f;
            _parts.drawElementCallback = (rectangle, index, active, focused) =>
                DrawPart(rectangle, partsProperty.GetArrayElementAtIndex(index).objectReferenceValue
                    as BuildingPartDefinition);
            _parts.onAddCallback = ignored => AddPart(partsProperty);
            _parts.onRemoveCallback = ignored => RemovePart(partsProperty, _parts.index);
        }

        public override void OnInspectorGUI()
        {
            NatoriCityBuilderSettings settings = NatoriCityBuilderProjectSettings.Settings;
            if (settings == null)
            {
                EditorGUILayout.HelpBox(
                    "Project Settings > Natori City Builderで共通設定アセットを登録してから編集してください。",
                    MessageType.Error);
                if (GUILayout.Button("Project Settingsを開く"))
                {
                    SettingsService.OpenProjectSettings("Project/Natori City Builder");
                }
                return;
            }

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.HelpBox(
                "各行は独立したPart Definitionです。同じPrefabを複数回登録しても別の識別子として扱います。",
                MessageType.Info);
            _parts.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
            if (EditorGUI.EndChangeCheck())
            {
                NatoriCityBuildingEditorStateRegistry.InvalidateAllModels();
            }
        }

        private static void DrawPart(Rect rectangle, BuildingPartDefinition definition)
        {
            if (definition == null)
            {
                EditorGUI.HelpBox(rectangle, "Missing Building Part Definition", MessageType.Error);
                return;
            }
            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition.Update();
            NatoriCityBuilderSettings settings = NatoriCityBuilderProjectSettings.Settings;
            rectangle.height = EditorGUIUtility.singleLineHeight;
            DrawProperty(ref rectangle, serializedDefinition.FindProperty("_label"), "Label");
            DrawProperty(ref rectangle, serializedDefinition.FindProperty("_prefab"), "Prefab");
            DrawDefinitionPopup(
                ref rectangle,
                serializedDefinition.FindProperty("_buildingGroup"),
                settings.BuildingGroups,
                "階層グループ");
            DrawDefinitionPopup(
                ref rectangle,
                serializedDefinition.FindProperty("_placementType"),
                settings.PlacementTypes,
                "配置種別");
            DrawProperty(ref rectangle, serializedDefinition.FindProperty("_footprintWidth"), "+X占有セル数");
            DrawProperty(ref rectangle, serializedDefinition.FindProperty("_footprintDepth"), "+Z占有セル数");
            serializedDefinition.ApplyModifiedProperties();
        }

        private static void DrawProperty(ref Rect rectangle, SerializedProperty property, string label)
        {
            EditorGUI.PropertyField(rectangle, property, new GUIContent(label));
            rectangle.y += EditorGUIUtility.singleLineHeight + 4.0f;
        }

        private static void DrawDefinitionPopup<T>(
            ref Rect rectangle,
            SerializedProperty property,
            IReadOnlyList<T> registeredDefinitions,
            string label) where T : Object
        {
            var availableDefinitions = new List<T>();
            for (int definitionSeek = 0; definitionSeek < registeredDefinitions.Count; definitionSeek++)
            {
                T registeredDefinition = registeredDefinitions[definitionSeek];
                if (registeredDefinition != null)
                {
                    availableDefinitions.Add(registeredDefinition);
                }
            }

            var currentDefinition = property.objectReferenceValue as T;
            var labels = new string[availableDefinitions.Count + 1];
            labels[0] = currentDefinition == null ? "未設定" : "未登録: " + currentDefinition.name;
            int currentIndex = 0;
            for (int definitionSeek = 0; definitionSeek < availableDefinitions.Count; definitionSeek++)
            {
                T availableDefinition = availableDefinitions[definitionSeek];
                labels[definitionSeek + 1] = GetDefinitionLabel(availableDefinition);
                if (availableDefinition == currentDefinition)
                {
                    currentIndex = definitionSeek + 1;
                    labels[0] = "未設定";
                }
            }
            int selectedIndex = EditorGUI.Popup(rectangle, label, currentIndex, labels);
            if (selectedIndex != currentIndex)
            {
                property.objectReferenceValue = selectedIndex == 0
                    ? null
                    : availableDefinitions[selectedIndex - 1];
            }
            rectangle.y += EditorGUIUtility.singleLineHeight + 4.0f;
        }

        private static string GetDefinitionLabel(Object definition)
        {
            if (definition is BuildingGroupDefinition buildingGroup)
            {
                return buildingGroup.Label;
            }
            return ((PlacementTypeDefinition)definition).Label;
        }

        private void AddPart(SerializedProperty partsProperty)
        {
            serializedObject.ApplyModifiedProperties();
            BuildingPartDefinition definition = NatoriCityBuilderSubAssetUtility.Create<BuildingPartDefinition>(
                target,
                "Building Part");
            serializedObject.Update();
            partsProperty.arraySize++;
            partsProperty.GetArrayElementAtIndex(partsProperty.arraySize - 1).objectReferenceValue = definition;
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        private void RemovePart(SerializedProperty partsProperty, int index)
        {
            if (index < 0 || index >= partsProperty.arraySize)
            {
                return;
            }
            var definition = partsProperty.GetArrayElementAtIndex(index).objectReferenceValue
                as BuildingPartDefinition;
            NatoriCityBuilderSubAssetUtility.RemoveReferenceAt(partsProperty, index);
            serializedObject.ApplyModifiedProperties();
            if (definition != null)
            {
                NatoriCityBuilderSubAssetUtility.Destroy(definition);
            }
        }
    }
}
