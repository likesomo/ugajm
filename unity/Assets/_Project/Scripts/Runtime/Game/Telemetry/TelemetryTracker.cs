using System;
using System.Collections.Generic;
using SevenSteps.Core;

namespace SevenSteps.Game.Telemetry
{
    /// <summary>
    /// C# port of prototypes/seven-steps/src/telemetry.js <c>Tracker</c> (docs/telemetry.md v1): turns
    /// (state before, command, Rules.Dispatch result) into schema events. Pure C#: the clock and the output are injected,
    /// so the editor verifier can replay server/fixtures/telemetry.golden.json through it.
    /// Reads Core state and events only; it never re-evaluates rules.
    /// Keep this file in step with telemetry.js: same events, same field values, same order.
    /// </summary>
    public sealed class TelemetryTracker
    {
        public const int IdleMs = 30000;

        sealed class Run
        {
            public int Level;
            public long T0;
            public int Moves, Undos, Restarts, Hints, Fails;
            public bool Done;
        }

        readonly Action<TelemetryRecord> emit;
        readonly Func<long> now;
        readonly long idleMs;
        /// Level number (1-based) -> times started on this device.
        public readonly Dictionary<int, int> Attempts;

        long seq;
        long? sessionT;
        long? lastInput;
        Run run;

        public TelemetryTracker(Action<TelemetryRecord> emit, Func<long> now, Dictionary<int, int> attempts = null, long idleMs = IdleMs)
        {
            this.emit = emit;
            this.now = now;
            this.idleMs = idleMs;
            Attempts = attempts ?? new Dictionary<int, int>();
        }

        public bool InLevel => run != null;
        public bool LevelDone => run != null && run.Done;

        TelemetryRecord New(string type) => new TelemetryRecord(seq++, now(), type);

        public void SessionStart(bool firstTime, int cleared, bool resumed = false)
        {
            sessionT ??= now();
            lastInput = now();
            var e = New("session_start").Set("firstTime", firstTime).Set("cleared", cleared);
            if (resumed) e.Set("resumed", true);
            emit(e);
        }

        public void LevelStart(State state, int level, string levelName, string via)
        {
            Attempts.TryGetValue(level, out int a);
            int attempt = a + 1;
            Attempts[level] = attempt;
            run = new Run { Level = level, T0 = now() };
            lastInput = now();
            emit(New("level_start").Set("level", level).Set("levelName", levelName).Set("levelKey", LevelKey(state.Level))
                .Set("attempt", attempt).Set("via", via).Set("bodies", state.Level.Bodies));
        }

        TelemetryRecord Where(TelemetryRecord e, State s) =>
            e.Set("level", run.Level).Set("body", BodyNo(s)).Set("step", s.Used).Set("at", Xy(s, s.Pos));

