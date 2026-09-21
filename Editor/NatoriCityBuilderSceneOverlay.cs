using UnityEditor;
using UnityEditor.Overlays;

namespace Natori.CityBuilder.Editor
{
    //Unityのオーバーレイとして表示することで、パネル上のクリックが配置操作へ流れない。
    [Overlay(typeof(SceneView), "Natori City Builder", true,
        defaultDockZone = DockZone.LeftColumn, defaultDockPosition = DockPosition.Top)]
    internal sealed class NatoriCityBuilderSceneOverlay : IMGUIOverlay, ITransientOverlay
    {
        public bool visible => NatoriCityBuilderEditingSession.IsEditing;

        public override void OnGUI()
        {
            NatoriCityBuilderSceneTool.DrawSceneToolbar();
        }
    }
}
