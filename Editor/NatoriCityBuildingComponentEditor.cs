using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    [CustomEditor(typeof(NatoriCityBuildingComponent))]
    public sealed class NatoriCityBuildingComponentEditor : UnityEditor.Editor
    {
        private ReorderableList _assetLists;
        private PlacementTypeDefinition _selectedPlacementType;
        private string _placementTypeSearch = string.Empty;
        private string _partSearch = string.Empty;
        private string _hoveredPlacementTypeLabel;

        private void OnEnable()
        {
            NatoriCitySelectionSceneTool.Changed += Repaint;
            SerializedProperty assetListsProperty = serializedObject.FindProperty("_assetLists");
            _assetLists = new ReorderableList(serializedObject, assetListsProperty, true, true, true, true);
            _assetLists.drawHeaderCallback = rectangle => EditorGUI.LabelField(rectangle, "使用するAsset List");
            _assetLists.elementHeight = EditorGUIUtility.singleLineHeight + 4.0f;
            _assetLists.drawElementCallback = (rectangle, index, active, focused) =>
            {
                rectangle.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(
                    rectangle,
                    assetListsProperty.GetArrayElementAtIndex(index),
                    GUIContent.none);
            };
        }

        private void OnDisable()
        {
            NatoriCitySelectionSceneTool.Changed -= Repaint;
        }

        public override void OnInspectorGUI()
        {
            var building = (NatoriCityBuildingComponent)target;
            NatoriCityBuilderSettings settings = NatoriCityBuilderProjectSettings.Settings;
            if (settings == null)
            {
                DrawMissingSettings();
                return;
            }
            NatoriCityBuilderProjectSettings.SynchronizeBuildingSettings(building);

            serializedObject.Update();
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_rebuildInAwake"),
                new GUIContent("AwakeでRebuild"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_rebuildWhenEditingBegins"),
                new GUIContent("Edit開始時にRebuild"));
            serializedObject.ApplyModifiedProperties();
            DrawGridSize(building);
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            _assetLists.DoLayoutList();
            bool assetListsChanged = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (assetListsChanged)
            {
                NatoriCityBuildingEditorStateRegistry.InvalidateModel(building);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild Generated Objects"))
            {
                NatoriCityBuildingEditorRebuilder.Rebuild(building, "Rebuild Natori City Builder");
            }
            NatoriCityBuildingEditorState state = NatoriCityBuildingEditorStateRegistry.Get(building);
            DrawValidation(state);
            EditorGUILayout.Space();
            DrawFloorVisibilityHeader(building);
            DrawFloors(building, settings);
            DrawAddFloorButton(building, settings);
            DrawEditingPalette(building, state);
        }

        private void DrawMissingSettings()
        {
            EditorGUILayout.HelpBox(
                "Project Settings > Natori City Builderで共通設定アセットを登録してから編集してください。",
                MessageType.Error);
            if (GUILayout.Button("Project Settingsを開く"))
            {
                SettingsService.OpenProjectSettings("Project/Natori City Builder");
            }
        }

        private void DrawGridSize(NatoriCityBuildingComponent building)
        {
            SerializedProperty gridSizeProperty = serializedObject.FindProperty("_gridSize");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(gridSizeProperty, new GUIContent("グリッド範囲 X/Z"));
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }
            serializedObject.ApplyModifiedProperties();
            Vector2Int currentGridSize = building.GridSize;
            if (currentGridSize.x > 0 && currentGridSize.y > 0)
            {
                NatoriCityBuildingEditorActions.RebuildAfterGridResize(building);
            }
            else
            {
                SceneView.RepaintAll();
            }
            serializedObject.Update();
        }

        private static void DrawValidation(NatoriCityBuildingEditorState state)
        {
            NatoriCityBuildingValidationResult validation = state.Validation;
            for (int errorSeek = 0; errorSeek < validation.Errors.Count; errorSeek++)
            {
                EditorGUILayout.HelpBox(validation.Errors[errorSeek], MessageType.Error);
            }
            for (int warningSeek = 0; warningSeek < validation.Warnings.Count; warningSeek++)
            {
                EditorGUILayout.HelpBox(validation.Warnings[warningSeek], MessageType.Warning);
            }
        }

        private static void DrawFloorVisibilityHeader(NatoriCityBuildingComponent building)
        {
            EditorGUILayout.LabelField("階", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("すべて表示"))
            {
                NatoriCityBuildingVisibility.SetAllFloorsVisible(building, true);
            }
            if (GUILayout.Button("すべて非表示"))
            {
                NatoriCityBuildingVisibility.SetAllFloorsVisible(building, false);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFloors(
            NatoriCityBuildingComponent building,
            NatoriCityBuilderSettings settings)
        {
            for (int floorSeek = 0; floorSeek < building.Floors.Count; floorSeek++)
            {
                BuildingFloor floor = building.Floors[floorSeek];
                if (floor == null)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"[{floorSeek + 1}F] 階データがMissingです");
                    if (GUILayout.Button("削除", GUILayout.Width(48.0f)))
                    {
                        NatoriCityBuildingEditorActions.RemoveFloor(building, floorSeek);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                    continue;
                }
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"[{floorSeek + 1}F]", GUILayout.Width(38.0f));
                DrawFloorGroupButton(building, floor, settings);
                bool floorHidden = NatoriCityBuildingVisibility.IsFloorHidden(building, floor);
                if (GUILayout.Button(floorHidden ? "Show" : "Hide", GUILayout.Width(48.0f)))
                {
                    NatoriCityBuildingVisibility.SetFloorVisible(building, floor, floorHidden);
                }
                if (GUILayout.Button("Hide Except", GUILayout.Width(82.0f)))
                {
                    NatoriCityBuildingVisibility.HideAllExcept(building, floor);
                }
                bool isEditing = NatoriCityBuilderEditingSession.Building == building
                    && NatoriCityBuilderEditingSession.FloorIdentifier == floor.Identifier;
                if (GUILayout.Button(isEditing ? "Stop" : "Edit", GUILayout.Width(48.0f)))
                {
                    ToggleEditing(building, floor, isEditing);
                }
                if (GUILayout.Button("−", GUILayout.Width(24.0f)))
                {
                    RemoveFloor(building, floorSeek, floor);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void DrawFloorGroupButton(
            NatoriCityBuildingComponent building,
            BuildingFloor floor,
            NatoriCityBuilderSettings settings)
        {
            string groupLabel = floor.BuildingGroup == null ? "Missing" : floor.BuildingGroup.Label;
            if (!GUILayout.Button(groupLabel, EditorStyles.popup))
            {
                return;
            }
            var menu = new GenericMenu();
            for (int groupSeek = 0; groupSeek < settings.BuildingGroups.Count; groupSeek++)
            {
                BuildingGroupDefinition group = settings.BuildingGroups[groupSeek];
                if (group == null)
                {
                    continue;
                }
                bool selected = group == floor.BuildingGroup;
                menu.AddItem(new GUIContent(group.Label), selected, () => ChangeFloorGroup(building, floor, group));
            }
            menu.ShowAsContext();
        }

        private static void ChangeFloorGroup(
            NatoriCityBuildingComponent building,
            BuildingFloor floor,
            BuildingGroupDefinition group)
        {
            if (group == floor.BuildingGroup)
            {
                return;
            }
            if (floor.Placements.Count > 0 && !EditorUtility.DisplayDialog(
                "階層グループを変更",
                "この階のすべての配置情報が破壊されます。続行しますか？",
                "変更",
                "キャンセル"))
            {
                return;
            }
            NatoriCityBuildingEditorActions.SetFloorBuildingGroup(building, floor, group);
        }

        private static void RemoveFloor(
            NatoriCityBuildingComponent building,
            int floorIndex,
            BuildingFloor floor)
        {
            if (floor.Placements.Count > 0 && !EditorUtility.DisplayDialog(
                "階を削除",
                "この階のすべての配置情報が削除され、上の階が下へ詰められます。続行しますか？",
                "削除",
                "キャンセル"))
            {
                return;
            }
            if (NatoriCityBuilderEditingSession.FloorIdentifier == floor.Identifier)
            {
                NatoriCityBuilderEditingSession.End();
            }
            NatoriCityBuildingEditorActions.RemoveFloor(building, floorIndex);
        }

        private static void ToggleEditing(
            NatoriCityBuildingComponent building,
            BuildingFloor floor,
            bool isEditing)
        {
            if (isEditing)
            {
                NatoriCityBuilderEditingSession.End();
                return;
            }
            if (building.RebuildWhenEditingBegins
                && !NatoriCityBuildingEditorRebuilder.Rebuild(building, "Rebuild Natori City Builder"))
            {
                return;
            }
            if (!building.RebuildWhenEditingBegins)
            {
                NatoriCityBuildingValidationResult validation =
                    NatoriCityBuildingEditorStateRegistry.Get(building).Validation;
                if (!validation.IsValid)
                {
                    Debug.LogError(
                        "Natori City Builder: Editを開始できません。Inspectorに表示されたエラーを解消してください。",
                        building);
                    return;
                }
            }
            NatoriCityBuilderEditingSession.Begin(building, floor);
        }

        private static void DrawAddFloorButton(
            NatoriCityBuildingComponent building,
            NatoriCityBuilderSettings settings)
        {
            if (!GUILayout.Button("階を追加"))
            {
                return;
            }
            var menu = new GenericMenu();
            for (int groupSeek = 0; groupSeek < settings.BuildingGroups.Count; groupSeek++)
            {
                BuildingGroupDefinition group = settings.BuildingGroups[groupSeek];
                if (group == null)
                {
                    continue;
                }
                menu.AddItem(
                    new GUIContent(group.Label),
                    false,
                    () => NatoriCityBuildingEditorActions.AddFloor(building, group));
            }
            if (settings.BuildingGroups.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("階層グループがありません"));
            }
            menu.ShowAsContext();
        }

        private void DrawEditingPalette(
            NatoriCityBuildingComponent building,
            NatoriCityBuildingEditorState state)
        {
            if (NatoriCityBuilderEditingSession.Building != building)
            {
                return;
            }
            BuildingFloor floor = NatoriCityBuilderEditingSession.GetEditingFloor();
            if (floor == null)
            {
                return;
            }

            EditorGUILayout.Space();
            if (!NatoriCityBuilderSceneTool.DrawEditingTools(state, floor))
            {
                return;
            }
            EditorGUILayout.LabelField("配置パレット", EditorStyles.boldLabel);
            bool strokeModeEnabled = EditorGUILayout.ToggleLeft(
                "ストロークモード（左ドラッグで連続配置）",
                NatoriCityBuilderEditorPreferences.StrokeModeEnabled);
            if (strokeModeEnabled != NatoriCityBuilderEditorPreferences.StrokeModeEnabled)
            {
                NatoriCityBuilderEditorPreferences.StrokeModeEnabled = strokeModeEnabled;
                SceneView.RepaintAll();
            }
            EditorGUILayout.Space(4.0f);
            EditorGUILayout.LabelField("配置種別", EditorStyles.boldLabel);
            IReadOnlyList<PlacementTypeDefinition> availableTypes =
                state.ModelIndex.GetPlacementTypes(floor.BuildingGroup);
            if (availableTypes.Count == 0)
            {
                NatoriCityBuilderEditingSession.SelectPart(null);
                EditorGUILayout.HelpBox("この階層グループへ配置できるパーツがありません。", MessageType.Warning);
                return;
            }

            _placementTypeSearch = EditorGUILayout.TextField(
                "文字列検索",
                _placementTypeSearch);
            IReadOnlyList<PlacementTypeDefinition> filteredTypes = FilterPlacementTypes(
                availableTypes,
                _placementTypeSearch);
            if (filteredTypes.Count == 0)
            {
                NatoriCityBuilderEditingSession.SelectPart(null);
                EditorGUILayout.HelpBox("検索条件に一致する配置種別がありません。", MessageType.Info);
                return;
            }

            PlacementTypeDefinition previousPlacementType = _selectedPlacementType;
            if (_selectedPlacementType == null
                || !ContainsPlacementType(filteredTypes, _selectedPlacementType))
            {
                _selectedPlacementType = filteredTypes[0];
            }
            DrawPlacementTypeTabs(filteredTypes);
            if (_selectedPlacementType != previousPlacementType)
            {
                NatoriCityBuilderEditingSession.SelectPart(null);
                SceneView.RepaintAll();
            }
            EditorGUILayout.Space(6.0f);
            EditorGUILayout.LabelField("配置パーツアセット", EditorStyles.boldLabel);
            _partSearch = EditorGUILayout.TextField("文字列検索", _partSearch);
            DrawPartButtons(state, floor, _selectedPlacementType);
            DrawPlacementTypeHoverOverlay();
        }

        private static IReadOnlyList<PlacementTypeDefinition> FilterPlacementTypes(
            IReadOnlyList<PlacementTypeDefinition> availableTypes,
            string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return availableTypes;
            }
            var filteredTypes = new List<PlacementTypeDefinition>();
            for (int typeSeek = 0; typeSeek < availableTypes.Count; typeSeek++)
            {
                PlacementTypeDefinition placementType = availableTypes[typeSeek];
                string placementTypeLabel = placementType.Label ?? string.Empty;
                if (placementTypeLabel.IndexOf(
                    searchText,
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    filteredTypes.Add(placementType);
                }
            }
            return filteredTypes;
        }

        private static bool ContainsPlacementType(
            IReadOnlyList<PlacementTypeDefinition> placementTypes,
            PlacementTypeDefinition target)
        {
            for (int typeSeek = 0; typeSeek < placementTypes.Count; typeSeek++)
            {
                if (placementTypes[typeSeek] == target)
                {
                    return true;
                }
            }
            return false;
        }

        private void DrawPlacementTypeTabs(IReadOnlyList<PlacementTypeDefinition> placementTypes)
        {
            const float tabWidth = 120.0f;
            const float tabHeight = 25.0f;
            const float tabSpacing = 4.0f;
            float availableWidth = Mathf.Max(tabWidth, EditorGUIUtility.currentViewWidth - 36.0f);
            int columnCount = Mathf.Max(
                1,
                Mathf.FloorToInt((availableWidth + tabSpacing) / (tabWidth + tabSpacing)));
            var tabStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
            };
            _hoveredPlacementTypeLabel = null;

            for (int typeSeek = 0; typeSeek < placementTypes.Count; typeSeek++)
            {
                if (typeSeek % columnCount == 0)
                {
                    EditorGUILayout.BeginHorizontal();
                }
                else
                {
                    GUILayout.Space(tabSpacing);
                }

                PlacementTypeDefinition placementType = placementTypes[typeSeek];
                Color previousBackgroundColor = GUI.backgroundColor;
                if (placementType == _selectedPlacementType)
                {
                    GUI.backgroundColor = new Color(0.35f, 0.75f, 1.0f);
                }
                if (GUILayout.Button(
                    new GUIContent(placementType.Label, placementType.Label),
                    tabStyle,
                    GUILayout.Width(tabWidth),
                    GUILayout.Height(tabHeight)))
                {
                    _selectedPlacementType = placementType;
                }
                Rect tabRectangle = GUILayoutUtility.GetLastRect();
                if (tabRectangle.Contains(Event.current.mousePosition))
                {
                    _hoveredPlacementTypeLabel = placementType.Label;
                }
                GUI.backgroundColor = previousBackgroundColor;

                if (typeSeek % columnCount == columnCount - 1)
                {
                    EditorGUILayout.EndHorizontal();
                }
            }
            if (placementTypes.Count % columnCount != 0)
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawPlacementTypeHoverOverlay()
        {
            if (string.IsNullOrEmpty(_hoveredPlacementTypeLabel)
                || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var overlayStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
            };
            float maximumWidth = Mathf.Max(100.0f, EditorGUIUtility.currentViewWidth - 16.0f);
            float desiredWidth = overlayStyle.CalcSize(
                new GUIContent(_hoveredPlacementTypeLabel)).x + 12.0f;
            float overlayWidth = Mathf.Min(maximumWidth, Mathf.Max(140.0f, desiredWidth));
            float overlayHeight = overlayStyle.CalcHeight(
                new GUIContent(_hoveredPlacementTypeLabel),
                overlayWidth);
            Vector2 mousePosition = Event.current.mousePosition;
            float maximumX = Mathf.Max(4.0f, EditorGUIUtility.currentViewWidth - overlayWidth - 4.0f);
            var overlayRectangle = new Rect(
                Mathf.Clamp(mousePosition.x + 12.0f, 4.0f, maximumX),
                mousePosition.y + 18.0f,
                overlayWidth,
                overlayHeight);
            GUI.Label(overlayRectangle, _hoveredPlacementTypeLabel, overlayStyle);
        }

        private void DrawPartButtons(
            NatoriCityBuildingEditorState state,
            BuildingFloor floor,
            PlacementTypeDefinition placementType)
        {
            IReadOnlyList<BuildingPartDefinition> parts =
                state.ModelIndex.GetParts(floor.BuildingGroup, placementType);
            const float cardWidth = 128.0f;
            const float cardHeight = 144.0f;
            const float cardSpacing = 6.0f;
            float availableWidth = Mathf.Max(cardWidth, EditorGUIUtility.currentViewWidth - 36.0f);
            int columnCount = Mathf.Max(
                1,
                Mathf.FloorToInt((availableWidth + cardSpacing) / (cardWidth + cardSpacing)));
            int displayedPartCount = 0;
            for (int partSeek = 0; partSeek < parts.Count; partSeek++)
            {
                BuildingPartDefinition part = parts[partSeek];
                if (!string.IsNullOrEmpty(_partSearch)
                    && part.Label.IndexOf(_partSearch, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                if (displayedPartCount % columnCount == 0)
                {
                    EditorGUILayout.BeginHorizontal();
                }
                else
                {
                    GUILayout.Space(cardSpacing);
                }
                Texture preview = part.Prefab == null
                    ? EditorGUIUtility.IconContent("console.warnicon").image
                    : AssetPreview.GetAssetPreview(part.Prefab) ?? AssetPreview.GetMiniThumbnail(part.Prefab);
                bool selected = NatoriCityBuilderEditingSession.SelectedPart == part;
                DrawPartCard(part, preview, selected, cardWidth, cardHeight);
                displayedPartCount++;
                if (displayedPartCount % columnCount == 0)
                {
                    EditorGUILayout.EndHorizontal();
                }
            }
            if (displayedPartCount % columnCount != 0)
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void DrawPartCard(
            BuildingPartDefinition part,
            Texture preview,
            bool selected,
            float cardWidth,
            float cardHeight)
        {
            Rect cardRectangle = GUILayoutUtility.GetRect(
                cardWidth,
                cardHeight,
                GUILayout.Width(cardWidth),
                GUILayout.Height(cardHeight));
            Color previousBackgroundColor = GUI.backgroundColor;
            if (selected)
            {
                GUI.backgroundColor = new Color(0.35f, 0.75f, 1.0f);
            }
            bool clicked = GUI.Button(
                cardRectangle,
                new GUIContent(string.Empty, part.Label),
                GUI.skin.button);
            GUI.backgroundColor = previousBackgroundColor;

            Rect previewRectangle = new(
                cardRectangle.x + 6.0f,
                cardRectangle.y + 6.0f,
                cardRectangle.width - 12.0f,
                104.0f);
            GUI.DrawTexture(previewRectangle, preview, ScaleMode.ScaleToFit, true);

            var labelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                wordWrap = true,
            };
            Rect labelRectangle = new(
                cardRectangle.x + 5.0f,
                previewRectangle.yMax + 2.0f,
                cardRectangle.width - 10.0f,
                cardRectangle.yMax - previewRectangle.yMax - 5.0f);
            GUI.Label(labelRectangle, part.Label, labelStyle);

            if (clicked)
            {
                NatoriCityBuilderEditingSession.SelectPart(selected ? null : part);
                SceneView.RepaintAll();
            }
        }

    }
}
