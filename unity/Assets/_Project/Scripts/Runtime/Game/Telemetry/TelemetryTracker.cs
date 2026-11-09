using System;
using SevenSteps.Contracts;
using SevenSteps.Core;
using N = SevenSteps.Contracts.TelemetryNames;

namespace SevenSteps.Game.Telemetry
{
    /// <summary>
    /// C# port of prototypes/seven-steps/src/telemetry.js <c>Tracker</c> (docs/telemetry.md v1, docs/architecture.md 11.4):
    /// turns (state before, command, Rules.Dispatch result) into <see cref="TelemetryEvent"/>s for an <see cref="ITelemetry"/>.
    /// Pure C#: the clock and the attempt counter are injected, so the golden test replays
    /// server/fixtures/telemetry.golden.json through it with <see cref="MemoryTelemetry"/>.
    /// Reads Core state, events and queries only; it never re-evaluates rules. seq/t are stamped by the sender.
    /// Keep in step with telemetry.js: same events, same field values, same order.
    /// </summary>
    public sealed class TelemetryTracker
    {
        sealed class Run
        {
            public int Level;
            public long T0;
            public int Moves, Undos, Restarts, Hints, Fails;
            public bool Done;
        }

        readonly ITelemetry sink;
        readonly Func<long> now;
        readonly Func<int, int> nextAttempt;
        readonly long idleMs;

        long? sessionT;
        long? lastInput;
        Run run;

        /// <param name="nextAttempt">1-based level -> attempt number of this start (ProgressData.CountStart in the game).</param>
        public TelemetryTracker(ITelemetry sink, Func<long> now, Func<int, int> nextAttempt, long idleMs = N.IdleMs)
        {
            this.sink = sink;
            this.now = now;
            this.nextAttempt = nextAttempt;
            this.idleMs = idleMs;
        }

        public bool InLevel => run != null;

        void Emit(TelemetryEvent e) => sink.Track(e);

        public void SessionStart(bool firstTime, int cleared, bool resumed = false)
        {
            sessionT ??= now();
            lastInput = now();
            var e = new TelemetryEvent(N.Types.SessionStart).With("firstTime", firstTime).With("cleared", cleared);
            if (resumed) e.With("resumed", true);
            Emit(e);
        }

        /// <param name="chapter">levels.json stage "chapter" id (docs/telemetry.md level_start), or null/empty when the stage has none (then not sent).</param>
        public void LevelStart(State state, int level, string levelName, string via, string chapter = null)
        {
            int attempt = nextAttempt(level);
            run = new Run { Level = level, T0 = now() };
            lastInput = now();
            var e = new TelemetryEvent(N.Types.LevelStart).With("level", level).With("levelName", levelName)
                .With("levelKey", TelemetryJson.LevelKey(state.Level));
            if (!string.IsNullOrEmpty(chapter)) e.With("chapter", chapter); // same place as telemetry.js: right after levelKey
            Emit(e.With("attempt", attempt).With("via", via).With("bodies", state.Level.Bodies));
        }

        TelemetryEvent Where(TelemetryEvent e, State s) =>
            e.With("level", run.Level).With("body", BodyNo(s)).With("step", s.Used).WithCell("at", s.Pos, s.Level.W);

        /// Every player input, before acting on it. Emits idle if the pause before it was long.
        public void Input(State state)
        {
            long t = now();
            if (run != null && lastInput.HasValue && t - lastInput.Value >= idleMs)
            {
                Emit(Where(new TelemetryEvent(N.Types.Idle), state).With("idleMs", t - lastInput.Value)
                    .With("status", StatusName(state.Status)).With("bodiesLeft", state.Bodies).With("statues", state.Statues.Length));
            }
            lastInput = t;
        }

