using System;

namespace SevenSteps.Game.Flow
{
    /// <summary>Where a cleared stage leads (docs/stage-select-transitions.md).</summary>
    public enum ClearExit
    {
        /// The result panel (journey off, or the last stage of all: its [끝까지 보기] opens the ending).
        Result,
        /// The next stage of the same planet rises beside this one (advance transition, no menu).
        Advance,
        /// The planet's last stage: pull back to the star map, the new trail lights (planet complete).
        PlanetComplete,
    }

    /// <summary>Pure rules of the journey flow. Chapter keys are levels.json "chapter" ("" = first chapter).</summary>
    public static class JourneyRoute
    {
        public static ClearExit After(bool journeyOn, int stage, int stageCount, Func<int, string> chapterOf)
        {
            if (stageCount < 1) throw new ArgumentOutOfRangeException(nameof(stageCount), stageCount, "at least one stage");
            if (stage < 0 || stage >= stageCount) throw new ArgumentOutOfRangeException(nameof(stage), stage, $"stage must be 0..{stageCount - 1}");
            if (chapterOf == null) throw new ArgumentNullException(nameof(chapterOf));
            if (!journeyOn || stage == stageCount - 1) return ClearExit.Result;
            return (chapterOf(stage) ?? "") == (chapterOf(stage + 1) ?? "") ? ClearExit.Advance : ClearExit.PlanetComplete;
        }
    }
}
