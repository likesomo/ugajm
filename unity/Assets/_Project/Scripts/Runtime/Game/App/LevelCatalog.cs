using System;
using System.Linq;
using SevenSteps.Core;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>Stages from levels.json (docs/architecture.md 6). Parsed once per app run and shared by the Title and Game scenes.</summary>
    public static class LevelCatalog
    {
        static Level[] cached;
        static TextAsset source;
        static int[] minBodies;
        static string[] chapterIds;

        /// Domain reload is disabled in the editor: parse levels.json again on every Play so edits to it show up.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetForPlay()
        {
            cached = null;
            source = null;
            minBodies = null;
            chapterIds = null;
        }

        // levels.json "chapter" (docs/architecture.md 6.4). Read here, not through Core's LevelDto: the rules do not know chapters.
        [Serializable] sealed class ChapterFieldDto { public string chapter; }
        [Serializable] sealed class StageChaptersDto { public ChapterFieldDto[] levels; }

        public static Level[] Load(GameAssets assets)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            if (assets.levelsJson == null) throw new MissingReferenceException("[SevenSteps] GameAssets.levelsJson is not set");
            if (cached != null && source == assets.levelsJson) return cached;
            string json = assets.levelsJson.text;
            var file = JsonUtility.FromJson<LevelFileDto>(json);
            cached = file.levels.Select(d => d.ToLevel()).ToArray();
            minBodies = file.levels.Select(d => d.expected != null ? d.expected.minBodies : 0).ToArray();
            var chapters = JsonUtility.FromJson<StageChaptersDto>(json).levels;
            chapterIds = cached.Select((_, k) => chapters != null && k < chapters.Length ? chapters[k].chapter ?? "" : "").ToArray();
            source = assets.levelsJson;
            return cached;
        }

        /// Fewest bodies the JS solver needs for a stage (levels.json expected.minBodies), 0 if unknown. Call after Load.
        public static int MinBodies(int stage)
        {
            return minBodies != null && stage >= 0 && stage < minBodies.Length ? minBodies[stage] : 0;
        }

        /// Chapter id of a stage (levels.json "chapter", e.g. "moss"), "" when the stage has none (= first chapter, 6.4).
        /// Call after Load; a stage outside levels.json throws.
        public static string ChapterIdOf(int stage)
        {
            if (chapterIds == null) throw new InvalidOperationException("LevelCatalog.ChapterIdOf before Load");
            if (stage < 0 || stage >= chapterIds.Length) throw new ArgumentOutOfRangeException(nameof(stage), stage, $"stage must be 0..{chapterIds.Length - 1}");
            return chapterIds[stage];
        }
    }
}
