using System;
using System.Collections.Generic;

namespace SevenSteps.Game.Flow
{
    /// <summary>What a clear changed for one stage: first clear and the best stars before and after.</summary>
    public readonly struct ClearNote
    {
        /// Global stage index (levels.json order).
        public readonly int Stage;
        public readonly bool FirstClear;
        public readonly int BestBefore, BestAfter;

        public ClearNote(int stage, bool firstClear, int bestBefore, int bestAfter)
        {
            Stage = stage; FirstClear = firstClear; BestBefore = bestBefore; BestAfter = bestAfter;
        }

        public override string ToString() => $"stage {Stage}{(FirstClear ? " first" : "")} {BestBefore}->{BestAfter}";
    }

    /// <summary>
    /// Clears not yet shown on the U5 surface constellation (the first-clear trace; architect 2026-11-12: journey-independent,
    /// not JourneyReturn, whose Pending/Settle life is the pullback's and exists only with -newmap). Pure C#, one per run
    /// (Services, Bootstrap), not saved. GameFlowController records each win; StageSelectController takes a planet's notes
    /// when it opens that surface. Notes are per stage (a journey's Next can clear several stages without the surface); a
    /// stage recorded again before it is taken merges (first clear OR, the first best before, the latest best after).
    /// </summary>
    public sealed class ClearNews
    {
        public const int MaxStars = 3;
        readonly Dictionary<int, ClearNote> notes = new Dictionary<int, ClearNote>();

        public int Count => notes.Count;

        /// A win's change. Nothing new (not a first clear, same best) is ignored.
        public void Record(int stage, bool firstClear, int bestBefore, int bestAfter)
        {
            if (stage < 0) throw new ArgumentOutOfRangeException(nameof(stage), stage, "stage index");
            if (bestBefore < 0 || bestBefore > MaxStars) throw new ArgumentOutOfRangeException(nameof(bestBefore), bestBefore, "0..3");
            if (bestAfter < 0 || bestAfter > MaxStars) throw new ArgumentOutOfRangeException(nameof(bestAfter), bestAfter, "0..3");
            if (bestAfter < bestBefore) throw new ArgumentException($"stage {stage}: best {bestBefore} -> {bestAfter} went down", nameof(bestAfter));
            if (firstClear && bestBefore != 0) throw new ArgumentException($"stage {stage}: a first clear had best {bestBefore} before", nameof(bestBefore));
            if (!firstClear && bestAfter == bestBefore) return;
            if (notes.TryGetValue(stage, out var old))
                notes[stage] = new ClearNote(stage, old.FirstClear || firstClear, old.BestBefore, bestAfter);
            else
                notes[stage] = new ClearNote(stage, firstClear, bestBefore, bestAfter);
        }

        /// Moves the notes of <paramref name="stages"/> (a planet's stages) into <paramref name="into"/> (cleared first),
        /// in the order of <paramref name="stages"/>, and forgets them. True when there was any.
        public bool TryTake(IReadOnlyList<int> stages, List<ClearNote> into)
        {
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            for (int i = 0; i < stages.Count; i++)
                if (notes.TryGetValue(stages[i], out var n)) { into.Add(n); notes.Remove(stages[i]); }
            return into.Count > 0;
        }

        /// Forgets everything (progress reset).
        public void Clear() => notes.Clear();
    }
}
