using System.Collections.Generic;
using UnityEngine;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Star trail look of one planet (docs/architecture.md 15.5): the stone variation of the seven planets. Art fills the
    /// asset (Art/&lt;Set&gt;/StarTrail_&lt;id&gt;.asset) and ChapterPreset.starTrail points at it; no asset = <see cref="Default"/>.
    /// The view (gameplay-ui) passes the shader numbers as globals with <see cref="SetGlobals"/> when the chapter changes and
    /// uses the seconds for its fades. Brother colours are not here: they are the HUD's (15.1 _StarTrailColors).
    /// Ranges: <see cref="Validate"/> (tileset-validate).
    /// </summary>
    [CreateAssetMenu(menuName = "Seven Steps/Star Trail Preset", fileName = "StarTrail")]
    public sealed class StarTrailPreset : ScriptableObject
    {
        [Header("Footprint stars (fraction of a cell; step 1 → 6)")]
        public float dotSizeMin = 0.035f, dotSizeMax = 0.065f;
        public float opacityMin = 0.22f, opacityMax = 0.42f;

        [Header("Glow (before tone mapping)")]
        public float emission = 0.8f;
        [Tooltip("Seventh-step awakening peak, for awakenSeconds only. >= emission.")]
        public float awakenEmission = 1.2f;

        [Header("Constellation lines and marks (fraction of a cell)")]
        public float lineWidth = 0.04f;
        [Range(0, 1)] public float lineOpacity = 0.35f;
        [Tooltip("Star on a KEEP statue's top.")]
        public float statueStarSize = 0.12f;
        [Tooltip("SWITCH baton seam width on the new start cell.")]
        public float seamWidth = 0.06f;
        [Tooltip("Sunken star of a brother who became a bridge: 0 = like a footprint, 1 = deepest / darkest.")]
        [Range(0, 1)] public float bridgeSink = 0.5f;

        [Header("Stone variation")]
        [Tooltip("Engraving depth of the Dipper glyph and lines, 0..1.")]
        [Range(0, 1)] public float engrave = 0.5f;
        [Tooltip("Tints the brother colours toward the planet's stone; alpha = amount (<= 0.5 keeps brothers apart).")]
        public Color stoneTint = new Color(1f, 1f, 1f, 0f);

        [Header("Planet variation (view)")]
        [Tooltip("Sparks at the seventh step / reset, shared pool. 0 = none (lowest option).")]
        public int particleCount = 6;
        [Tooltip("Star shape in the shared mask atlas (art), 0..3.")]
        public int shapeMask;

        [Header("Timing (seconds, view)")]
        public float awakenSeconds = 0.18f;
        public float undoSeconds = 0.12f;
        public float failSeconds = 0.20f;
        public float restartSeconds = 0.25f;
        public float finaleSeconds = 1.2f;

        /// Global float4s (docs 15.5).
        public const string StyleAName = "_StarTrailPresetA", StyleBName = "_StarTrailPresetB", StyleCName = "_StarTrailPresetC", TintName = "_StarTrailTint";
        public static readonly int StyleAId = Shader.PropertyToID(StyleAName), StyleBId = Shader.PropertyToID(StyleBName),
            StyleCId = Shader.PropertyToID(StyleCName), TintId = Shader.PropertyToID(TintName);

        static StarTrailPreset fallback;
        /// The built-in values (an instance with the field defaults), for a chapter without a style asset.
        public static StarTrailPreset Default
        {
            get
            {
                if (fallback == null) { fallback = CreateInstance<StarTrailPreset>(); fallback.hideFlags = HideFlags.HideAndDontSave; fallback.name = "StarTrail (default)"; }
                return fallback;
            }
        }

        /// A: (dotSizeMin, dotSizeMax, opacityMin, opacityMax). B: (emission, awakenEmission, lineWidth, lineOpacity).
        /// C: (statueStarSize, seamWidth, bridgeSink, engrave). Tint: stoneTint (linear rgb, a = amount).
        public Vector4 StyleA => new Vector4(dotSizeMin, dotSizeMax, opacityMin, opacityMax);
        public Vector4 StyleB => new Vector4(emission, awakenEmission, lineWidth, lineOpacity);
        public Vector4 StyleC => new Vector4(statueStarSize, seamWidth, bridgeSink, engrave);

        /// Sets the four style globals; null = <see cref="Default"/>.
        public static void SetGlobals(StarTrailPreset s)
        {
            if (s == null) s = Default;
            Shader.SetGlobalVector(StyleAId, s.StyleA);
            Shader.SetGlobalVector(StyleBId, s.StyleB);
            Shader.SetGlobalVector(StyleCId, s.StyleC);
            var t = QualitySettings.activeColorSpace == ColorSpace.Linear ? s.stoneTint.linear : s.stoneTint;
            Shader.SetGlobalVector(TintId, new Vector4(t.r, t.g, t.b, s.stoneTint.a));
        }

        /// Range problems ("field: why"), empty when the asset is usable. Readability limits from design/art/star-trail.md.
        public List<string> Validate()
        {
            var p = new List<string>();
            void Range(string name, float v, float lo, float hi) { if (!(v >= lo && v <= hi)) p.Add($"{name} {v:0.###} not in {lo}..{hi}"); }
            Range("dotSizeMin", dotSizeMin, 0.01f, 0.2f);
            Range("dotSizeMax", dotSizeMax, 0.01f, 0.2f);
            if (dotSizeMax < dotSizeMin) p.Add("dotSizeMax < dotSizeMin");
            Range("opacityMin", opacityMin, 0f, 1f);
            Range("opacityMax", opacityMax, 0f, 1f);
            if (opacityMax < opacityMin) p.Add("opacityMax < opacityMin");
            Range("emission", emission, 0f, 1.5f);
            Range("awakenEmission", awakenEmission, 0f, 1.5f);
            if (awakenEmission < emission) p.Add("awakenEmission < emission");
            Range("lineWidth", lineWidth, 0f, 0.15f);
            Range("lineOpacity", lineOpacity, 0f, 1f);
            Range("statueStarSize", statueStarSize, 0f, 0.4f);
            Range("seamWidth", seamWidth, 0f, 0.2f);
            Range("bridgeSink", bridgeSink, 0f, 1f);
            Range("engrave", engrave, 0f, 1f);
            Range("stoneTint.a", stoneTint.a, 0f, 0.5f);
            Range("awakenSeconds", awakenSeconds, 0f, 2f);
            Range("undoSeconds", undoSeconds, 0f, 2f);
            Range("failSeconds", failSeconds, 0f, 2f);
            Range("restartSeconds", restartSeconds, 0f, 2f);
            Range("finaleSeconds", finaleSeconds, 0f, 5f);
            Range("particleCount", particleCount, 0, 12);
            Range("shapeMask", shapeMask, 0, 3);
            return p;
        }
    }
}
