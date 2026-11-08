using System;
using SevenSteps.Core;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// What <see cref="GameSession"/> needs from a board view. Top-down and quarter-view boards implement the same interface.
    /// A presenter only reads State/StepResult. Rule questions (passable, win, door open) are answered by Core, never here.
    /// </summary>
    public interface IBoardPresenter
    {
        /// New stage: destroy and rebuild everything for this state. No animation.
        void Rebuild(State state);

        /// Stop any running animation and draw exactly this snapshot, with no animation.
        /// Used for Undo, Restart, replay and for cutting an animation short. Must not invoke a pending Present callback.
        void Snap(State state);

        /// Animate prev -> result.State: walk result.Path first, then play result.Events in list order,
        /// then end in exactly the picture Snap(result.State) would draw.
        /// Must invoke <paramref name="done"/> exactly once when finished, unless Snap or Rebuild cancels it first.
        void Present(State prev, StepResult result, Action done);
    }
}
