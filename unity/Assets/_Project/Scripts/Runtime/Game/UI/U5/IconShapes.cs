using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenSteps.Game.UI.U5
{
    /// The U5 icons and glyphs (ui-redesign-2026-11-12 EVAL "U5", touch-targets.csv). Each id has a placeholder line
    /// drawing here; Codex art replaces it per id through <see cref="UiIconSet"/>.
    /// docs/ui-u5-kit.md 1: append-only (saved in UiIconSet assets and the art manifest by name).
    public enum UiIcon
    {
        Undo, Restart, Hint, Pause, Play, Settings, Map, Retry, Next, Back, Language, Credits, Info, SoundOn, SoundOff, Motion,
        Close, Keep, Switch,
    }

    /// <summary>
    /// Placeholder icon geometry (pure): each icon is a set of strokes in a -0.5..0.5 box (y up), redrawn from the
    /// review-only U5 concept (feat/ui-concepts ConceptGraphic) as data, so one sprite per icon is rasterised instead of one
    /// Image per stroke. No Unity objects here; <see cref="IconRaster"/> turns it into a texture.
    /// </summary>
    public static class IconShapes
    {
        public readonly struct Stroke
        {
            public readonly Vector2 A, B;
            public Stroke(float ax, float ay, float bx, float by) { A = new Vector2(ax, ay); B = new Vector2(bx, by); }
        }

        /// Strokes of <paramref name="id"/> (at least one, all inside the box).
        public static IReadOnlyList<Stroke> Of(UiIcon id)
        {
            var s = new List<Stroke>();
            void L(float x, float y, float xx, float yy) => s.Add(new Stroke(x, y, xx, yy));
            void Arc(float x, float y, float r, float a, float b)
            {
                int n = Mathf.Max(5, Mathf.CeilToInt(Mathf.Abs(b - a) / 9));
                for (int i = 0; i < n; i++)
                {
                    float u = Mathf.Lerp(a, b, i / (float)n) * Mathf.Deg2Rad, v = Mathf.Lerp(a, b, (i + 1f) / n) * Mathf.Deg2Rad;
                    L(x + Mathf.Cos(u) * r, y + Mathf.Sin(u) * r, x + Mathf.Cos(v) * r, y + Mathf.Sin(v) * r);
                }
            }
            switch (id)
            {
                case UiIcon.Undo: // a half turn back to the left, arrow head down (not a full circle: that is Restart)
                    L(0.25f, -0.24f, 0.25f, -0.04f); Arc(0.01f, -0.04f, 0.24f, 0, 180);
                    L(-0.23f, -0.14f, -0.33f, -0.02f); L(-0.23f, -0.14f, -0.13f, -0.02f); break;
                case UiIcon.Restart:
                case UiIcon.Retry: // full circle arrow
                    Arc(0, 0, 0.27f, -130, 160); L(-0.1f, 0.29f, -0.27f, 0.21f); L(-0.27f, 0.21f, -0.24f, 0.03f); break;
                case UiIcon.Pause: L(-0.1f, -0.2f, -0.1f, 0.2f); L(0.1f, -0.2f, 0.1f, 0.2f); break;
                case UiIcon.Play: L(-0.12f, -0.22f, 0.22f, 0); L(0.22f, 0, -0.12f, 0.22f); L(-0.12f, 0.22f, -0.12f, -0.22f); break;
                case UiIcon.Next: L(-0.1f, -0.22f, 0.13f, 0); L(0.13f, 0, -0.1f, 0.22f); break;
                case UiIcon.Back: L(0.1f, -0.22f, -0.13f, 0); L(-0.13f, 0, 0.1f, 0.22f); break;
                case UiIcon.Hint: // bulb
                    Arc(0, 0.07f, 0.18f, -55, 235); L(-0.1f, -0.12f, -0.07f, -0.25f); L(-0.07f, -0.25f, 0.07f, -0.25f); L(0.07f, -0.25f, 0.1f, -0.12f); break;
                case UiIcon.Settings:
                    Arc(0, 0, 0.12f, 0, 360); Arc(0, 0, 0.24f, 0, 360);
                    for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; L(Mathf.Cos(a) * 0.24f, Mathf.Sin(a) * 0.24f, Mathf.Cos(a) * 0.33f, Mathf.Sin(a) * 0.33f); }
                    break;
                case UiIcon.Map: // the Dipper: four ladle stars and the handle
                {
                    var p = new[]
                    {
                        new Vector2(-0.36f, 0.2f), new Vector2(-0.36f, -0.02f), new Vector2(-0.14f, -0.06f), new Vector2(-0.12f, 0.16f),
                        new Vector2(0.06f, 0.12f), new Vector2(0.2f, -0.04f), new Vector2(0.36f, -0.2f),
                    };
                    for (int i = 1; i < p.Length; i++) L(p[i - 1].x, p[i - 1].y, p[i].x, p[i].y);
                    L(p[3].x, p[3].y, p[0].x, p[0].y);
                    foreach (var q in p) Arc(q.x, q.y, 0.035f, 0, 360); // the stars
                    break;
                }
                case UiIcon.Language: // a globe: rim, equator, two latitudes and the meridian ellipse
                    Arc(0, 0, 0.29f, 0, 360); L(-0.29f, 0, 0.29f, 0); L(-0.24f, 0.15f, 0.24f, 0.15f); L(-0.24f, -0.15f, 0.24f, -0.15f);
                    for (int i = 0; i < 24; i++)
                    {
                        float u = i * Mathf.PI / 12, v = (i + 1) * Mathf.PI / 12;
                        L(Mathf.Cos(u) * 0.12f, Mathf.Sin(u) * 0.29f, Mathf.Cos(v) * 0.12f, Mathf.Sin(v) * 0.29f);
                    }
                    break;
                case UiIcon.Info: Arc(0, 0, 0.29f, 0, 360); L(0, -0.17f, 0, 0.05f); L(0, 0.15f, 0, 0.17f); break;
                case UiIcon.SoundOn:
                case UiIcon.SoundOff:
                    L(-0.26f, -0.09f, -0.12f, -0.09f); L(-0.12f, -0.09f, 0.03f, -0.23f); L(0.03f, -0.23f, 0.03f, 0.23f);
                    L(0.03f, 0.23f, -0.12f, 0.09f); L(-0.12f, 0.09f, -0.26f, 0.09f); L(-0.26f, 0.09f, -0.26f, -0.09f);
                    if (id == UiIcon.SoundOn) Arc(0.03f, 0, 0.22f, -50, 50);
                    else { L(0.14f, -0.1f, 0.32f, 0.1f); L(0.14f, 0.1f, 0.32f, -0.1f); }
                    break;
                case UiIcon.Credits: // a five-point star outline
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i * Mathf.PI / 5, b = (i + 1) * Mathf.PI / 5, ra = i % 2 == 0 ? 0.32f : 0.14f, rb = i % 2 == 0 ? 0.14f : 0.32f;
                        L(Mathf.Sin(a) * ra, Mathf.Cos(a) * ra - 0.02f, Mathf.Sin(b) * rb, Mathf.Cos(b) * rb - 0.02f);
                    }
                    break;
                case UiIcon.Close: L(-0.17f, -0.17f, 0.17f, 0.17f); L(-0.17f, 0.17f, 0.17f, -0.17f); break;
                case UiIcon.Motion: Arc(0, 0, 0.28f, 15, 300); L(-0.14f, -0.05f, 0, 0.14f); L(0, 0.14f, 0.14f, -0.05f); break;
                case UiIcon.Keep: // standing stone on its base
                    L(-0.19f, -0.23f, 0.19f, -0.23f); L(-0.16f, -0.22f, -0.12f, 0.1f); L(-0.12f, 0.1f, 0.11f, 0.1f); L(0.11f, 0.1f, 0.16f, -0.22f);
                    Arc(0, 0.22f, 0.09f, 0, 360); L(-0.25f, -0.3f, 0.25f, -0.3f); break;
                case UiIcon.Switch: // a brother and the arrow to the next
                    Arc(-0.1f, 0.15f, 0.09f, 0, 360); L(-0.1f, 0.04f, -0.1f, -0.2f); L(-0.1f, -0.06f, -0.22f, -0.17f); L(-0.1f, -0.06f, 0.03f, -0.17f);
                    L(0.05f, 0.15f, 0.3f, 0.15f); L(0.21f, 0.24f, 0.3f, 0.15f); L(0.3f, 0.15f, 0.21f, 0.06f); break;
                default: throw new ArgumentOutOfRangeException(nameof(id), id, "no placeholder drawing");
            }
            return s;
        }

        /// Distance from <paramref name="p"/> to the nearest stroke (box units).
        public static float Distance(IReadOnlyList<Stroke> strokes, Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var s in strokes)
            {
                var d = s.B - s.A;
                float t = d.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - s.A, d) / d.sqrMagnitude) : 0;
                best = Mathf.Min(best, Vector2.Distance(p, s.A + d * t));
            }
            return best;
        }
    }
}
