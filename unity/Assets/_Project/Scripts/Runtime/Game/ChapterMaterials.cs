using SevenSteps.Contracts;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>
    /// ChapterPreset.materialOverrides on an instance (docs/architecture.md 12.8): every renderer's shared materials
    /// 'from' → 'to', replaced as an array once (no per-frame work, no renderer.material instance copies). Used for board
    /// pieces (BoardView) and the chapter's sky and planet (ChapterStage), so a chapter never keeps another's material
    /// (frost: Environment_frost instead of the mossy BaseAtlas).
    /// </summary>
    public static class ChapterMaterials
    {
        /// <param name="chapter">null or no overrides = nothing to do.</param>
        public static void Apply(GameObject root, ChapterPreset chapter)
        {
            if (root == null || chapter == null || !chapter.HasOverrides) return;
            foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = rend.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var to = chapter.Override(mats[i]);
                    if (to != mats[i]) { mats[i] = to; changed = true; }
                }
                if (changed) rend.sharedMaterials = mats;
            }
        }
    }
}
