using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Natori.CityBuilder.Editor.Tests
{
    public sealed class NatoriCityToolbarIconsTests
    {
        [TestCase("Pen")]
        [TestCase("Select")]
        [TestCase("Rectangle")]
        [TestCase("Move")]
        [TestCase("RotateGroup")]
        [TestCase("RotateEach")]
        public void PackageIconImportsAsTexture(string name)
        {
            //Git導入でも表示できるよう、パッケージ内の画像とインポート設定を検証する。
            string path = "Packages/com.natori.city-builder/Editor/Icons/" + name + ".png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null, "Imported type: " + AssetDatabase.GetMainAssetTypeAtPath(path));
            Assert.That(texture.width, Is.GreaterThanOrEqualTo(24));
        }
    }
}
