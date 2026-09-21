using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Tests
{
    public sealed class NatoriCityBuilderCoreTests
    {
        [Test]
        public void GridMinimumCentersEvenAndOddDimensionsOnGameObjectOrigin()
        {
            Assert.That(NatoriCityGridGeometry.GetGridMinimum(new Vector2Int(100, 80), 1.0f),
                Is.EqualTo(new Vector2(-50.0f, -40.0f)));
            Assert.That(NatoriCityGridGeometry.GetGridMinimum(new Vector2Int(99, 79), 1.0f),
                Is.EqualTo(new Vector2(-49.5f, -39.5f)));
            Assert.That(NatoriCityGridGeometry.GetGridMinimum(new Vector2Int(10, 8), 2.5f),
                Is.EqualTo(new Vector2(-12.5f, -10.0f)));
        }

        [Test]
        public void RotatedFootprintKeepsMinimumCornerAndMovesToRotatedAreaCenter()
        {
            Vector2Int rotatedFootprint = NatoriCityGridGeometry.GetRotatedFootprint(
                new Vector2Int(2, 3),
                1);
            Assert.That(rotatedFootprint, Is.EqualTo(new Vector2Int(3, 2)));
            Vector3 localPosition = NatoriCityGridGeometry.GetPlacementLocalPosition(
                new Vector2Int(10, 10),
                new Vector2Int(2, 4),
                rotatedFootprint,
                7.0f,
                2.0f);
            Assert.That(localPosition, Is.EqualTo(new Vector3(-3.0f, 7.0f, 0.0f)));
        }

        [Test]
        public void RemovingFloorReindexesAndLowersUpperFloors()
        {
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var firstGroup = ScriptableObject.CreateInstance<BuildingGroupDefinition>();
            var removedGroup = ScriptableObject.CreateInstance<BuildingGroupDefinition>();
            var upperGroup = ScriptableObject.CreateInstance<BuildingGroupDefinition>();
            SetFloat(firstGroup, "_height", 3.0f);
            SetFloat(removedGroup, "_height", 4.0f);
            SetFloat(upperGroup, "_height", 3.0f);
            building.AddFloor(firstGroup);
            building.AddFloor(removedGroup);
            building.AddFloor(upperGroup);

            Assert.That(NatoriCityGridGeometry.GetFloorBaseHeight(building, 2), Is.EqualTo(7.0f));
            building.RemoveFloorAt(1);
            Assert.That(building.Floors.Count, Is.EqualTo(2));
            Assert.That(building.Floors[1].BuildingGroup, Is.SameAs(upperGroup));
            Assert.That(NatoriCityGridGeometry.GetFloorBaseHeight(building, 1), Is.EqualTo(3.0f));

            Object.DestroyImmediate(buildingObject);
            Object.DestroyImmediate(firstGroup);
            Object.DestroyImmediate(removedGroup);
            Object.DestroyImmediate(upperGroup);
        }

        [Test]
        public void ValidationStopsWhenFootprintChangedAfterPlacement()
        {
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var group = ScriptableObject.CreateInstance<BuildingGroupDefinition>();
            var placementType = ScriptableObject.CreateInstance<PlacementTypeDefinition>();
            var part = ScriptableObject.CreateInstance<BuildingPartDefinition>();
            var assetList = ScriptableObject.CreateInstance<NatoriCityBuildingAssetList>();
            var settings = ScriptableObject.CreateInstance<NatoriCityBuilderSettings>();
            SetObject(building, "_settings", settings);
            SetObject(part, "_buildingGroup", group);
            SetObject(part, "_placementType", placementType);
            AddObjectToArray(assetList, "_parts", part);
            AddObjectToArray(building, "_assetLists", assetList);
            building.AddFloor(group);
            building.Floors[0].AddPlacement(new BuildingPartPlacement(part, Vector2Int.zero));

            NatoriCityBuildingValidationResult validResult = NatoriCityBuildingValidator.Validate(building);
            Assert.That(validResult.IsValid, Is.True);
            SetInteger(part, "_footprintWidth", 2);
            NatoriCityBuildingValidationResult invalidResult = NatoriCityBuildingValidator.Validate(building);
            Assert.That(invalidResult.IsValid, Is.False);
            Assert.That(invalidResult.Errors, Has.Some.Contains("占有サイズが配置時から変更"));

            Object.DestroyImmediate(buildingObject);
            Object.DestroyImmediate(group);
            Object.DestroyImmediate(placementType);
            Object.DestroyImmediate(part);
            Object.DestroyImmediate(assetList);
            Object.DestroyImmediate(settings);
        }

        private static void SetFloat(Object target, string propertyName, float value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInteger(Object target, string propertyName, int value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddObjectToArray(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.arraySize++;
            property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
