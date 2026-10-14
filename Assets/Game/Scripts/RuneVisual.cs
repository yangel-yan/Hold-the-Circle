using UnityEngine;
using UnityEngine.Rendering;

namespace HoldTheCircle
{
    public static class RuneVisual
    {
        private static Material s_ink;

        public static LineRenderer Stroke(Transform parent, string name, Vector3[] points,
            Color color, float width = 0.012f, bool loop = false)
        {
            if (s_ink == null)
            {
                s_ink = RiteGlyph.RibbonMaterial;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = s_ink;
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            return line;
        }

        public static LineRenderer Arc(Transform parent, string name, float radius,
            float start, float sweep, Color color, float width = 0.012f)
        {
            int count = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(sweep) / 5f));
            var points = new Vector3[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float angle = (start + sweep * i / count) * Mathf.Deg2Rad;
                points[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            }
            return Stroke(parent, name, points, color, width);
        }
    }
}
