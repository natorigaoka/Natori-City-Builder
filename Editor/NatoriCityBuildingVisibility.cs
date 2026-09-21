using System.Collections.Generic;
using UnityEditor;

namespace Natori.CityBuilder.Editor
{
    public static class NatoriCityBuildingVisibility
    {
        public static bool IsFloorHidden(NatoriCityBuildingComponent building, BuildingFloor floor)
        {
            return NatoriCityBuildingEditorStateRegistry.Get(building)
                .VisibilityIndex
                .IsFloorHidden(floor.Identifier);
        }

        public static void SetFloorVisible(
            NatoriCityBuildingComponent building,
            BuildingFloor floor,
            bool visible)
        {
            IReadOnlyList<NatoriCityGeneratedPart> parts =
                NatoriCityBuildingEditorStateRegistry.Get(building).GeneratedIndex.GetFloorParts(
                    floor.Identifier);
            for (int partSeek = 0; partSeek < parts.Count; partSeek++)
            {
                NatoriCityGeneratedPart part = parts[partSeek];
                if (part == null)
                {
                    NatoriCityBuildingEditorStateRegistry.InvalidateGeneratedHierarchy(building);
                    continue;
                }
                if (part.FloorIdentifier != floor.Identifier)
                {
                    continue;
                }
                if (visible)
                {
                    SceneVisibilityManager.instance.Show(part.gameObject, true);
                }
                else
                {
                    SceneVisibilityManager.instance.Hide(part.gameObject, true);
                }
            }
            NatoriCityBuildingEditorStateRegistry.Get(building).InvalidateVisibility();
            SceneView.RepaintAll();
        }

        public static void SetAllFloorsVisible(NatoriCityBuildingComponent building, bool visible)
        {
            NatoriCityGeneratedHierarchyIndex generatedIndex =
                NatoriCityBuildingEditorStateRegistry.Get(building).GeneratedIndex;
            foreach (NatoriCityGeneratedPart part in generatedIndex.Parts)
            {
                if (part == null)
                {
                    NatoriCityBuildingEditorStateRegistry.InvalidateGeneratedHierarchy(building);
                    continue;
                }
                if (visible)
                {
                    SceneVisibilityManager.instance.Show(part.gameObject, true);
                }
                else
                {
                    SceneVisibilityManager.instance.Hide(part.gameObject, true);
                }
            }
            NatoriCityBuildingEditorStateRegistry.Get(building).InvalidateVisibility();
            SceneView.RepaintAll();
        }

        public static void HideAllExcept(NatoriCityBuildingComponent building, BuildingFloor retainedFloor)
        {
            NatoriCityGeneratedHierarchyIndex generatedIndex =
                NatoriCityBuildingEditorStateRegistry.Get(building).GeneratedIndex;
            foreach (NatoriCityGeneratedPart part in generatedIndex.Parts)
            {
                if (part == null)
                {
                    NatoriCityBuildingEditorStateRegistry.InvalidateGeneratedHierarchy(building);
                    continue;
                }
                if (part.FloorIdentifier == retainedFloor.Identifier)
                {
                    SceneVisibilityManager.instance.Show(part.gameObject, true);
                }
                else
                {
                    SceneVisibilityManager.instance.Hide(part.gameObject, true);
                }
            }
            NatoriCityBuildingEditorStateRegistry.Get(building).InvalidateVisibility();
            SceneView.RepaintAll();
        }

        public static void DisableGeneratedPartPicking(NatoriCityBuildingComponent building)
        {
            NatoriCityGeneratedRoot root =
                NatoriCityBuildingEditorStateRegistry.Get(building).GeneratedIndex.Root;
            if (root != null)
            {
                SceneVisibilityManager.instance.DisablePicking(root.gameObject, true);
            }
        }

        public static void EnableGeneratedPartPicking(NatoriCityBuildingComponent building)
        {
            NatoriCityGeneratedRoot root =
                NatoriCityBuildingEditorStateRegistry.Get(building).GeneratedIndex.Root;
            if (root != null)
            {
                SceneVisibilityManager.instance.EnablePicking(root.gameObject, true);
            }
        }
    }
}