        /// before: the state passed to Rules.Dispatch; r: its result.
        public void Command(State before, Command cmd, StepResult r)
        {
            if (run == null) return;
            Input(before);
            var after = r.State;
            int L = run.Level;
            int w = before.Level.W;

            if (cmd.Type == CommandType.Restart)
            {
                if (run.Done) { LevelStart(after, L, before.Level.Name, N.Via.Again); return; }
                Emit(Where(new TelemetryEvent(N.Types.Restart), before).With("moves", run.Moves));
                run.Restarts++;
                return;
            }
            if (cmd.Type == CommandType.Undo)
            {
                if (ReferenceEquals(after, before)) return;
                Emit(Where(new TelemetryEvent(N.Types.Undo), before).With("status", StatusName(before.Status)));
                run.Undos++;
                return;
            }
            // Ignored input (moving while choosing KEEP/SWITCH or after the stage ended): nothing to record.
            if (ReferenceEquals(after, before) && !Has(r, GameEventType.Bump)) return;
            int land = before.Pos;

            if (cmd.Type == CommandType.Move)
            {
                int first = Rules.Neighbor(before.Level, before.Pos, cmd.Dir);
                string dir = DirName(cmd.Dir);
                if (Has(r, GameEventType.Bump))
                {
                    // A blocked move reports Used + 1 (telemetry.js), whatever the cell would have cost.
                    int at = EvAt(r, GameEventType.Bump);
                    string blockedBy = at >= 0 && Rules.Contains(before.Statues, at) ? N.Tile.Statue : TileKind(before, at);
                    Emit(Base(before, L, before.Used + 1, dir).WithCell("to", at, w).With("result", N.MoveResult.Blocked).With("blockedBy", blockedBy));
                    return;
                }
                run.Moves++;
                // Step and landing cell come from the core's landing event: its StepIdx already counts mud as 2 steps,
                // so the step cost is not computed again here (docs/architecture.md 9, review of 61180ac).
                var landing = Landing(r);
                int step = landing.StepIdx;
                if (step <= before.Used) throw new InvalidOperationException($"TelemetryTracker: landing event {landing.Type} has step {step} after {before.Used} used");
                land = landing.At;
                string firstTile = TileKind(before, first);
                string result = N.MoveResult.Moved;
                if (Has(r, GameEventType.Fall)) result = N.MoveResult.Fell;
                else if (Has(r, GameEventType.Bridge)) result = N.MoveResult.Bridged;
                else if (land != first) result = N.MoveResult.Slid;
                else if (firstTile == N.Tile.Door) result = N.MoveResult.Door;
                else if (firstTile == N.Tile.Gate) result = N.MoveResult.Gate;
                var ev = Base(before, L, step, dir).WithCell("to", land, w).With("result", result);
                if (Has(r, GameEventType.Crumble)) ev.With("crumbled", true);
                Emit(ev);
                if (step == Rules.Steps && !Has(r, GameEventType.Fall))
                {
                    string effect = Has(r, GameEventType.Win) ? N.Effect.Win : Has(r, GameEventType.Bridge) ? N.Effect.Bridge
                        : Has(r, GameEventType.Seal) ? N.Effect.Seal : Has(r, GameEventType.Vanish) ? N.Effect.Vanish
                        : Has(r, GameEventType.Choose) ? N.Effect.Choose : N.Effect.Statue;
                    // tile is the landing cell before the step (a lever, not the walls it flips); flipped: the lever flipped the walls.
                    var seventh = new TelemetryEvent(N.Types.SeventhStep).With("level", L).With("body", BodyNo(before)).WithCell("at", land, w)
                        .With("tile", TileKind(before, land)).With("effect", effect);
                    if (Has(r, GameEventType.Flip)) seventh.With("flipped", true);
                    Emit(seventh);
                }
            }
            else if (cmd.Type == CommandType.Keep || cmd.Type == CommandType.Switch)
            {
                Emit(new TelemetryEvent(cmd.Type == CommandType.Keep ? N.Types.Keep : N.Types.Switch)
                    .With("level", L).With("body", BodyNo(before)).WithCell("at", before.Pos, w));
            }

            foreach (var e in r.Events)
            {
                string reason = LostReason(e.Type);
                if (reason == null) continue;
                Emit(new TelemetryEvent(N.Types.BodyLost).With("level", L).With("body", BodyNo(before)).WithCell("at", e.At, w)
                    .With("reason", reason).With("wasted", reason == N.LostReason.Fell || reason == N.LostReason.Vanish));
            }
            if (after.Status == Status.Lose)
            {
                run.Fails++;
                Emit(new TelemetryEvent(N.Types.LevelFail).With("level", L).WithCell("at", land, w).With("moves", run.Moves)
                    .With("bodiesUsed", before.Level.Bodies));
            }
            if (after.Status == Status.Win && !run.Done)
            {
                run.Done = true;
                Emit(new TelemetryEvent(N.Types.LevelClear).With("level", L).With("durationMs", now() - run.T0).With("moves", run.Moves)
                    .With("undos", run.Undos).With("restarts", run.Restarts).With("hints", run.Hints).With("fails", run.Fails)
                    .With("bodiesUsed", BodyNo(after)));
            }
        }

