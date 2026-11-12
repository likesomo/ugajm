using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenSteps.Game.UI.U5
{
    /// <summary>
    /// The U5 icon sprites (one swappable asset, lead 2026-11-12): an entry per <see cref="UiIcon"/> whose sprite Codex art
    /// fills in; an id with no sprite (or no asset at all) draws the placeholder from <see cref="IconShapes"/>. White
    /// sprites, tinted by the kit.
    /// </summary>
    [CreateAssetMenu(menuName = "Seven Steps/U5 Icon Set", fileName = "UiIconSet")]
    public sealed class UiIconSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public UiIcon id;
            [Tooltip("White icon, about 40 inside a 48 box; Codex art (u5-icons.json manifest)")]
            public Sprite sprite;
        }

        public Entry[] icons = Array.Empty<Entry>();

        [Header("Not icons (docs/ui-u5-kit.md 1.1; null = the kit's flat stand-in)")]
        [Tooltip("caption-plate: the FocusPlate background (9-sliced: its border counts as plate padding)")]
        public Sprite captionPlate;
        [Tooltip("scarf-dot: one HUD brother dot, white, tinted with the scarf colour")]
        public Sprite scarfDot;
        [Tooltip("star-stone-lit / star-stone-unlit: the clear screen's world star stones")]
        public Sprite starStoneLit, starStoneUnlit;
        [Tooltip("step-star / step-star-link: step stars C (core's StepSkyView reads them here)")]
        public Sprite stepStar, stepStarLink;

        /// Manifest id of an icon (u5-icons.json, docs 1.1): lower-case kebab of the enum name (SoundOn = "sound-on").
        public static string ManifestId(UiIcon id)
        {
            Check(id);
            var name = id.ToString();
            var sb = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > 0) sb.Append('-');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// The icon of a manifest id; false for ids that are not icons (caption-plate, ...) or unknown.
        public static bool TryIcon(string manifestId, out UiIcon id)
        {
            foreach (UiIcon v in Enum.GetValues(typeof(UiIcon)))
                if (ManifestId(v) == manifestId) { id = v; return true; }
            id = default;
            return false;
        }

        /// Manifest ids of the named non-icon sprites, in field order.
        public static readonly string[] SpriteIds = { "caption-plate", "scarf-dot", "star-stone-lit", "star-stone-unlit", "step-star", "step-star-link" };

        /// Sets a non-icon sprite by manifest id; false when the id is not one of <see cref="SpriteIds"/>.
        public bool SetSprite(string manifestId, Sprite sprite)
        {
            switch (manifestId)
            {
                case "caption-plate": captionPlate = sprite; return true;
                case "scarf-dot": scarfDot = sprite; return true;
                case "star-stone-lit": starStoneLit = sprite; return true;
                case "star-stone-unlit": starStoneUnlit = sprite; return true;
                case "step-star": stepStar = sprite; return true;
                case "step-star-link": stepStarLink = sprite; return true;
                default: return false;
            }
        }

        /// Sets the art sprite of <paramref name="id"/> (manifest import), replacing an existing entry.
        public void SetIcon(UiIcon id, Sprite sprite)
        {
            Check(id);
            for (int i = 0; i < icons.Length; i++)
                if (icons[i].id == id) { icons[i].sprite = sprite; return; }
            Array.Resize(ref icons, icons.Length + 1);
            icons[icons.Length - 1] = new Entry { id = id, sprite = sprite };
        }

        /// The art sprite of <paramref name="id"/>, else the placeholder.
        public Sprite Get(UiIcon id) => Art(id) ?? IconRaster.Placeholder(id);

        /// The art sprite of <paramref name="id"/>, or null (placeholder in use).
        public Sprite Art(UiIcon id)
        {
            Check(id);
            foreach (var e in icons) if (e.id == id && e.sprite != null) return e.sprite;
            return null;
        }

        /// Every id still on its placeholder (scene-validate warns; 0 before a release build, docs/ui-u5-kit.md 1).
        public List<UiIcon> Missing()
        {
            var list = new List<UiIcon>();
            foreach (UiIcon id in Enum.GetValues(typeof(UiIcon))) if (Art(id) == null) list.Add(id);
            return list;
        }

        public static void Check(UiIcon id)
        {
            if (!Enum.IsDefined(typeof(UiIcon), id)) throw new ArgumentOutOfRangeException(nameof(id), id, "not a UiIcon");
        }

        /// <see cref="Get"/> on <paramref name="set"/>, or the placeholder when there is no set.
        public static Sprite Of(UiIconSet set, UiIcon id) => set != null ? set.Get(id) : IconRaster.Placeholder(id);

        void OnValidate()
        {
            var seen = new HashSet<UiIcon>();
            foreach (var e in icons)
                if (!seen.Add(e.id)) Debug.LogError($"[SevenSteps] UiIconSet {name}: {e.id} is listed twice", this);
        }
    }

    /// Rasterises <see cref="IconShapes"/> once per id into a white anti-aliased sprite (no texture asset needed).
    public static class IconRaster
    {
        /// Texels per side: twice the 48 px button, so it stays sharp up to 96 px.
        public const int Size = 96;
        /// Half the line width in box units: about a 3 px line on a 48 px icon.
        public const float HalfWidth = 0.032f;

        static readonly Dictionary<UiIcon, Sprite> cache = new Dictionary<UiIcon, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay() => cache.Clear();

        public static Sprite Placeholder(UiIcon id)
        {
            UiIconSet.Check(id);
            if (cache.TryGetValue(id, out var s) && s != null) return s;
            var strokes = IconShapes.Of(id);
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Icon " + id };
            var px = new Color32[Size * Size];
            float texel = 1f / Size;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var p = new Vector2((x + 0.5f) * texel - 0.5f, (y + 0.5f) * texel - 0.5f);
                float cover = Mathf.Clamp01((HalfWidth - IconShapes.Distance(strokes, p)) / texel + 0.5f);
                px[y * Size + x] = new Color32(255, 255, 255, (byte)(cover * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, false); // kept readable (36 KB each): the review sheet samples it
            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100);
            s.name = tex.name;
            cache[id] = s;
            return s;
        }
    }
}
