using System;
using System.Linq;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>
    /// Builds the run's <see cref="StoryBook"/> from GameAssets.storyJson (docs/architecture.md 16.3, A8). A missing or
    /// broken story.json logs and turns the story off; the game goes on (scene-validate stops such a build earlier).
    /// </summary>
    public static class StoryLoad
    {
        /// <param name="devRun">Replay, smoke, flow / title check: a fresh progress in memory, never saved.</param>
        public static StoryBook FromAssets(GameAssets assets, SaveSystem save, bool devRun)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            if (save == null) throw new ArgumentNullException(nameof(save));
            var catalog = Catalog(assets);
            if (catalog == null) return StoryBook.Off();
            var levels = LevelCatalog.Load(assets);
            var keys = Enumerable.Range(0, levels.Length).Select(LevelCatalog.ChapterKeyOf).ToArray();
            return new StoryBook(catalog, devRun ? new StoryProgress() : save.Story, devRun ? null : (Action)save.SaveStory,
                id => StoryTruths.FinaleCleared(levels, keys, save.Progress, id));
        }

        /// story.json checked against chapters.json, or null (no story.json, no chapters, or a failed load check: logged).
        public static StoryCatalog Catalog(GameAssets assets)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            if (assets.storyJson == null) { Debug.LogWarning("[SevenSteps] GameAssets.storyJson is not set: no story lines"); return null; }
            LevelCatalog.Load(assets);
            var ids = LevelCatalog.Chapters.Select(c => c.id).ToArray();
            if (ids.Length == 0) { Debug.LogWarning("[SevenSteps] No chapters.json: no story lines"); return null; }
            try
            {
                return StoryCatalog.FromJson(assets.storyJson.text, ids);
            }
            catch (Exception e)
            {
                Debug.LogError("[SevenSteps] story.json failed its load check, story lines are off: " + e.Message);
                return null;
            }
        }
    }
}
