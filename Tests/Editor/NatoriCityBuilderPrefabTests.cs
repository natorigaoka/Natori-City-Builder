using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Natori.CityBuilder.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Natori.CityBuilder.Tests
{
    public sealed class NatoriCityBuilderPrefabTests
    {
        private const string TestFolder = "Assets/NatoriCityBuilderTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.CreateFolder("Assets", "NatoriCityBuilderTests");
            }
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            NatoriCityBuilderProjectSettings.Settings = null;
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [Test]
        public void RebuildUsesCurrentGlobalHorizontalCellSizeWithoutChangingCellData()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);
            Vector2Int anchorCell = new(49, 49);
            building.Floors[0].AddPlacement(new BuildingPartPlacement(part, anchorCell));

            Assert.That(NatoriCityBuildingEditorRebuilder.Rebuild(
                building,
                "Test Default Cell Size"), Is.True);
            NatoriCityGeneratedPart generatedPart =
                building.GetComponentInChildren<NatoriCityGeneratedPart>(true);
            Assert.That(generatedPart.transform.localPosition,
                Is.EqualTo(new Vector3(-0.5f, 0.0f, -0.5f)));

            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty("_horizontalCellSize").floatValue = 2.0f;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(NatoriCityBuildingEditorRebuilder.Rebuild(
                building,
                "Test Expanded Cell Size"), Is.True);
            generatedPart = building.GetComponentInChildren<NatoriCityGeneratedPart>(true);
            Assert.That(generatedPart.transform.localPosition,
                Is.EqualTo(new Vector3(-1.0f, 0.0f, -1.0f)));
            Assert.That(building.Floors[0].Placements[0].AnchorCell, Is.EqualTo(anchorCell));
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void RebuildOnPrefabInstanceUsesComponentDataAndCreatesStandardOverrides()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            GameObject buildingPrefab = CreateBuildingPrefab(group, assetList, part);

            var instanceRoot = (GameObject)PrefabUtility.InstantiatePrefab(buildingPrefab);
            var instanceBuilding = instanceRoot.GetComponent<NatoriCityBuildingComponent>();
            Assert.That(NatoriCityBuildingEditorRebuilder.Rebuild(
                instanceBuilding,
                "Test Prefab Instance Rebuild"), Is.True);
            Assert.That(instanceBuilding.GetComponentsInChildren<NatoriCityGeneratedPart>(true).Length,
                Is.EqualTo(1));

            Undo.RecordObject(instanceBuilding, "Add Instance Placement");
            instanceBuilding.Floors[0].AddPlacement(new BuildingPartPlacement(part, new Vector2Int(1, 0)));
            PrefabUtility.RecordPrefabInstancePropertyModifications(instanceBuilding);
            Assert.That(NatoriCityBuildingEditorRebuilder.Rebuild(
                instanceBuilding,
                "Test Add Instance Placement"), Is.True);
            NatoriCityGeneratedPart[] generatedParts =
                instanceBuilding.GetComponentsInChildren<NatoriCityGeneratedPart>(true);
            Assert.That(generatedParts.Length, Is.EqualTo(2));
            NatoriCityGeneratedRoot generatedRoot =
                instanceBuilding.GetComponentInChildren<NatoriCityGeneratedRoot>(true);
            Assert.That(PrefabUtility.IsAddedGameObjectOverride(generatedRoot.gameObject), Is.True);

            BuildingPartPlacement inheritedPlacement = instanceBuilding.Floors[0].Placements[0];
            Undo.RecordObject(instanceBuilding, "Remove Inherited Placement");
            instanceBuilding.Floors[0].RemovePlacement(inheritedPlacement);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instanceBuilding);
            Assert.That(NatoriCityBuildingEditorRebuilder.Rebuild(
                instanceBuilding,
                "Test Remove Inherited Placement"), Is.True);
            Assert.That(instanceBuilding.GetComponentsInChildren<NatoriCityGeneratedPart>(true).Length,
                Is.EqualTo(1));
            Assert.That(PrefabUtility.GetRemovedGameObjects(instanceRoot).Count, Is.GreaterThanOrEqualTo(1));

            Object.DestroyImmediate(instanceRoot);
        }

        [Test]
        public void RotationUpdatesOnlyTargetGeneratedWrapperAndPreservesUndo()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            var serializedPart = new SerializedObject(part);
            serializedPart.FindProperty("_footprintWidth").intValue = 2;
            serializedPart.ApplyModifiedPropertiesWithoutUndo();
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building,
                building.Floors[0],
                part,
                new Vector2Int(10, 10)), Is.True);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building,
                building.Floors[0],
                part,
                new Vector2Int(20, 20)), Is.True);
            BuildingPartPlacement rotatedPlacement = building.Floors[0].Placements[0];
            BuildingPartPlacement retainedPlacement = building.Floors[0].Placements[1];
            GameObject generatedRoot = building.GeneratedRootObject;
            NatoriCityGeneratedPart rotatedGeneratedPart = FindGeneratedPart(
                building,
                rotatedPlacement.Identifier);
            NatoriCityGeneratedPart retainedGeneratedPart = FindGeneratedPart(
                building,
                retainedPlacement.Identifier);
            GameObject retainedPrefabInstance = retainedGeneratedPart.transform.GetChild(0).gameObject;

            Assert.That(NatoriCityBuildingEditorActions.RotatePlacement(
                building,
                rotatedPlacement,
                true), Is.True);

            Assert.That(building.GeneratedRootObject, Is.SameAs(generatedRoot));
            Assert.That(FindGeneratedPart(building, rotatedPlacement.Identifier),
                Is.SameAs(rotatedGeneratedPart));
            Assert.That(FindGeneratedPart(building, retainedPlacement.Identifier),
                Is.SameAs(retainedGeneratedPart));
            Assert.That(retainedGeneratedPart.transform.GetChild(0).gameObject,
                Is.SameAs(retainedPrefabInstance));
            Assert.That(rotatedPlacement.QuarterTurnsClockwise, Is.EqualTo(1));
            Assert.That(Quaternion.Angle(
                rotatedGeneratedPart.transform.localRotation,
                Quaternion.Euler(0.0f, 90.0f, 0.0f)), Is.LessThan(0.001f));

            Undo.PerformUndo();
            Assert.That(building.Floors[0].Placements[0].QuarterTurnsClockwise, Is.EqualTo(0));
            Assert.That(building.GeneratedRootObject, Is.SameAs(generatedRoot));
            Undo.PerformRedo();
            Assert.That(building.Floors[0].Placements[0].QuarterTurnsClockwise, Is.EqualTo(1));
            Assert.That(building.GeneratedRootObject, Is.SameAs(generatedRoot));
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void RemovalDeletesOnlyTargetGeneratedWrapper()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building, building.Floors[0], part, Vector2Int.zero), Is.True);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building, building.Floors[0], part, Vector2Int.one), Is.True);
            BuildingPartPlacement removedPlacement = building.Floors[0].Placements[0];
            BuildingPartPlacement retainedPlacement = building.Floors[0].Placements[1];
            GameObject generatedRoot = building.GeneratedRootObject;
            GameObject removedWrapper = FindGeneratedPart(
                building,
                removedPlacement.Identifier).gameObject;
            NatoriCityGeneratedPart retainedGeneratedPart = FindGeneratedPart(
                building,
                retainedPlacement.Identifier);

            Assert.That(NatoriCityBuildingEditorActions.RemovePlacement(
                building,
                building.Floors[0],
                removedPlacement), Is.True);

            Assert.That(building.GeneratedRootObject, Is.SameAs(generatedRoot));
            Assert.That(removedWrapper == null, Is.True);
            Assert.That(FindGeneratedPart(building, retainedPlacement.Identifier),
                Is.SameAs(retainedGeneratedPart));
            Assert.That(building.Floors[0].Placements.Count, Is.EqualTo(1));
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void FloorRemovalMovesUpperGeneratedWrapperWithoutReinstantiation()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);
            building.AddFloor(group);
            building.Floors[0].AddPlacement(new BuildingPartPlacement(part, Vector2Int.zero));
            building.Floors[1].AddPlacement(new BuildingPartPlacement(part, Vector2Int.one));
            Assert.That(NatoriCityBuildingEditorRebuilder.Rebuild(
                building,
                "Test Floor Setup"), Is.True);
            string upperPlacementIdentifier = building.Floors[1].Placements[0].Identifier;
            NatoriCityGeneratedPart upperGeneratedPart = FindGeneratedPart(
                building,
                upperPlacementIdentifier);
            GameObject upperPrefabInstance = upperGeneratedPart.transform.GetChild(0).gameObject;
            GameObject generatedRoot = building.GeneratedRootObject;
            Assert.That(upperGeneratedPart.transform.localPosition.y, Is.EqualTo(group.Height));

            NatoriCityBuildingEditorActions.RemoveFloor(building, 0);

            Assert.That(building.GeneratedRootObject, Is.SameAs(generatedRoot));
            Assert.That(FindGeneratedPart(building, upperPlacementIdentifier),
                Is.SameAs(upperGeneratedPart));
            Assert.That(upperGeneratedPart.transform.GetChild(0).gameObject,
                Is.SameAs(upperPrefabInstance));
            Assert.That(upperGeneratedPart.transform.localPosition.y, Is.EqualTo(0.0f));
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void BrokenGeneratedHierarchyTriggersFullRebuildOnlyWhenNeeded()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building, building.Floors[0], part, Vector2Int.zero), Is.True);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building, building.Floors[0], part, Vector2Int.one), Is.True);
            GameObject originalRoot = building.GeneratedRootObject;
            Object.DestroyImmediate(originalRoot.transform.GetChild(0).gameObject);

            Assert.That(NatoriCityBuildingEditorActions.RotatePlacement(
                building,
                building.Floors[0].Placements[1],
                true), Is.True);

            Assert.That(building.GeneratedRootObject, Is.Not.SameAs(originalRoot));
            Assert.That(originalRoot == null, Is.True);
            Assert.That(building.GetComponentsInChildren<NatoriCityGeneratedPart>(true).Length,
                Is.EqualTo(2));
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void GridResizeKeepsUnaffectedGeneratedWrapperAndRemovesOnlyOutsidePlacement()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building, building.Floors[0], part, Vector2Int.zero), Is.True);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building, building.Floors[0], part, new Vector2Int(99, 99)), Is.True);
            BuildingPartPlacement retainedPlacement = building.Floors[0].Placements[0];
            BuildingPartPlacement removedPlacement = building.Floors[0].Placements[1];
            NatoriCityGeneratedPart retainedGeneratedPart = FindGeneratedPart(
                building,
                retainedPlacement.Identifier);
            GameObject retainedPrefabInstance = retainedGeneratedPart.transform.GetChild(0).gameObject;
            GameObject originalRoot = building.GeneratedRootObject;

            serializedBuilding.Update();
            serializedBuilding.FindProperty("_gridSize").vector2IntValue = new Vector2Int(10, 10);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            NatoriCityBuildingEditorActions.RebuildAfterGridResize(building);

            Assert.That(building.GeneratedRootObject, Is.SameAs(originalRoot));
            Assert.That(FindGeneratedPart(building, retainedPlacement.Identifier),
                Is.SameAs(retainedGeneratedPart));
            Assert.That(retainedGeneratedPart.transform.GetChild(0).gameObject,
                Is.SameAs(retainedPrefabInstance));
            Assert.That(FindGeneratedPart(building, removedPlacement.Identifier), Is.Null);
            Assert.That(building.Floors[0].Placements.Count, Is.EqualTo(1));
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void EditorStateReusesIndexesUntilSerializedBuildingDataChanges()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(building);
            NatoriCityBuildingModelIndex initialIndex = state.ModelIndex;
            NatoriCityBuildingValidationResult initialValidation = state.Validation;

            Assert.That(state.ModelIndex, Is.SameAs(initialIndex));
            Assert.That(state.Validation, Is.SameAs(initialValidation));

            serializedBuilding.Update();
            serializedBuilding.FindProperty("_gridSize").vector2IntValue = new Vector2Int(20, 20);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(state.ModelIndex, Is.Not.SameAs(initialIndex));
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void RotationOnPrefabInstanceUsesPartialUpdateAndRecordsOverrides()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            GameObject buildingPrefab = CreateBuildingPrefab(group, assetList, part);
            var instanceRoot = (GameObject)PrefabUtility.InstantiatePrefab(buildingPrefab);
            var instanceBuilding = instanceRoot.GetComponent<NatoriCityBuildingComponent>();
            BuildingPartPlacement placement = instanceBuilding.Floors[0].Placements[0];
            NatoriCityGeneratedPart generatedPart = FindGeneratedPart(
                instanceBuilding,
                placement.Identifier);
            GameObject originalRoot = instanceBuilding.GeneratedRootObject;

            Assert.That(NatoriCityBuildingEditorActions.RotatePlacement(
                instanceBuilding,
                placement,
                true), Is.True);

            Assert.That(instanceBuilding.GeneratedRootObject, Is.SameAs(originalRoot));
            Assert.That(FindGeneratedPart(instanceBuilding, placement.Identifier),
                Is.SameAs(generatedPart));
            Assert.That(Quaternion.Angle(
                generatedPart.transform.localRotation,
                Quaternion.Euler(0.0f, 90.0f, 0.0f)), Is.LessThan(0.001f));
            Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(instanceRoot, false), Is.True);
            Object.DestroyImmediate(instanceRoot);
        }

        [Test]
        public void PrefabReplacementDuringRotationRecreatesOnlyTargetWrapper()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject originalPrefab = CreatePartPrefab();
            GameObject replacementPrefab = CreatePartPrefab("ReplacementPart", PrimitiveType.Sphere);
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, originalPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building, building.Floors[0], part, Vector2Int.zero), Is.True);
            BuildingPartPlacement placement = building.Floors[0].Placements[0];
            NatoriCityGeneratedPart originalGeneratedPart = FindGeneratedPart(
                building,
                placement.Identifier);
            GameObject originalRoot = building.GeneratedRootObject;
            var serializedPart = new SerializedObject(part);
            serializedPart.FindProperty("_prefab").objectReferenceValue = replacementPrefab;
            serializedPart.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(NatoriCityBuildingEditorActions.RotatePlacement(
                building,
                placement,
                true), Is.True);

            NatoriCityGeneratedPart replacementGeneratedPart = FindGeneratedPart(
                building,
                placement.Identifier);
            Assert.That(building.GeneratedRootObject, Is.SameAs(originalRoot));
            Assert.That(originalGeneratedPart == null, Is.True);
            Assert.That(replacementGeneratedPart, Is.Not.Null);
            Assert.That(replacementGeneratedPart.PrefabAtGeneration, Is.SameAs(replacementPrefab));
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void RemovingPlacementAfterPrefabBecomesMissingAlsoRemovesGeneratedWrapper()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building, building.Floors[0], part, Vector2Int.zero), Is.True);
            BuildingPartPlacement placement = building.Floors[0].Placements[0];
            GameObject generatedWrapper = FindGeneratedPart(
                building,
                placement.Identifier).gameObject;
            var serializedPart = new SerializedObject(part);
            serializedPart.FindProperty("_prefab").objectReferenceValue = null;
            serializedPart.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(NatoriCityBuildingEditorActions.RemovePlacement(
                building,
                building.Floors[0],
                placement), Is.True);

            Assert.That(generatedWrapper == null, Is.True);
            Assert.That(building.Floors[0].Placements.Count, Is.EqualTo(0));
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void EmptyTopFloorRemovalDoesNotTouchLowerGeneratedTransform()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);
            building.AddFloor(group);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building, building.Floors[0], part, Vector2Int.zero), Is.True);
            NatoriCityGeneratedPart lowerGeneratedPart = FindGeneratedPart(
                building,
                building.Floors[0].Placements[0].Identifier);
            lowerGeneratedPart.transform.localPosition += new Vector3(0.25f, 0.0f, 0.0f);
            Vector3 retainedPosition = lowerGeneratedPart.transform.localPosition;

            NatoriCityBuildingEditorActions.RemoveFloor(building, 1);

            Assert.That(lowerGeneratedPart.transform.localPosition, Is.EqualTo(retainedPosition));
            Object.DestroyImmediate(buildingObject);
        }

        [UnityTest]
        public IEnumerator MarkerOnlyCorruptionIsInvalidatedAndFallsBackToFullRebuild()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building, building.Floors[0], part, Vector2Int.zero), Is.True);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building, building.Floors[0], part, Vector2Int.one), Is.True);
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(building);
            _ = state.GeneratedIndex;
            NatoriCityGeneratedPart brokenMarker = FindGeneratedPart(
                building,
                building.Floors[0].Placements[0].Identifier);
            GameObject originalRoot = building.GeneratedRootObject;
            Undo.DestroyObjectImmediate(brokenMarker);
            yield return null;

            Assert.That(NatoriCityBuildingEditorActions.RotatePlacement(
                building,
                building.Floors[0].Placements[1],
                true), Is.True);

            Assert.That(building.GeneratedRootObject, Is.Not.SameAs(originalRoot));
            Assert.That(building.GetComponentsInChildren<NatoriCityGeneratedPart>(true).Length,
                Is.EqualTo(2));
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void RemovingInheritedPlacementOnPrefabInstanceUsesPartialUpdate()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            GameObject buildingPrefab = CreateBuildingPrefab(group, assetList, part);
            var instanceRoot = (GameObject)PrefabUtility.InstantiatePrefab(buildingPrefab);
            var instanceBuilding = instanceRoot.GetComponent<NatoriCityBuildingComponent>();
            BuildingPartPlacement placement = instanceBuilding.Floors[0].Placements[0];
            GameObject originalRoot = instanceBuilding.GeneratedRootObject;

            Assert.That(NatoriCityBuildingEditorActions.RemovePlacement(
                instanceBuilding,
                instanceBuilding.Floors[0],
                placement), Is.True);

            Assert.That(instanceBuilding.GeneratedRootObject, Is.SameAs(originalRoot));
            Assert.That(instanceBuilding.GetComponentsInChildren<NatoriCityGeneratedPart>(true).Length,
                Is.EqualTo(0));
            Assert.That(PrefabUtility.GetRemovedGameObjects(instanceRoot).Count,
                Is.GreaterThanOrEqualTo(1));
            Object.DestroyImmediate(instanceRoot);
        }

        [Test]
        public void PlacementUndoRestoresComponentDataAndGeneratedHierarchyTogether()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);

            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building,
                building.Floors[0],
                part,
                Vector2Int.zero), Is.True);
            Assert.That(building.Floors[0].Placements.Count, Is.EqualTo(1));
            Assert.That(building.GetComponentsInChildren<NatoriCityGeneratedPart>(true).Length,
                Is.EqualTo(1));

            Undo.PerformUndo();

            Assert.That(building.Floors[0].Placements.Count, Is.EqualTo(0));
            Assert.That(building.GetComponentsInChildren<NatoriCityGeneratedPart>(true).Length,
                Is.EqualTo(0));
            Undo.PerformRedo();
            Assert.That(building.Floors[0].Placements.Count, Is.EqualTo(1));
            Assert.That(building.GetComponentsInChildren<NatoriCityGeneratedPart>(true).Length,
                Is.EqualTo(1));
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void SceneCallbackRegistrationFollowsEditingSessionLifecycle()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            building.AddFloor(group);

            NatoriCityBuilderEditingSession.Begin(building, building.Floors[0]);

            Assert.That(NatoriCityBuilderEditingSession.IsEditing, Is.True);
            Assert.That(NatoriCityBuilderSceneTool.IsRegistered, Is.True);
            NatoriCityBuilderEditingSession.End();
            Assert.That(NatoriCityBuilderEditingSession.IsEditing, Is.False);
            Assert.That(NatoriCityBuilderSceneTool.IsRegistered, Is.False);
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void PlacementStrokeUndoRestoresAllPaintedCellsTogether()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);

            int undoGroup = NatoriCityBuildingEditorActions.BeginPlacementStroke(building);
            Assert.That(undoGroup, Is.GreaterThanOrEqualTo(0));
            Assert.That(NatoriCityBuildingEditorActions.AddPlacementToStroke(
                building,
                building.Floors[0],
                part,
                Vector2Int.zero,
                undoGroup), Is.True);
            GameObject generatedRootAfterFirstPlacement = building.GeneratedRootObject;
            Assert.That(NatoriCityBuildingEditorActions.AddPlacementToStroke(
                building,
                building.Floors[0],
                part,
                new Vector2Int(1, 0),
                undoGroup), Is.True);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacementToStroke(
                building,
                building.Floors[0],
                part,
                new Vector2Int(2, 0),
                undoGroup), Is.True);
            NatoriCityBuildingEditorActions.EndPlacementStroke(undoGroup);

            Assert.That(building.GeneratedRootObject, Is.SameAs(generatedRootAfterFirstPlacement));
            Assert.That(building.Floors[0].Placements.Count, Is.EqualTo(3));
            Assert.That(building.GetComponentsInChildren<NatoriCityGeneratedPart>(true).Length,
                Is.EqualTo(3));
            Undo.PerformUndo();
            Assert.That(building.Floors[0].Placements.Count, Is.EqualTo(0));
            Assert.That(building.GetComponentsInChildren<NatoriCityGeneratedPart>(true).Length,
                Is.EqualTo(0));
            Undo.PerformRedo();
            Assert.That(building.Floors[0].Placements.Count, Is.EqualTo(3));
            Assert.That(building.GetComponentsInChildren<NatoriCityGeneratedPart>(true).Length,
                Is.EqualTo(3));
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void RebuildRemovesUnknownGeneratedChildrenAndGridShrinkPrunesPlacements()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building,
                building.Floors[0],
                part,
                new Vector2Int(99, 99)), Is.True);

            NatoriCityGeneratedRoot generatedRoot =
                building.GetComponentInChildren<NatoriCityGeneratedRoot>(true);
            GameObject originalGeneratedRootObject = generatedRoot.gameObject;
            var unknownChild = new GameObject("Unknown Generated Child");
            unknownChild.transform.SetParent(generatedRoot.transform, false);
            Transform generatedPrefabInstance = generatedRoot.transform.GetChild(0).GetChild(0);
            generatedPrefabInstance.gameObject.AddComponent<Light>();
            generatedRoot.gameObject.SetActive(false);
            Object.DestroyImmediate(generatedRoot);
            Assert.That(NatoriCityBuildingEditorRebuilder.Rebuild(
                building,
                "Test Generated Cleanup"), Is.True);
            Assert.That(originalGeneratedRootObject == null, Is.True);
            Assert.That(unknownChild == null, Is.True);
            NatoriCityGeneratedRoot rebuiltRoot =
                building.GetComponentInChildren<NatoriCityGeneratedRoot>(true);
            Assert.That(rebuiltRoot.gameObject.activeSelf, Is.True);
            Assert.That(rebuiltRoot.transform.GetChild(0).GetChild(0).GetComponent<Light>(), Is.Null);

            serializedBuilding.Update();
            serializedBuilding.FindProperty("_gridSize").vector2IntValue = new Vector2Int(10, 10);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            NatoriCityBuildingEditorActions.RebuildAfterGridResize(building);

            Assert.That(building.Floors[0].Placements.Count, Is.EqualTo(0));
            Assert.That(building.GetComponentsInChildren<NatoriCityGeneratedPart>(true).Length,
                Is.EqualTo(0));
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void RemovedAssetListPreventsPlacementMutationBeforeRebuild()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            PlacementTypeDefinition placementType = settings.PlacementTypes[0];
            GameObject partPrefab = CreatePartPrefab();
            NatoriCityBuildingAssetList assetList = CreateAssetList(group, placementType, partPrefab);
            BuildingPartDefinition part = assetList.Parts[0];
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building,
                building.Floors[0],
                part,
                Vector2Int.zero), Is.True);

            serializedBuilding.Update();
            serializedBuilding.FindProperty("_assetLists").arraySize = 0;
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(NatoriCityBuildingEditorActions.AddPlacement(
                building,
                building.Floors[0],
                part,
                new Vector2Int(1, 0)), Is.False);
            BuildingPartPlacement retainedPlacement = building.Floors[0].Placements[0];
            LogAssert.Expect(LogType.Error, new Regex("配置を変更できません"));
            Assert.That(NatoriCityBuildingEditorActions.RotatePlacement(
                building,
                retainedPlacement,
                true), Is.False);

            Assert.That(building.Floors[0].Placements.Count, Is.EqualTo(1));
            Assert.That(retainedPlacement.QuarterTurnsClockwise, Is.EqualTo(0));
            Assert.That(building.GetComponentsInChildren<NatoriCityGeneratedPart>(true).Length,
                Is.EqualTo(1));
            Object.DestroyImmediate(buildingObject);
        }

        [Test]
        public void RemovingSubAssetReferenceShrinksSerializedListWithoutLeavingMissingRow()
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            BuildingGroupDefinition group = settings.BuildingGroups[0];
            var serializedSettings = new SerializedObject(settings);
            SerializedProperty groupsProperty = serializedSettings.FindProperty("_buildingGroups");

            NatoriCityBuilderSubAssetUtility.RemoveReferenceAt(groupsProperty, 0);
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            NatoriCityBuilderSubAssetUtility.Destroy(group);
            serializedSettings.Update();

            Assert.That(groupsProperty.arraySize, Is.EqualTo(0));
            Assert.That(settings.BuildingGroups.Count, Is.EqualTo(0));
        }

        [TestCase(1, 1)]
        [TestCase(2, 3)]
        [TestCase(3, 2)]
        public void SelectionRotationPreservesLayoutAndReturnsAfterFourTurns(int width, int depth)
        {
            NatoriCityBuildingComponent building = CreateSelectionBuilding(width, depth);
            try
            {
                BuildingFloor floor = building.Floors[0];
                var source = new System.Collections.Generic.List<BuildingPartPlacement>(floor.Placements);
                var rotated = NatoriCityPlacementBatch.Transform(source, Vector2Int.zero, 1, true, new Vector2Int(20, 20));
                //既知の座標を先に照合し、同じ誤式の往復だけでテストが通ることを防ぐ。
                Assert.That(rotated[0].AnchorCell, Is.EqualTo(new Vector2Int(10, 30 - width)));
                Assert.That(rotated[0].QuarterTurnsClockwise, Is.EqualTo(1));
                for (int turnSeek = 1; turnSeek < 4; turnSeek++)
                {
                    rotated = NatoriCityPlacementBatch.Transform(rotated, Vector2Int.zero, 1, true, new Vector2Int(20, 20));
                }
                for (int placementSeek = 0; placementSeek < source.Count; placementSeek++)
                {
                    Assert.That(rotated[placementSeek].AnchorCell, Is.EqualTo(source[placementSeek].AnchorCell));
                    Assert.That(rotated[placementSeek].QuarterTurnsClockwise, Is.EqualTo(0));
                    Assert.That(rotated[placementSeek].Identifier, Is.EqualTo(source[placementSeek].Identifier));
                }
                var individual = NatoriCityPlacementBatch.Transform(source, Vector2Int.zero, -1, false, Vector2Int.zero);
                Assert.That(individual[0].AnchorCell, Is.EqualTo(source[0].AnchorCell));
                Assert.That(individual[0].QuarterTurnsClockwise, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(building.gameObject);
            }
        }

        [Test]
        public void RectangleSelectionUsesRotatedFootprintsAndDeduplicatesAdditiveSelection()
        {
            NatoriCityBuildingComponent building = CreateSelectionBuilding(2, 3);
            try
            {
                BuildingFloor floor = building.Floors[0];
                floor.Placements[0].SetGridTransform(new Vector2Int(10, 10), 1);
                var selection = new NatoriCityPlacementSelection();
                var hits = NatoriCityPlacementSelection.InRectangle(floor, new Vector2Int(12, 11), new Vector2Int(12, 11));
                Assert.That(hits.Count, Is.EqualTo(1));
                selection.Select(floor, hits, false, false);
                selection.Select(floor, hits, true, false);
                Assert.That(selection.Count, Is.EqualTo(1));
                selection.Select(floor, hits, false, true);
                Assert.That(selection.Count, Is.Zero);
                //逆向きの矩形ドラッグも同じ領域を選択する。
                hits = NatoriCityPlacementSelection.InRectangle(floor, new Vector2Int(33, 33), new Vector2Int(10, 10));
                Assert.That(hits.Count, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(building.gameObject);
            }
        }

        [Test]
        public void BatchMoveUpdatesWrappersAndOccupancyWithSingleUndoRedo()
        {
            NatoriCityBuildingComponent building = CreateSelectionBuilding(2, 3);
            try
            {
                BuildingFloor floor = building.Floors[0];
                var source = new System.Collections.Generic.List<BuildingPartPlacement>(floor.Placements);
                var selection = new NatoriCityPlacementSelection();
                selection.Select(floor, source, false, false);
                NatoriCityGeneratedPart generated = FindGeneratedPart(building, source[0].Identifier);
                Vector3 originalPosition = generated.transform.localPosition;
                Undo.ClearAll();
                var state = NatoriCityBuildingEditorStateRegistry.Get(building);
                Assert.That(NatoriCityPlacementBatch.Apply(state, floor, source,
                    NatoriCityPlacementBatch.Transform(source, new Vector2Int(2, 4), 0, false, Vector2Int.zero),
                    "Test Batch Move", out string error), Is.True, error);
                Assert.That(FindGeneratedPart(building, source[0].Identifier), Is.SameAs(generated));
                Assert.That(generated.transform.localPosition, Is.EqualTo(originalPosition + new Vector3(2, 0, 4)));
                Assert.That(state.ModelIndex.GetOccupancy(floor).GetPlacementCount(new Vector2Int(10, 10)), Is.Zero);
                Assert.That(state.ModelIndex.GetOccupancy(floor).GetPlacementCount(new Vector2Int(12, 14)), Is.EqualTo(1));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(building.Floors[0].Placements[0].AnchorCell, Is.EqualTo(new Vector2Int(10, 10)));
                Assert.That(generated.transform.localPosition, Is.EqualTo(originalPosition));
                Assert.That(selection.Resolve(building.Floors[0]).Count, Is.EqualTo(2));
                Undo.PerformRedo();
                Assert.That(building.Floors[0].Placements[0].AnchorCell, Is.EqualTo(new Vector2Int(12, 14)));
                Assert.That(generated.transform.localPosition, Is.EqualTo(originalPosition + new Vector3(2, 0, 4)));
            }
            finally
            {
                Object.DestroyImmediate(building.gameObject);
            }
        }

        [Test]
        public void InvalidBatchDoesNotPartiallyMutateModelOrGeneratedObjects()
        {
            NatoriCityBuildingComponent building = CreateSelectionBuilding(2, 3);
            try
            {
                BuildingFloor floor = building.Floors[0];
                var source = new System.Collections.Generic.List<BuildingPartPlacement>(floor.Placements);
                var state = NatoriCityBuildingEditorStateRegistry.Get(building);
                var candidates = NatoriCityPlacementBatch.Transform(source, new Vector2Int(70, 0), 0, false, Vector2Int.zero);
                Vector3 original = FindGeneratedPart(building, source[0].Identifier).transform.localPosition;
                Assert.That(NatoriCityPlacementBatch.Apply(state, floor, source, candidates, "Invalid Move", out _), Is.False);
                Assert.That(source[0].AnchorCell, Is.EqualTo(new Vector2Int(10, 10)));
                Assert.That(FindGeneratedPart(building, source[0].Identifier).transform.localPosition, Is.EqualTo(original));
                //移動対象自身の旧位置は占有判定から除外するが、未選択の配置は除外しない。
                var firstOnly = new[] { source[0] };
                candidates = NatoriCityPlacementBatch.Transform(firstOnly, new Vector2Int(20, 20), 0, false, Vector2Int.zero);
                Assert.That(NatoriCityPlacementBatch.Apply(state, floor, firstOnly, candidates, "Duplicate Move", out _), Is.False);
                Assert.That(floor.Placements.Count, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(building.gameObject);
            }
        }

        [Test]
        public void ClipboardPreservesRelativeLayoutOrientationAndGetsNewIdentifiers()
        {
            string previousClipboard = EditorGUIUtility.systemCopyBuffer;
            NatoriCityBuildingComponent building = CreateSelectionBuilding(2, 3);
            try
            {
                BuildingFloor floor = building.Floors[0];
                floor.Placements[0].RotateClockwise();
                NatoriCityBuildingEditorStateRegistry.InvalidateModel(building);
                NatoriCityPlacementClipboard.Copy(floor.Placements);
                Assert.That(NatoriCityPlacementClipboard.TryRead(new Vector2Int(40, 40), out var candidates, out string error), Is.True, error);
                Assert.That(candidates[0].AnchorCell, Is.EqualTo(new Vector2Int(40, 40)));
                Assert.That(candidates[1].AnchorCell, Is.EqualTo(new Vector2Int(60, 60)));
                Assert.That(candidates[0].QuarterTurnsClockwise, Is.EqualTo(1));
                Assert.That(candidates[0].Identifier, Is.Not.EqualTo(floor.Placements[0].Identifier));
                var state = NatoriCityBuildingEditorStateRegistry.Get(building);
                Assert.That(NatoriCityPlacementBatch.Apply(state, floor, System.Array.Empty<BuildingPartPlacement>(), candidates,
                    "Test Paste", out error), Is.True, error);
                Assert.That(floor.Placements.Count, Is.EqualTo(4));
                Assert.That(building.GetComponentsInChildren<NatoriCityGeneratedPart>().Length, Is.EqualTo(4));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(building.Floors[0].Placements.Count, Is.EqualTo(2));
                Assert.That(building.GetComponentsInChildren<NatoriCityGeneratedPart>().Length, Is.EqualTo(2));
                //コピー後に定義が変化した場合は、新しい形状へ勝手に置き換えない。
                var serializedPart = new SerializedObject(building.Floors[0].Placements[0].PartDefinition);
                serializedPart.FindProperty("_footprintWidth").intValue = 8;
                serializedPart.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(NatoriCityPlacementClipboard.TryRead(Vector2Int.zero, out _, out _), Is.False);
            }
            finally
            {
                EditorGUIUtility.systemCopyBuffer = previousClipboard;
                Object.DestroyImmediate(building.gameObject);
            }
        }

        [Test]
        public void BatchDeleteRemovesAllSelectedWrappersAndUndoRestoresIdentifiers()
        {
            NatoriCityBuildingComponent building = CreateSelectionBuilding(1, 1);
            try
            {
                BuildingFloor floor = building.Floors[0];
                string identifier = floor.Placements[0].Identifier;
                var source = new System.Collections.Generic.List<BuildingPartPlacement>(floor.Placements);
                Undo.ClearAll();
                Assert.That(NatoriCityPlacementBatch.Apply(NatoriCityBuildingEditorStateRegistry.Get(building), floor,
                    source, System.Array.Empty<BuildingPartPlacement>(), "Test Delete", out string error), Is.True, error);
                Assert.That(floor.Placements.Count, Is.Zero);
                Assert.That(building.GetComponentsInChildren<NatoriCityGeneratedPart>().Length, Is.Zero);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(building.Floors[0].Placements.Count, Is.EqualTo(2));
                Assert.That(building.Floors[0].Placements[0].Identifier, Is.EqualTo(identifier));
                Assert.That(building.GetComponentsInChildren<NatoriCityGeneratedPart>().Length, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(building.gameObject);
            }
        }

        [Test]
        public void ClipboardCanPasteToCompatibleFloorAndRejectsOtherGroups()
        {
            string previousClipboard = EditorGUIUtility.systemCopyBuffer;
            NatoriCityBuildingComponent building = CreateSelectionBuilding(1, 1);
            try
            {
                NatoriCityPlacementClipboard.Copy(building.Floors[0].Placements);
                building.AddFloor(building.Floors[0].BuildingGroup);
                var state = NatoriCityBuildingEditorStateRegistry.Get(building);
                state.RefreshModel();
                Assert.That(NatoriCityPlacementClipboard.TryRead(new Vector2Int(40, 40), out var candidates, out _), Is.True);
                Assert.That(NatoriCityPlacementBatch.Apply(state, building.Floors[1], System.Array.Empty<BuildingPartPlacement>(),
                    candidates, "Paste To Other Floor", out string error), Is.True, error);
                Assert.That(building.Floors[0].Placements.Count, Is.EqualTo(2));
                Assert.That(building.Floors[1].Placements.Count, Is.EqualTo(2));
                Assert.That(FindGeneratedPart(building, candidates[0].Identifier).transform.localPosition.y,
                    Is.EqualTo(building.Floors[0].BuildingGroup.Height));
                var otherGroup = NatoriCityBuilderSubAssetUtility.Create<BuildingGroupDefinition>(building.Settings, "Other Group");
                var serializedSettings = new SerializedObject(building.Settings);
                AddReference(serializedSettings.FindProperty("_buildingGroups"), otherGroup);
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                building.AddFloor(otherGroup);
                state.RefreshModel();
                Assert.That(NatoriCityPlacementBatch.Apply(state, building.Floors[2], System.Array.Empty<BuildingPartPlacement>(),
                    candidates, "Reject Other Group", out _), Is.False);
                Assert.That(building.Floors[2].Placements.Count, Is.Zero);
                EditorGUIUtility.systemCopyBuffer = "NatoriCityBuilder.Placements\n{broken";
                Assert.That(NatoriCityPlacementClipboard.TryRead(Vector2Int.zero, out _, out _), Is.False);
            }
            finally
            {
                EditorGUIUtility.systemCopyBuffer = previousClipboard;
                Object.DestroyImmediate(building.gameObject);
            }
        }

        [Test]
        public void BatchMoveOnPrefabInstancePersistsOverridesWithoutChangingSource()
        {
            NatoriCityBuildingComponent source = CreateSelectionBuilding(2, 3);
            GameObject instance = null;
            try
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source.gameObject, TestFolder + "/SelectionBuilding.prefab");
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var building = instance.GetComponent<NatoriCityBuildingComponent>();
                var selected = new System.Collections.Generic.List<BuildingPartPlacement>(building.Floors[0].Placements);
                Assert.That(NatoriCityPlacementBatch.Apply(NatoriCityBuildingEditorStateRegistry.Get(building), building.Floors[0],
                    selected, NatoriCityPlacementBatch.Transform(selected, new Vector2Int(3, 4), 1, false, Vector2Int.zero),
                    "Move Prefab Selection", out string error), Is.True, error);
                Undo.FlushUndoRecordObjects();
                Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), Is.True);
                Assert.That(prefab.GetComponent<NatoriCityBuildingComponent>().Floors[0].Placements[0].AnchorCell,
                    Is.EqualTo(new Vector2Int(10, 10)));
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, TestFolder + "/EditedBuilding.prefab");
                var savedBuilding = saved.GetComponent<NatoriCityBuildingComponent>();
                Assert.That(savedBuilding.Floors[0].Placements[0].AnchorCell, Is.EqualTo(new Vector2Int(13, 14)));
                Assert.That(savedBuilding.Floors[0].Placements[0].QuarterTurnsClockwise, Is.EqualTo(1));
                Assert.That(savedBuilding.GetComponentsInChildren<NatoriCityGeneratedPart>().Length, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(source.gameObject);
            }
        }

        private static NatoriCityBuildingComponent CreateSelectionBuilding(int width, int depth)
        {
            NatoriCityBuilderSettings settings = CreateSettings();
            PlacementTypeDefinition type = settings.PlacementTypes[0];
            var serializedType = new SerializedObject(type);
            serializedType.FindProperty("_disallowSamePartAtSameAnchor").boolValue = true;
            serializedType.ApplyModifiedPropertiesWithoutUndo();
            NatoriCityBuildingAssetList list = CreateAssetList(settings.BuildingGroups[0], type, CreatePartPrefab());
            var serializedPart = new SerializedObject(list.Parts[0]);
            serializedPart.FindProperty("_footprintWidth").intValue = width;
            serializedPart.FindProperty("_footprintDepth").intValue = depth;
            serializedPart.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            var building = new GameObject("Selection Test").AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), list);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(settings.BuildingGroups[0]);
            building.Floors[0].AddPlacement(new BuildingPartPlacement(list.Parts[0], new Vector2Int(10, 10)));
            building.Floors[0].AddPlacement(new BuildingPartPlacement(list.Parts[0], new Vector2Int(30, 30)));
            Assert.That(NatoriCityBuildingEditorRebuilder.Rebuild(building, "Create Selection Test"), Is.True);
            return building;
        }

        private static NatoriCityBuilderSettings CreateSettings()
        {
            var settings = ScriptableObject.CreateInstance<NatoriCityBuilderSettings>();
            AssetDatabase.CreateAsset(settings, TestFolder + "/NatoriCityBuilderSettings.asset");
            BuildingGroupDefinition group = NatoriCityBuilderSubAssetUtility.Create<BuildingGroupDefinition>(
                settings,
                "Group");
            PlacementTypeDefinition placementType =
                NatoriCityBuilderSubAssetUtility.Create<PlacementTypeDefinition>(settings, "Placement Type");
            var serializedSettings = new SerializedObject(settings);
            AddReference(serializedSettings.FindProperty("_buildingGroups"), group);
            AddReference(serializedSettings.FindProperty("_placementTypes"), placementType);
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            NatoriCityBuilderProjectSettings.Settings = settings;
            return settings;
        }

        private static GameObject CreatePartPrefab()
        {
            return CreatePartPrefab("Part", PrimitiveType.Cube);
        }

        private static GameObject CreatePartPrefab(string name, PrimitiveType primitiveType)
        {
            GameObject source = GameObject.CreatePrimitive(primitiveType);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                source,
                TestFolder + "/" + name + ".prefab");
            Object.DestroyImmediate(source);
            return prefab;
        }

        private static NatoriCityBuildingAssetList CreateAssetList(
            BuildingGroupDefinition group,
            PlacementTypeDefinition placementType,
            GameObject partPrefab)
        {
            var assetList = ScriptableObject.CreateInstance<NatoriCityBuildingAssetList>();
            AssetDatabase.CreateAsset(assetList, TestFolder + "/AssetList.asset");
            BuildingPartDefinition part = NatoriCityBuilderSubAssetUtility.Create<BuildingPartDefinition>(
                assetList,
                "Part");
            var serializedPart = new SerializedObject(part);
            serializedPart.FindProperty("_prefab").objectReferenceValue = partPrefab;
            serializedPart.FindProperty("_buildingGroup").objectReferenceValue = group;
            serializedPart.FindProperty("_placementType").objectReferenceValue = placementType;
            serializedPart.ApplyModifiedPropertiesWithoutUndo();
            var serializedAssetList = new SerializedObject(assetList);
            AddReference(serializedAssetList.FindProperty("_parts"), part);
            serializedAssetList.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return assetList;
        }

        private static GameObject CreateBuildingPrefab(
            BuildingGroupDefinition group,
            NatoriCityBuildingAssetList assetList,
            BuildingPartDefinition part)
        {
            var buildingObject = new GameObject("Building");
            var building = buildingObject.AddComponent<NatoriCityBuildingComponent>();
            var serializedBuilding = new SerializedObject(building);
            AddReference(serializedBuilding.FindProperty("_assetLists"), assetList);
            serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
            building.AddFloor(group);
            building.Floors[0].AddPlacement(new BuildingPartPlacement(part, Vector2Int.zero));
            Assert.That(NatoriCityBuildingEditorRebuilder.Rebuild(
                building,
                "Test Build Prefab Source"), Is.True);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                buildingObject,
                TestFolder + "/Building.prefab");
            Object.DestroyImmediate(buildingObject);
            return prefab;
        }

        private static void AddReference(SerializedProperty arrayProperty, Object value)
        {
            arrayProperty.arraySize++;
            arrayProperty.GetArrayElementAtIndex(arrayProperty.arraySize - 1).objectReferenceValue = value;
        }

        private static NatoriCityGeneratedPart FindGeneratedPart(
            NatoriCityBuildingComponent building,
            string placementIdentifier)
        {
            NatoriCityGeneratedPart[] generatedParts =
                building.GetComponentsInChildren<NatoriCityGeneratedPart>(true);
            for (int partSeek = 0; partSeek < generatedParts.Length; partSeek++)
            {
                if (generatedParts[partSeek].PlacementIdentifier == placementIdentifier)
                {
                    return generatedParts[partSeek];
                }
            }
            return null;
        }
    }
}
