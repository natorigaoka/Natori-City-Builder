using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    public static class NatoriCityBuilderSubAssetUtility
    {
        public static T Create<T>(Object owner, string objectName) where T : ScriptableObject
        {
            var child = ScriptableObject.CreateInstance<T>();
            child.name = objectName;
            Undo.RegisterCreatedObjectUndo(child, "Add Natori City Builder Definition");
            AssetDatabase.AddObjectToAsset(child, owner);
            EditorUtility.SetDirty(owner);
            return child;
        }

        public static void Destroy(ScriptableObject child)
        {
            Undo.DestroyObjectImmediate(child);
            AssetDatabase.SaveAssets();
        }

        public static void RemoveReferenceAt(SerializedProperty arrayProperty, int index)
        {
            int previousSize = arrayProperty.arraySize;
            arrayProperty.DeleteArrayElementAtIndex(index);
            if (arrayProperty.arraySize == previousSize)
            {
                arrayProperty.DeleteArrayElementAtIndex(index);
            }
        }
    }
}
