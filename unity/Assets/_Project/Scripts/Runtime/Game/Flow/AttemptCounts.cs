using System;
using SevenSteps.Contracts;
using SevenSteps.Core;

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
        /// A costing hint was opened in this attempt, so a clear earns at most 1 star (Stars.Of), restarts included
        /// (user decision 2026-11-12-b (2), option A). The HUD shows the star-cap mark while this holds.
        public bool Hinted => Hints > 0;
        public AttemptCounts WithUndo() => new AttemptCounts(Hints, Undos + 1, Restarts);
        public AttemptCounts WithRestart() => new AttemptCounts(Hints, Undos, Restarts + 1);

        /// The counts after one session update (docs 6.5): an Undo counts when the core went back a state (its Undo event;
        /// also after Lose). A Restart counts when the state before it was not the stage start (the start state has no Prev:
        /// NewState, or undone back to it). Hints are not session updates: add them with WithHint. Used by the game
        /// (BoardController) and the QA replay, so both judge stars the same way.
        public AttemptCounts After(SessionUpdate u)
        {
            if (u.Result == null) return this;
            var a = this;
            if (u.Result.Has(GameEventType.Undo)) a = a.WithUndo();
            if (u.Result.Has(GameEventType.Restart) && u.Prev != null && u.Prev.Prev != null) a = a.WithRestart();
            return a;
        }

        public override string ToString() => $"hints {Hints}, undos {Undos}, restarts {Restarts}";
    }
}
