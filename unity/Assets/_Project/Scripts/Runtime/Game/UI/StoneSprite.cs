using UnityEngine;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// A carved slab of stone as a white sprite (U5 title, U1 grammar: the name on a stone, the play stone): an
    /// eight-sided outline with a lighter upper-left edge and a darker lower-right bevel, so a flat tint reads as a slab.
    /// Made once in code (no texture asset); the colour is the Image's. UiLayoutCheck measures such a plate by its tint
    /// (no PNG to sample), which is the face colour.
    /// </summary>
    public static class StoneSprite
    {
        public const int Width = 160, Height = 96;
        /// Outline in box units (-0.5..0.5), counter-clockwise, as the U5 concept's title stone.
        static readonly Vector2[] Outline =
        {
            new Vector2(-0.42f, -0.5f), new Vector2(0.37f, -0.5f), new Vector2(0.5f, -0.32f), new Vector2(0.48f, 0.33f),
            new Vector2(0.32f, 0.5f), new Vector2(-0.35f, 0.48f), new Vector2(-0.5f, 0.29f), new Vector2(-0.49f, -0.32f),
        };

        static Sprite cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay() => cached = null;

        public static Sprite Get()
        {
            if (cached != null) return cached;
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Stone slab" };
            var px = new Color32[Width * Height];
            float bevel = 0.07f;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var p = new Vector2((x + 0.5f) / Width - 0.5f, (y + 0.5f) / Height - 0.5f);
                float d = SignedDistance(p); // < 0 inside, box units (x scaled to the width)
                float cover = Mathf.Clamp01(-d * Width + 0.5f);
                // Bevel: the band near the edge is lighter toward the upper left, darker toward the lower right.
                float edge = Mathf.Clamp01(1f + d / bevel);
                float side = Mathf.Clamp01(0.5f + (p.x - p.y) * 1.2f); // 0 upper left .. 1 lower right
                float shade = 1f - edge * Mathf.Lerp(-0.12f, 0.35f, side);
                byte v = (byte)(Mathf.Clamp01(shade) * 255);
                px[y * Width + x] = new Color32(v, v, v, (byte)(cover * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            cached = Sprite.Create(tex, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), 100);
            cached.name = tex.name;
            return cached;
        }

        /// Distance to the outline (negative inside), x and y in box units.
        static float SignedDistance(Vector2 p)
        {
            float best = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = Outline.Length - 1; i < Outline.Length; j = i++)
            {
                Vector2 a = Outline[j], b = Outline[i];
                if ((b.y > p.y) != (a.y > p.y) && p.x < (a.x - b.x) * (p.y - b.y) / (a.y - b.y) + b.x) inside = !inside;
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
            }
            return inside ? -best : best;
        }
    }
}
