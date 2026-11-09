using System;
using UnityEngine;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// One chapter (one small planet): the art set, material swaps, lighting and sky for a group of stages
    /// (docs/architecture.md 12.8). Stages pick a chapter with levels.json "chapter" = <see cref="id"/>.
    /// Art fills the asset (Art/&lt;Set&gt;/Chapter_&lt;id&gt;.asset); the game scene applies it when the chapter changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Seven Steps/Chapter Preset", fileName = "Chapter")]
    public sealed class ChapterPreset : ScriptableObject
    {
        [Tooltip("Matches levels.json \"chapter\". Lowercase, no spaces, e.g. moss-ruins.")]
        public string id = "";
        public string displayName = "";

        [Header("Board")]
        [Tooltip("Tile/actor prefabs. Several chapters may share one set and differ only by material overrides.")]
        public TileSet tileSet;
        [Tooltip("Applied to every board instance of this chapter: shared material 'from' is replaced by 'to'.")]
        public MaterialOverride[] materialOverrides = new MaterialOverride[0];

        [Header("Look")]
        public LightingPreset lighting;
        public SkyPreset sky;

        /// The material to use instead of <paramref name="m"/> in this chapter (itself when not overridden).
        public Material Override(Material m)
        {
            if (m == null || materialOverrides == null) return m;
            foreach (var o in materialOverrides)
                if (o.from == m && o.to != null) return o.to;
            return m;
        }

        public bool HasOverrides => materialOverrides != null && materialOverrides.Length > 0;
    }

    [Serializable]
    public struct MaterialOverride
    {
        public Material from;
        public Material to;
    }
}
