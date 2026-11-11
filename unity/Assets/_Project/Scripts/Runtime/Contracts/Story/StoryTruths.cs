using System;
using System.Collections.Generic;
using SevenSteps.Core;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Story facts derived from the stage list and the clear records (docs/architecture.md 16.6, A4 / A9 / A10). Pure,
    /// nothing saved. Chapter boundaries come from the data, never from stage numbers: a chapter's finale is its last
    /// stage, except in the chapter that holds the game's last stage, where the finale is the stage before it (the last
    /// stage is the ending). Clears are read by stage name (ProgressData, 11.3.1).
    /// <paramref name="chapterKeys"/> is one key per stage in levels.json order (LevelCatalog.ChapterKeyOf).
    /// </summary>
    public static class StoryTruths
    {
        /// 0-based finale stage of a chapter, -1 when the chapter has no finale (no stages, or only the last stage).
        public static int FinaleStage(IReadOnlyList<string> chapterKeys, string chapterId)
        {
            Check(chapterKeys);
            if (string.IsNullOrEmpty(chapterId)) throw new ArgumentException("chapter id is empty", nameof(chapterId));
            int last = -1;
            for (int i = 0; i < chapterKeys.Count; i++) if (chapterKeys[i] == chapterId) last = i;
            if (last < 0) return -1;
            if (last != chapterKeys.Count - 1) return last;
            return last > 0 && chapterKeys[last - 1] == chapterId ? last - 1 : -1;
        }

        /// The stage is its chapter's finale (show the finale line on its win).
        public static bool IsFinaleStage(IReadOnlyList<string> chapterKeys, int stage)
        {
            Check(chapterKeys);
            if (stage < 0 || stage >= chapterKeys.Count) throw new ArgumentOutOfRangeException(nameof(stage), stage, $"0..{chapterKeys.Count - 1}");
            return !string.IsNullOrEmpty(chapterKeys[stage]) && FinaleStage(chapterKeys, chapterKeys[stage]) == stage;
        }

        /// The game's last stage: its win plays the ending.
        public static bool IsEndingStage(IReadOnlyList<string> chapterKeys, int stage)
        {
            Check(chapterKeys);
            return stage == chapterKeys.Count - 1;
        }

        /// The chapter's finale stage has been cleared.
        public static bool FinaleCleared(IReadOnlyList<Level> levels, IReadOnlyList<string> chapterKeys, ProgressData progress, string chapterId)
        {
            Check(levels, chapterKeys, progress);
            int k = FinaleStage(chapterKeys, chapterId);
            return k >= 0 && progress.IsCleared(levels[k]);
        }

        /// Chapters whose finale is cleared ("truths", carvings). All collected = Truths == chapterIds.Count (A10).
        public static int Truths(IReadOnlyList<Level> levels, IReadOnlyList<string> chapterKeys, ProgressData progress, IReadOnlyList<string> chapterIds)
        {
            if (chapterIds == null) throw new ArgumentNullException(nameof(chapterIds));
            int n = 0;
            foreach (var id in chapterIds) if (FinaleCleared(levels, chapterKeys, progress, id)) n++;
            return n;
        }

        /// Every stage of the chapter is cleared ("constellation"). False for a chapter with no stages.
        public static bool ChapterComplete(IReadOnlyList<Level> levels, IReadOnlyList<string> chapterKeys, ProgressData progress, string chapterId)
        {
            Check(levels, chapterKeys, progress);
            bool any = false;
            for (int i = 0; i < chapterKeys.Count; i++)
            {
                if (chapterKeys[i] != chapterId) continue;
                any = true;
                if (!progress.IsCleared(levels[i])) return false;
            }
            return any;
        }

        static void Check(IReadOnlyList<string> chapterKeys)
        {
            if (chapterKeys == null) throw new ArgumentNullException(nameof(chapterKeys));
        }

        static void Check(IReadOnlyList<Level> levels, IReadOnlyList<string> chapterKeys, ProgressData progress)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            Check(chapterKeys);
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (levels.Count != chapterKeys.Count) throw new ArgumentException($"{levels.Count} stages but {chapterKeys.Count} chapter keys", nameof(chapterKeys));
        }
    }
}
