using System;

namespace SevenSteps.Game.Flow
{
    /// <summary>
    /// What one attempt at a stage used, for the 3-star judgement (docs/architecture.md 6.5, Core Stars.Of).
    /// An attempt runs from LoadStage (select, next, again, [ ]) to the clear; R inside it stays the same attempt.
    /// </summary>
    public readonly struct AttemptCounts
    {
        /// H presses in this attempt.
        public readonly int Hints;
        /// Undos that went back a state (an Undo event).
        public readonly int Undos;
        /// Restarts from a state that was not the stage start.
        public readonly int Restarts;

        public AttemptCounts(int hints, int undos, int restarts)
        {
            if (hints < 0) throw new ArgumentOutOfRangeException(nameof(hints), hints, "count");
            if (undos < 0) throw new ArgumentOutOfRangeException(nameof(undos), undos, "count");
            if (restarts < 0) throw new ArgumentOutOfRangeException(nameof(restarts), restarts, "count");
            Hints = hints;
            Undos = undos;
            Restarts = restarts;
        }

        public AttemptCounts WithHint() => new AttemptCounts(Hints + 1, Undos, Restarts);
        public AttemptCounts WithUndo() => new AttemptCounts(Hints, Undos + 1, Restarts);
        public AttemptCounts WithRestart() => new AttemptCounts(Hints, Undos, Restarts + 1);

        public override string ToString() => $"hints {Hints}, undos {Undos}, restarts {Restarts}";
    }
}
