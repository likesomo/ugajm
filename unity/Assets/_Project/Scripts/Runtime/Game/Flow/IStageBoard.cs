using System;
using SevenSteps.Core;
using SevenSteps.Game.UI;

namespace SevenSteps.Game.Flow
{
    /// <summary>
    /// What the game flow controller needs from the board side (docs/scene-flow.md 4.3, S3). Implemented by the board
    /// controller (BoardController, added by the Game scene root GameController); a fake in tests. The board knows nothing
    /// about AppFlow: it loads the stage it is told and reports how the attempt is going.
    /// </summary>
    public interface IStageBoard
    {
        /// Current board state, null before the first LoadStage.
        State State { get; }
        /// Load a stage (0-based). <paramref name="via"/> = telemetry level_start.via for this entry.
        void LoadStage(int stage, string via);

        /// The 7th step reached the exit (rules state is Win). Raised before the board redraws its HUD, so progress saved
        /// in the handler shows at once.
        event Action<State> Won;
        /// The win presentation (walk to the exit) finished: time for the result panel.
        event Action<State> WinPresented;
        /// The board just became Lose (all bodies gone). Not an AppState: undo / restart still work (4.7).
        event Action<State> Lost;
        /// The board left Lose (undo / restart).
        event Action Recovered;
    }

    /// <summary>The result panel as the flow controller sees it (ResultView; a fake in tests).</summary>
    public interface IResultPanel
    {
        void Show(ResultModel model);
        void Hide();
    }
}
