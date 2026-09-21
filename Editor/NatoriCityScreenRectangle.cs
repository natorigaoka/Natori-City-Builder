using System.Collections.Generic;
using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    //選択枠と配置の占有面を同じ画面座標へそろえる。斜め視点でも投影AABBの空白を選ばない。
    internal static class NatoriCityScreenRectangle
    {
        public static Rect FromPoints(Vector2 first, Vector2 last)
        {
            Vector2 minimum = Vector2.Min(first, last);
            Vector2 maximum = Vector2.Max(first, last);
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        public static bool Intersects(Camera camera, Rect viewport, Rect rectangle, IReadOnlyList<Vector3> worldPolygon)
        {
            var clipped = new List<Vector3>(worldPolygon);
            //カメラ背面の頂点をそのまま投影すると反転した巨大な選択領域になる。
            //視錐台で切ってから投影し、近クリップ面を横切る配置も見えている部分で判定する。
            foreach (Plane plane in GeometryUtility.CalculateFrustumPlanes(camera))
            {
                clipped = Clip(clipped, plane);
                if (clipped.Count == 0)
                {
                    return false;
                }
            }
            var projected = new List<Vector2>(clipped.Count);
            foreach (Vector3 world in clipped)
            {
                Vector3 point = camera.WorldToViewportPoint(world);
                projected.Add(new Vector2(viewport.x + point.x * viewport.width,
                    viewport.y + (1 - point.y) * viewport.height));
            }
            //凸多角形と矩形の分離軸を調べ、頂点を含まない辺同士の交差も拾う。
            if (IsSeparated(projected, rectangle, Vector2.right) || IsSeparated(projected, rectangle, Vector2.up))
            {
                return false;
            }
            for (int edgeSeek = 0; edgeSeek < projected.Count; edgeSeek++)
            {
                Vector2 edge = projected[(edgeSeek + 1) % projected.Count] - projected[edgeSeek];
                if (IsSeparated(projected, rectangle, new Vector2(-edge.y, edge.x)))
                {
                    return false;
                }
            }
            return true;
        }

        private static List<Vector3> Clip(List<Vector3> polygon, Plane plane)
        {
            var result = new List<Vector3>(polygon.Count + 1);
            Vector3 previous = polygon[polygon.Count - 1];
            float previousDistance = plane.GetDistanceToPoint(previous);
            foreach (Vector3 current in polygon)
            {
                float currentDistance = plane.GetDistanceToPoint(current);
                if ((previousDistance >= 0) != (currentDistance >= 0))
                {
                    result.Add(Vector3.LerpUnclamped(previous, current,
                        previousDistance / (previousDistance - currentDistance)));
                }
                if (currentDistance >= 0)
                {
                    result.Add(current);
                }
                previous = current;
                previousDistance = currentDistance;
            }
            return result;
        }

        private static bool IsSeparated(List<Vector2> polygon, Rect rectangle, Vector2 axis)
        {
            float minimum = Vector2.Dot(polygon[0], axis);
            float maximum = minimum;
            foreach (Vector2 point in polygon)
            {
                float projection = Vector2.Dot(point, axis);
                minimum = Mathf.Min(minimum, projection);
                maximum = Mathf.Max(maximum, projection);
            }
            float center = Vector2.Dot(rectangle.center, axis);
            float radius = (Mathf.Abs(axis.x) * rectangle.width + Mathf.Abs(axis.y) * rectangle.height) * 0.5f;
            return maximum < center - radius || minimum > center + radius;
        }
    }
}