        /// Every player input, before acting on it. Emits idle if the pause before it was long.
        public void Input(State state)
        {
            long t = now();
            if (run != null && lastInput.HasValue && t - lastInput.Value >= idleMs)
            {
                emit(Where(New("idle"), state).Set("idleMs", t - lastInput.Value).Set("status", StatusName(state.Status))
                    .Set("bodiesLeft", state.Bodies).Set("statues", state.Statues.Length));
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

            if (cmd.Type == CommandType.Restart)
            {
                if (run.Done) { LevelStart(after, L, before.Level.Name, "again"); return; }
                emit(Where(New("restart"), before).Set("moves", run.Moves));
                run.Restarts++;
                return;
            }
            if (cmd.Type == CommandType.Undo)
            {
                if (ReferenceEquals(after, before)) return;
                emit(Where(New("undo"), before).Set("status", StatusName(before.Status)));
                run.Undos++;
                return;
            }
            // Ignored input (moving while choosing KEEP/SWITCH or after the stage ended): nothing to record.
            if (ReferenceEquals(after, before) && !Has(r, GameEventType.Bump)) return;
            int land = before.Pos;

            if (cmd.Type == CommandType.Move)
            {
                int step = before.Used + 1;
                string dir = DirName(cmd.Dir);
                if (Has(r, GameEventType.Bump))
                {
                    int at = EvAt(r, GameEventType.Bump);
                    string blockedBy = at >= 0 && Rules.Contains(before.Statues, at) ? "statue" : TileKind(before, at);
                    emit(Base(before, L, step, dir).Set("to", Xy(before, at)).Set("result", "blocked").Set("blockedBy", blockedBy));
                    return;
                }
                run.Moves++;
                land = FirstAt(r, GameEventType.Fall, GameEventType.Bridge, GameEventType.Win, GameEventType.Seal,
                    GameEventType.Vanish, GameEventType.Choose, GameEventType.Stone, GameEventType.Step);
                int first = Neighbor(before, cmd.Dir);
                string firstTile = TileKind(before, first);
                string result = "moved";
                if (Has(r, GameEventType.Fall)) result = "fell";
                else if (Has(r, GameEventType.Bridge)) result = "bridged";
                else if (land != first) result = "slid";
                else if (firstTile == "door") result = "door";
                else if (firstTile == "gate") result = "gate";
                var ev = Base(before, L, step, dir).Set("to", Xy(before, land)).Set("result", result);
                if (Has(r, GameEventType.Crumble)) ev.Set("crumbled", true);
                emit(ev);
                if (step == Rules.Steps && !Has(r, GameEventType.Fall))
                {
                    string effect = Has(r, GameEventType.Win) ? "win" : Has(r, GameEventType.Bridge) ? "bridge" : Has(r, GameEventType.Seal) ? "seal"
                        : Has(r, GameEventType.Vanish) ? "vanish" : Has(r, GameEventType.Choose) ? "choose" : "statue";
                    emit(New("seventh_step").Set("level", L).Set("body", BodyNo(before)).Set("at", Xy(before, land))
                        .Set("tile", TileKind(before, land)).Set("effect", effect));
                }
            }
            else if (cmd.Type == CommandType.Keep || cmd.Type == CommandType.Switch)
            {
                emit(New(cmd.Type == CommandType.Keep ? "keep" : "switch").Set("level", L).Set("body", BodyNo(before)).Set("at", Xy(before, before.Pos)));
            }

            foreach (var e in r.Events)
            {
                string reason = LostReason(e.Type);
                if (reason == null) continue;
                emit(New("body_lost").Set("level", L).Set("body", BodyNo(before)).Set("at", Xy(before, e.At))
                    .Set("reason", reason).Set("wasted", reason == "fell" || reason == "vanish"));
            }
            if (after.Status == Status.Lose)
            {
                run.Fails++;
                emit(New("level_fail").Set("level", L).Set("at", Xy(before, land)).Set("moves", run.Moves).Set("bodiesUsed", before.Level.Bodies));
            }
            if (after.Status == Status.Win && !run.Done)
            {
                run.Done = true;
                emit(New("level_clear").Set("level", L).Set("durationMs", now() - run.T0).Set("moves", run.Moves).Set("undos", run.Undos)
                    .Set("restarts", run.Restarts).Set("hints", run.Hints).Set("fails", run.Fails).Set("bodiesUsed", BodyNo(after)));
            }
        }

        /// An event outside the schema table (from ITelemetry.Track). Gets the next seq like every other event.
        public void Custom(string type, Action<TelemetryRecord> fill)
        {
            var e = New(type);
            fill?.Invoke(e);
            emit(e);
        }

        public void Hint(State state)
        {
            if (run == null) return;
            Input(state);
            run.Hints++;
            emit(Where(New("hint"), state));
        }

        /// Leaving the current stage for another one (call before LevelStart of the new one).
        public void Leave(State state, int toLevel)
        {
            if (run == null) return;
            FlushIdle(state);
            var r = run;
            if (!r.Done)
            {
                emit(Where(New("level_leave"), state).Set("toLevel", toLevel).Set("durationMs", now() - r.T0)
                    .Set("moves", r.Moves).Set("undos", r.Undos).Set("restarts", r.Restarts).Set("hints", r.Hints).Set("fails", r.Fails));
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
            var e = New("session_end").Set("reason", reason).Set("sessionMs", now() - (sessionT ?? now()));
            if (run != null && state != null) Where(e, state).Set("cleared", run.Done);
            emit(e);
        }

        // ---------- helpers (same as telemetry.js) ----------

        TelemetryRecord Base(State before, int level, int step, string dir) =>
            New("move").Set("level", level).Set("body", BodyNo(before)).Set("step", step).Set("dir", dir).Set("from", Xy(before, before.Pos));

        static int BodyNo(State s) => s.Level.Bodies - s.Bodies + 1;

        static object Xy(State s, int i) => i < 0 ? null : (object)new XY(i % s.Level.W, i / s.Level.W);

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

        static int FirstAt(StepResult r, params GameEventType[] order)
        {
            foreach (var t in order) if (Has(r, t)) return EvAt(r, t);
            return -1;
        }

        static string LostReason(GameEventType t) => t switch
        {
            GameEventType.Fall => "fell",
            GameEventType.Vanish => "vanish",
            GameEventType.Stone => "statue",
            GameEventType.Switch => "switch",
            GameEventType.Bridge => "bridge",
            GameEventType.Seal => "seal",
            _ => null,
        };

        public static string TileKind(State s, int i)
        {
            if (i < 0) return "edge";
            if (i == s.Start) return "start";
            char t = Rules.TileAt(s, i);
            if (t >= 'a' && t <= 'c') return "plate";
            if (t >= 'A' && t <= 'C') return "door";
            if (t >= '1' && t <= '7') return "gate";
            return t switch
            {
                '#' => "wall", '.' => "floor", 'E' => "exit", 'o' => "pit", '~' => "ice", 'x' => "cracked", '=' => "bridge",
                _ => "floor",
            };
        }

        static int Neighbor(State s, Dir dir)
        {
            int w = s.Level.W, h = s.Level.H;
            int x = s.Pos % w + (dir == Dir.Left ? -1 : dir == Dir.Right ? 1 : 0);
            int y = s.Pos / w + (dir == Dir.Up ? -1 : dir == Dir.Down ? 1 : 0);
            return x < 0 || y < 0 || x >= w || y >= h ? -1 : y * w + x;
        }

        public static string DirName(Dir d) => d switch { Dir.Up => "up", Dir.Down => "down", Dir.Left => "left", _ => "right" };

        public static string StatusName(Status s) => s switch { Status.Play => "play", Status.Choose => "choose", Status.Win => "win", _ => "lose" };

        /// docs/telemetry.md levelKey: FNV-1a 32 over "bodies\nrow\nrow..." (rows padded with '#', start as 'S'), 8 hex chars.
        public static string LevelKey(Level level)
        {
            uint h = 0x811c9dc5;
            void Mix(char c) { h ^= c; h = unchecked(h * 0x01000193); }
            foreach (char c in level.Bodies.ToString()) Mix(c);
            for (int y = 0; y < level.H; y++)
            {
                Mix('\n');
                for (int x = 0; x < level.W; x++)
                {
                    int i = y * level.W + x;
                    Mix(i == level.Start ? 'S' : level.Tile(i));
                }
            }
            return h.ToString("x8");
        }
    }
}
