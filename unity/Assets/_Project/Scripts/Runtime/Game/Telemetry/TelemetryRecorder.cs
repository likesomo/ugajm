using SevenSteps.Contracts;
using SevenSteps.Core;

namespace SevenSteps.Game.Telemetry
{
    /// <summary>
    /// Game scene side of telemetry (docs/architecture.md 11.4): listens to <see cref="GameSession.Updated"/> and the
    /// controller's stage/hint events and forwards them to the app-lifetime <see cref="TelemetryTracker"/>.
    /// Moves the same values telemetry.js main.js hooks pass: (state before, command, result). No rule logic here.
    /// Replays (Snap without a result) and stage rebuilds are not player input and are not recorded.
    /// </summary>
    public sealed class TelemetryRecorder
    {
        readonly BoardController game;
        readonly GameSession session;
        readonly HttpTelemetry telemetry;
        TelemetryTracker Tracker => telemetry.Tracker;

        public TelemetryRecorder(BoardController game, GameSession session, HttpTelemetry telemetry)
        {
            this.game = game;
            this.session = session;
            this.telemetry = telemetry;
            telemetry.CurrentState = () => session.State;
            session.Updated += OnUpdated;
            game.StageLeaving += OnStageLeaving;
            game.StageEntered += OnStageEntered;
            game.HintShown += OnHint;
        }

        /// The Game scene is going away (stage select). Keep the last state so the next stage start can close this attempt.
        public void Dispose()
        {
            session.Updated -= OnUpdated;
            game.StageLeaving -= OnStageLeaving;
            game.StageEntered -= OnStageEntered;
            game.HintShown -= OnHint;
            telemetry.PendingLeaveState = session.State;
            telemetry.CurrentState = null;
        }

        void OnStageLeaving(int toStage)
        {
            // A tutorial board is not an attempt (docs/tutorial.md 2.6): only a stage left earlier (another scene) closes here.
            var from = game.InTutorial ? telemetry.PendingLeaveState : session.State ?? telemetry.PendingLeaveState;
            if (from != null) Tracker.Leave(from, toStage + 1);
            telemetry.PendingLeaveState = null;
        }

        void OnStageEntered(int stage, string via)
        {
            var s = session.State;
            // chapter: levels.json "chapter" id; "" (no chapter) is not sent (docs/telemetry.md level_start).
            Tracker.LevelStart(s, stage + 1, s.Level.Name, via, LevelCatalog.ChapterIdOf(stage));
        }

        void OnHint()
        {
            if (session.State == null) return;
            var t = game.LastHint; // null: a stage without a stored route (the whole route at once, costs)
            Tracker.Hint(session.State, t?.Tier ?? 0, t?.Tiers ?? 0, t?.Costs ?? true);
        }

        void OnUpdated(SessionUpdate u)
        {
            if (u.Result == null) return; // Rebuild, replay Snap
            if (game.InTutorial) return; // tutorial boards send nothing (docs/tutorial.md 2.6)
            if (u.Kind == SessionUpdateKind.Rebuild) return;
            Tracker.Command(u.Prev, u.Command, u.Result);
            var st = u.State.Status;
            if (u.Kind == SessionUpdateKind.Present && (st == Status.Win || st == Status.Lose)) telemetry.Flush();
        }
    }
}