        public void Hint(State state)
        {
            if (run == null) return;
            Input(state);
            run.Hints++;
            Emit(Where(new TelemetryEvent(N.Types.Hint), state));
        }

        /// Leaving the current stage for another one (call before LevelStart of the new one).
        public void Leave(State state, int toLevel)
        {
            if (run == null) return;
            FlushIdle(state);
            var r = run;
            if (!r.Done)
            {
                Emit(Where(new TelemetryEvent(N.Types.LevelLeave), state).With("toLevel", toLevel).With("durationMs", now() - r.T0)
                    .With("moves", r.Moves).With("undos", r.Undos).With("restarts", r.Restarts).With("hints", r.Hints).With("fails", r.Fails));
            }
            run = null;
        }

        /// An idle pause still in progress when the stage or tab is left.
        public void FlushIdle(State state)
        {
            if (state != null && run != null) Input(state);
        }

        public void SessionEnd(State state, string reason)
        {
            FlushIdle(state);
            var e = new TelemetryEvent(N.Types.SessionEnd).With("reason", reason).With("sessionMs", now() - (sessionT ?? now()));
            if (run != null && state != null) Where(e, state).With("cleared", run.Done);
            Emit(e);
        }

        // ---------- helpers (same as telemetry.js) ----------

        TelemetryEvent Base(State before, int level, int step, string dir) =>
            new TelemetryEvent(N.Types.Move).With("level", level).With("body", BodyNo(before)).With("step", step).With("dir", dir)
                .WithCell("from", before.Pos, before.Level.W);

        static int BodyNo(State s) => s.Level.Bodies - s.Bodies + 1;

        static bool Has(StepResult r, GameEventType t)
        {
            foreach (var e in r.Events) if (e.Type == t) return true;
            return false;
        }

        static int EvAt(StepResult r, GameEventType t)
        {
            foreach (var e in r.Events) if (e.Type == t) return e.At;
            return -1;
        }

        static readonly GameEventType[] LandingOrder =
        {
            GameEventType.Fall, GameEventType.Bridge, GameEventType.Win, GameEventType.Seal, GameEventType.Vanish,
            GameEventType.Choose, GameEventType.Stone, GameEventType.Step,
        };

        /// Landing event of an unblocked move (cell after any ice slide, and the step it took), in telemetry.js order.
        static GameEvent Landing(StepResult r)
        {
            foreach (var t in LandingOrder)
                foreach (var e in r.Events)
                    if (e.Type == t) return e;
            throw new InvalidOperationException("TelemetryTracker: unblocked move without a landing event");
        }

        static string LostReason(GameEventType t) => t switch
        {
            GameEventType.Fall => N.LostReason.Fell,
            GameEventType.Vanish => N.LostReason.Vanish,
            GameEventType.Stone => N.LostReason.Statue,
            GameEventType.Switch => N.LostReason.Switch,
            GameEventType.Bridge => N.LostReason.Bridge,
            GameEventType.Seal => N.LostReason.Seal,
            _ => null,
        };

        /// telemetry.js tileKind. Flip walls are read through Rules.TileAt, so they report the current state (R up/blocks, r down/open).
        public static string TileKind(State s, int i)
        {
            if (i < 0) return N.Tile.Edge;
            if (i == s.Start) return N.Tile.Start;
            char t = Rules.TileAt(s, i);
            if (t >= 'a' && t <= 'c') return N.Tile.Plate;
            if (t >= 'A' && t <= 'C') return N.Tile.Door;
            if (t >= '1' && t <= '7') return N.Tile.Gate;
            return t switch
            {
                '#' => N.Tile.Wall, 'E' => N.Tile.Exit, 'o' => N.Tile.Pit, '~' => N.Tile.Ice, 'x' => N.Tile.Cracked, '=' => N.Tile.Bridge,
                'm' => N.Tile.Mud, 'Q' => N.Tile.Door, 'L' => N.Tile.Lever, 'R' => N.Tile.FlipUp, 'r' => N.Tile.FlipDown,
                _ => N.Tile.Floor,
            };
        }

        public static string DirName(Dir d) => d switch { Dir.Up => N.Dir.Up, Dir.Down => N.Dir.Down, Dir.Left => N.Dir.Left, _ => N.Dir.Right };

        public static string StatusName(Status s) => s switch
        {
            Status.Play => N.Status.Play, Status.Choose => N.Status.Choose, Status.Win => N.Status.Win, _ => N.Status.Lose,
        };
    }
}
