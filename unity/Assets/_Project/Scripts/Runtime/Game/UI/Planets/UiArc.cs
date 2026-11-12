using System;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.Planets
{
    /// <summary>
    /// A ring arc as one UI mesh (the constellation nodes' rims, scarf arcs and focus accents, D2b): centre = the rect's
    /// centre, <see cref="Radius"/> to the stroke's middle, angles in degrees anticlockwise from +x (screen up = +y), as the
    /// review's arcs (IMPLEMENT.md D2b) once their y flip is undone by the caller. Not a raycast target.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiArc : MaskableGraphic
    {
        float radius = 27, width = 1.2f, from, to = 360;

        public float Radius => radius;
        public float Width => width;
        public float From => from;
        public float To => to;

        public static UiArc Create(Transform parent, string name, float radius, float width, float fromDeg, float toDeg, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = Vector2.one * (2 * (radius + width));
            var arc = go.AddComponent<UiArc>();
            arc.raycastTarget = false;
            arc.color = color;
            arc.Set(radius, width, fromDeg, toDeg);
            return arc;
        }

        public void Set(float radius, float width, float fromDeg, float toDeg)
        {
            if (radius <= 0 || width <= 0) throw new ArgumentOutOfRangeException(nameof(radius), $"radius {radius}, width {width}");
            if (toDeg <= fromDeg || toDeg - fromDeg > 360.001f) throw new ArgumentOutOfRangeException(nameof(toDeg), $"{fromDeg}..{toDeg}");
            this.radius = radius; this.width = width; from = fromDeg; to = toDeg;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int n = Mathf.Max(2, Mathf.CeilToInt((to - from) / 6f));
            float inner = radius - width / 2, outer = radius + width / 2;
            var c = (Color32)color;
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)n) * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(d * inner, c, Vector4.zero);
                vh.AddVert(d * outer, c, Vector4.zero);
                if (i == 0) continue;
                int k = i * 2;
                vh.AddTriangle(k - 2, k - 1, k + 1);
                vh.AddTriangle(k - 2, k + 1, k);
            }
        }
    }
}
