using System;
using SevenSteps.Contracts;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>
    /// The storybook cutscenes from GameAssets.cutscenesJson (design/story/cutscenes.md). None or a broken file logs and
    /// turns cutscenes off; the game goes on (scene-validate stops such a build earlier).
    /// </summary>
    public static class CutsceneLoad
    {
        /// The checked catalog, or null (no cutscenes.json: warning; a failed load check: error).
        public static CutsceneCatalog Catalog(GameAssets assets)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            if (assets.cutscenesJson == null) { Debug.LogWarning("[SevenSteps] GameAssets.cutscenesJson is not set: no cutscenes"); return null; }
            try
            {
                return CutsceneCatalog.FromJson(assets.cutscenesJson.text);
            }
            catch (FormatException e)
            {
                Debug.LogError("[SevenSteps] cutscenes.json failed its load check, cutscenes are off: " + e.Message);
                return null;
            }
        }
    }
}
