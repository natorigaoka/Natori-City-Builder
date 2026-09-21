using NUnit.Framework;
using UnityEngine;
using Natori.CityBuilder.Editor;

namespace Natori.CityBuilder.Tests
{
    public sealed class NatoriCityScreenRectangleTests
    {
        private Camera _camera;
        private readonly Rect _viewport = new(0, 0, 800, 600);

        [SetUp]
        public void SetUp()
        {
            _camera = new GameObject("Selection camera").AddComponent<Camera>();
            _camera.aspect = 4.0f / 3;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 100;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_camera.gameObject);
        }

        [Test]
        public void ReverseDragKeepsScreenAlignedRectangle()
        {
            Assert.That(NatoriCityScreenRectangle.FromPoints(new Vector2(700, 500), new Vector2(100, 80)),
                Is.EqualTo(new Rect(100, 80, 600, 420)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ObliqueViewSelectsProjectedFootprint(bool orthographic)
        {
            _camera.orthographic = orthographic;
            _camera.orthographicSize = 5;
            _camera.transform.position = new Vector3(6, 8, -9);
            _camera.transform.LookAt(Vector3.zero);
            var polygon = new[] { new Vector3(-1, 0, -1), new Vector3(-1, 0, 1),
                new Vector3(1, 0, 1), new Vector3(1, 0, -1) };
            Assert.That(NatoriCityScreenRectangle.Intersects(_camera, _viewport, new Rect(395, 295, 10, 10), polygon), Is.True);
            Assert.That(NatoriCityScreenRectangle.Intersects(_camera, _viewport, new Rect(0, 0, 20, 20), polygon), Is.False);
        }

        [Test]
        public void EmptyCornerOfProjectedBoundsIsNotSelected()
        {
            _camera.orthographic = true;
            _camera.orthographicSize = 3;
            //画面中央の菱形。外接矩形の隅を囲んでも、実際の投影面とは交差しない。
            var diamond = new[] { new Vector3(0, 2, 5), new Vector3(2, 0, 5),
                new Vector3(0, -2, 5), new Vector3(-2, 0, 5) };
            Assert.That(NatoriCityScreenRectangle.Intersects(_camera, _viewport, new Rect(560, 110, 20, 20), diamond), Is.False);
            Assert.That(NatoriCityScreenRectangle.Intersects(_camera, _viewport, new Rect(190, 295, 420, 10), diamond), Is.True);
        }

        [Test]
        public void BehindCameraIsExcludedAndNearPlaneCrossingIsClipped()
        {
            var behind = new[] { new Vector3(-1, -1, -5), new Vector3(-1, 1, -5),
                new Vector3(1, 1, -5), new Vector3(1, -1, -5) };
            Assert.That(NatoriCityScreenRectangle.Intersects(_camera, _viewport, _viewport, behind), Is.False);
            var crossing = new[] { new Vector3(-1, -1, -1), new Vector3(-1, 1, 2),
                new Vector3(1, 1, 2), new Vector3(1, -1, -1) };
            Assert.That(NatoriCityScreenRectangle.Intersects(_camera, _viewport, _viewport, crossing), Is.True);
        }
    }
}
