using System;
using UnityEngine;

namespace SevenSteps.Game.UI.Planets
{
    /// <summary>
    /// The D2b glyphs generated once and cached (IMPLEMENT.md D2b "generate once and cache"; the review's shapes): a soft
    /// round halo and the four-ray stage star, white so an Image tints them. 128 px, no mipmaps, clamped, not readable.
    /// </summary>
    public static class ConstellationGlyphs
    {
        const int Size = 128;
        static Sprite halo, star;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { halo = null; star = null; }

        /// Soft round glow: alpha (1 - r)^2.2.
        public static Sprite Halo => halo != null ? halo : halo = Make("Constellation halo", (x, y) => Mathf.Pow(Mathf.Clamp01(1 - Mathf.Sqrt(x * x + y * y)), 2.2f));

        /// Four-ray star: |x|^0.6 + |y|^0.6 &lt; 1 with a soft edge.
        public static Sprite Star => star != null ? star : star = Make("Constellation four-ray star", (x, y) => Mathf.Clamp01((1.03f - (Mathf.Pow(Mathf.Abs(x), .6f) + Mathf.Pow(Mathf.Abs(y), .6f))) * 30));

        static Sprite Make(string name, Func<float, float, float> alpha)
        {
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    px[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255 * alpha((x + .5f - Size / 2f) / (Size / 2f), (y + .5f - Size / 2f) / (Size / 2f))));
            t.SetPixels32(px);
            t.Apply(false, true);
            var s = Sprite.Create(t, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            s.name = name;
            s.hideFlags = HideFlags.DontSave;
            return s;
        }
    }
}
