using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    internal static class NatoriCityToolbarIcons
    {
        private static readonly GUIContent[] ModeContents =
        {
            Load("Pen", "ペン: パレットのパーツを配置"),
            Load("Select", "単一選択: クリックで選択。Shiftで追加、Ctrlで選択切替"),
            Load("Rectangle", "範囲選択: ドラッグした矩形内の配置をまとめて選択"),
        };
        private static readonly GUIContent[] ToolContents =
        {
            Load("Move", "グリッド移動: 矢印・中央の四角をドラッグ"),
            Load("RotateGroup", "配置全体の回転: 黄色の円周をドラッグして90度刻みで回転"),
            Load("RotateEach", "各配置の回転: 紫の円周をドラッグ。各アンカーを固定して90度刻みで回転"),
        };

        private static GUIContent Load(string name, string tooltip)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/com.natori.city-builder/Editor/Icons/" + name + ".png");
            return new GUIContent(texture, tooltip);
        }

        public static int DrawMode(int mode)
        {
            return Draw(mode, ModeContents);
        }

        public static int DrawTool(int tool)
        {
            return Draw(tool, ToolContents);
        }

        private static int Draw(int selected, GUIContent[] contents)
        {
            //元画像の解像度にかかわらずボタン内へ収め、他のEditor GUIへサイズを持ち越さない。
            Vector2 previousSize = EditorGUIUtility.GetIconSize();
            EditorGUIUtility.SetIconSize(new Vector2(24, 24));
            var style = new GUIStyle(GUI.skin.button) { imagePosition = ImagePosition.ImageOnly };
            int result = GUILayout.Toolbar(selected, contents, style, GUILayout.Width(108), GUILayout.Height(32));
            EditorGUIUtility.SetIconSize(previousSize);
            return result;
        }
    }
}
