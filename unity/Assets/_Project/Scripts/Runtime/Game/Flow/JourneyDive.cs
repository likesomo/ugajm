using System;

namespace SevenSteps.Game.Flow
{
    /// <summary>
    /// Map → board dive hand-off (docs/stage-select-transitions.md 1). Pure C#, one per run (Services). The Title scene
    /// <see cref="Begin"/>s it before committing the stage choice; the Game scene <see cref="Arrive"/>s on the same stage and
    /// plays the second half of the dive (camera settles from the planet onto the board, the board rises).
    /// </summary>
    public sealed class JourneyDive
    {
        public bool Pending { get; private set; }
        /// 0-based stage being dived into; -1 when none.
        public int Stage { get; private set; } = -1;

        public void Begin(int stage)
        {
            if (stage < 0) throw new ArgumentOutOfRangeException(nameof(stage), stage, "JourneyDive: stage must be 0 or more");
            if (Pending) throw new InvalidOperationException($"JourneyDive: stage {Stage} is still pending");
            Pending = true;
            Stage = stage;
        }

        /// The dive was cancelled (Esc) or its commit refused: nothing arrives.
        public void Cancel()
        {
            if (!Pending) throw new InvalidOperationException("JourneyDive: nothing to cancel");
            Clear();
        }

        /// The Game scene entered <paramref name="stage"/>: true when that is the dive's arrival (then it is no longer pending).
        /// A pending dive into another stage is a broken hand-off and throws.
        public bool Arrive(int stage)
        {
            if (!Pending) return false;
            if (stage != Stage) throw new InvalidOperationException($"JourneyDive: dived into stage {Stage} but the Game scene entered {stage}");
            Clear();
            return true;
        }

        void Clear()
        {
            Pending = false;
            Stage = -1;
        }
    }
}
