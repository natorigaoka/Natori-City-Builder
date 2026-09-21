using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    [CustomEditor(typeof(NatoriCityBuilderSettings))]
    public sealed class NatoriCityBuilderSettingsEditor : UnityEditor.Editor
    {
        private ReorderableList _buildingGroups;
        private ReorderableList _placementTypes;

        private void OnEnable()
        {
            _buildingGroups = CreateBuildingGroupList(serializedObject.FindProperty("_buildingGroups"));
            _placementTypes = CreatePlacementTypeList(serializedObject.FindProperty("_placementTypes"));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.HelpBox(
                "Labelは表示専用です。階層グループと配置種別の識別には各サブアセットのUnity参照を使用します。",
                MessageType.Info);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_horizontalCellSize"),
                new GUIContent("水平セルサイズ"));
            EditorGUILayout.HelpBox(
                "グリッド1セルのX/Z共通サイズです。変更後に各建物をRebuildすると、アンカーセルを保ったまま配置間隔が変わります。",
                MessageType.None);
            EditorGUILayout.Space();
            _buildingGroups.DoLayoutList();
            EditorGUILayout.Space();
            _placementTypes.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
            if (EditorGUI.EndChangeCheck())
            {
                NatoriCityBuildingEditorStateRegistry.InvalidateAllModels();
            }
        }

        private ReorderableList CreateBuildingGroupList(SerializedProperty property)
        {
            var list = new ReorderableList(serializedObject, property, true, true, true, true);
            list.drawHeaderCallback = rectangle => EditorGUI.LabelField(rectangle, "階層グループ");
            list.elementHeight = EditorGUIUtility.singleLineHeight * 2.0f + 8.0f;
            list.drawElementCallback = (rectangle, index, active, focused) =>
            {
                var definition = property.GetArrayElementAtIndex(index).objectReferenceValue
                    as BuildingGroupDefinition;
                DrawBuildingGroup(rectangle, definition);
            };
            list.onAddCallback = ignored => AddBuildingGroup(property);
            list.onRemoveCallback = ignored => RemoveSubAsset(property, list.index);
            return list;
        }

        private ReorderableList CreatePlacementTypeList(SerializedProperty property)
        {
            var list = new ReorderableList(serializedObject, property, true, true, true, true);
            list.drawHeaderCallback = rectangle => EditorGUI.LabelField(rectangle, "配置種別");
            list.elementHeight = EditorGUIUtility.singleLineHeight * 2.0f + 8.0f;
            list.drawElementCallback = (rectangle, index, active, focused) =>
            {
                var definition = property.GetArrayElementAtIndex(index).objectReferenceValue
                    as PlacementTypeDefinition;
                DrawPlacementType(rectangle, definition);
            };
            list.onAddCallback = ignored => AddPlacementType(property);
            list.onRemoveCallback = ignored => RemoveSubAsset(property, list.index);
            return list;
        }

        private static void DrawBuildingGroup(Rect rectangle, BuildingGroupDefinition definition)
        {
            if (definition == null)
            {
                EditorGUI.HelpBox(rectangle, "Missing Building Group Definition", MessageType.Error);
                return;
            }
            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition.Update();
            rectangle.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(rectangle, serializedDefinition.FindProperty("_label"), new GUIContent("Label"));
            rectangle.y += EditorGUIUtility.singleLineHeight + 4.0f;
            EditorGUI.PropertyField(rectangle, serializedDefinition.FindProperty("_height"), new GUIContent("高さ"));
            serializedDefinition.ApplyModifiedProperties();
        }

        private static void DrawPlacementType(Rect rectangle, PlacementTypeDefinition definition)
        {
            if (definition == null)
            {
                EditorGUI.HelpBox(rectangle, "Missing Placement Type Definition", MessageType.Error);
                return;
            }
            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition.Update();
            rectangle.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(rectangle, serializedDefinition.FindProperty("_label"), new GUIContent("Label"));
            rectangle.y += EditorGUIUtility.singleLineHeight + 4.0f;
            EditorGUI.PropertyField(
                rectangle,
                serializedDefinition.FindProperty("_disallowSamePartAtSameAnchor"),
                new GUIContent("同じ登録パーツの同一アンカー重複を禁止"));
            serializedDefinition.ApplyModifiedProperties();
        }

        private void AddBuildingGroup(SerializedProperty property)
        {
            serializedObject.ApplyModifiedProperties();
            BuildingGroupDefinition definition = NatoriCityBuilderSubAssetUtility.Create<BuildingGroupDefinition>(
                target,
                "Building Group");
            serializedObject.Update();
            property.arraySize++;
            property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = definition;
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        private void AddPlacementType(SerializedProperty property)
        {
            serializedObject.ApplyModifiedProperties();
            PlacementTypeDefinition definition = NatoriCityBuilderSubAssetUtility.Create<PlacementTypeDefinition>(
                target,
                "Placement Type");
            serializedObject.Update();
            property.arraySize++;
            property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = definition;
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        private void RemoveSubAsset(SerializedProperty property, int index)
        {
            if (index < 0 || index >= property.arraySize)
            {
                return;
            }
            var child = property.GetArrayElementAtIndex(index).objectReferenceValue as ScriptableObject;
            NatoriCityBuilderSubAssetUtility.RemoveReferenceAt(property, index);
            serializedObject.ApplyModifiedProperties();
            if (child != null)
            {
                NatoriCityBuilderSubAssetUtility.Destroy(child);
            }
        }
    }
}
