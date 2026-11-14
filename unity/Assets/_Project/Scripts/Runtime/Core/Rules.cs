using System;
using System.Collections.Generic;
using System.Text;

namespace SevenSteps.Core
{
    /// <summary>
    /// Pure rules, a line-by-line port of prototypes/seven-steps/src/core.js. No time, no randomness:
    /// every state change goes through Step/Choose, and the shell goes through Dispatch.
    /// </summary>
    public static class Rules
    {
        public const int Steps = 7;
        static readonly int[] DX = { 0, 0, -1, 1 };
        static readonly int[] DY = { -1, 1, 0, 0 };
        static readonly Act[] MoveActs = { Act.Up, Act.Down, Act.Left, Act.Right };
        static readonly Act[] ChooseActs = { Act.Keep, Act.Switch };
        static readonly Act[] KeepOnly = { Act.Keep };
        static readonly Act[] NoActs = new Act[0];

        public static State NewState(Level level, int bodies = 0) => new State
        {
            Level = level,
            Start = level.Start,
            Pos = level.Start,
            Used = 0,
            Bodies = bodies > 0 ? bodies : level.Bodies,
            Statues = new int[0],
            Bridges = new int[0],
            Broken = new int[0],
            Trail = new[] { new Footprint(level.Start, 0) },
            Status = Status.Play,
            Prev = null,
        };

        public static bool Contains(int[] arr, int i) => Array.IndexOf(arr, i) >= 0;

        /// Tile as currently seen: bridges (pit filled by a statue) read as '=', broken cracked floor as a pit,
        /// flip walls after an odd number of lever pulls swap: 'R' is always "raised now", 'r' "lowered now".
        public static char TileAt(State s, int i)
        {
            if (Contains(s.Bridges, i)) return '=';
            if (Contains(s.Broken, i)) return 'o';
            char t = s.Level.Tile(i);
            if (s.Flipped && IsFlipWall(t)) return t == 'R' ? 'r' : 'R';
            return t;
        }

        /// Flip wall letters: R raised (blocks), r lowered (open), as seen through TileAt.
        public static bool IsFlipWall(char t) => t == 'R' || t == 'r';

        /// Mud 'm': stepping into it costs 2 steps, so it blocks with one step left. An ice slide stops on it at no extra cost.
        public static bool IsMud(char t) => t == 'm';

        /// Statues standing now. Bridges (filled pits, sealed cracked floor) are not statues.
        public static int StatueCount(State s) => s.Statues.Length;

        /// Door letters: A-C (plate doors) and Q (statue-count door).
        public static bool IsDoor(char t) => (t >= 'A' && t <= 'C') || t == 'Q';

        /// A-C: a statue stands on a matching plate. Q: exactly Level.StatueCount statues stand.
        public static bool DoorOpen(State s, char door)
        {
            if (door == 'Q') return StatueCount(s) == s.Level.StatueCount;
            foreach (var p in s.Level.PlatesFor(char.ToLowerInvariant(door)))
                if (Contains(s.Statues, p)) return true;
            return false;
        }

        public static int Neighbor(Level l, int i, Dir d)
        {
            int x = l.X(i) + DX[(int)d];
            int y = l.Y(i) + DY[(int)d];
            return x < 0 || y < 0 || x >= l.W || y >= l.H ? -1 : y * l.W + x;
        }

        public static bool Passable(State s, int i, int stepIdx)
        {
            if (i < 0 || Contains(s.Statues, i)) return false;
            char t = TileAt(s, i);
            if (t == '#' || t == 'R') return false;
            if (IsDoor(t)) return DoorOpen(s, t);
            if (t >= '1' && t <= '7') return t - '0' == stepIdx;
            return true;
        }

        /// A new statue may open doors (plates, or Q reaching its count) or close them (Q passing its count):
        /// one DoorOpen/DoorClose per door cell that changed, in cell order. Undo/restart swap the whole state instead.
        static void DoorChanges(State before, State after, List<GameEvent> events)
        {
            var level = after.Level;
            for (int i = 0; i < level.Count; i++)
            {
                char t = level.Tile(i);
                if (!IsDoor(t)) continue;
                bool was = DoorOpen(before, t);
                bool now = DoorOpen(after, t);
                if (was != now) events.Add(new GameEvent(now ? GameEventType.DoorOpen : GameEventType.DoorClose, i));
            }
        }

        static int[] Add(int[] arr, int i)
        {
            if (Contains(arr, i)) return arr;
            var r = new int[arr.Length + 1];
            Array.Copy(arr, r, arr.Length);
            r[arr.Length] = i;
            Array.Sort(r);
            return r;
        }

        static State NextBody(State s, List<GameEvent> events, int start)
        {
            var n = s.Clone();
            int bodies = s.Bodies - 1;
            if (bodies <= 0)
            {
                events.Add(new GameEvent(GameEventType.Lose));
                n.Bodies = 0;
                n.Status = Status.Lose;
                return n;
            }
            events.Add(new GameEvent(GameEventType.Respawn, start));
            n.Bodies = bodies;
            n.Start = start;
            n.Pos = start;
            n.Used = 0;
            n.Status = Status.Play;
            n.Trail = new[] { new Footprint(start, 0) };
            return n;
        }

        /// Still playing but every direction bumps (wall, closed door, wrong number gate, mud with one step left, statues
        /// round the start): only Undo or Restart help (user report 2026-11-14, stage 44). The same test as Step's Bump;
        /// presentation only, the rules and the solver do not use it.
        public static bool IsStuck(State s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (s.Status != Status.Play) return false;
            for (var d = Dir.Up; d <= Dir.Right; d++)
            {
                int first = Neighbor(s.Level, s.Pos, d);
                int stepIdx = s.Used + (first >= 0 && IsMud(TileAt(s, first)) ? 2 : 1);
                if (stepIdx <= Steps && Passable(s, first, stepIdx)) return false;
            }
            return true;
        }

        /// One step. An illegal move returns the same state object with a Bump event.
        public static StepResult Step(State s, Dir dir)
        {
            var events = new List<GameEvent>();
            if (s.Status != Status.Play) return new StepResult(s, events);
            int first = Neighbor(s.Level, s.Pos, dir);
            // Mud: stepping into it costs 2 steps; with only one step left it blocks like a wall.
            int stepIdx = s.Used + (first >= 0 && IsMud(TileAt(s, first)) ? 2 : 1);
            if (stepIdx > Steps || !Passable(s, first, stepIdx))
            {
                events.Add(new GameEvent(GameEventType.Bump, first));
                return new StepResult(s, events);
            }

            int pos = first;
            var path = new List<int> { pos };
            var trail = new List<Footprint>(s.Trail) { new Footprint(pos, stepIdx) };
            // Ice: keep sliding while standing on ice and the next cell is open. The slide is still one step.
            // Sliding into mud stops there (mud is not ice) and adds no step.
            while (TileAt(s, pos) == '~')
            {
                int n = Neighbor(s.Level, pos, dir);
                if (!Passable(s, n, stepIdx)) break;
                pos = n;
                path.Add(pos);
                trail.Add(new Footprint(pos, stepIdx));
            }

            var next = s.Clone();
            next.Pos = pos;
            next.Used = stepIdx;
            next.Trail = trail.ToArray();
            var pathArr = path.ToArray();

            // Cracked floor survives one crossing: it breaks into a pit behind you.
            if (TileAt(s, s.Pos) == 'x')
            {
                next.Broken = Add(next.Broken, s.Pos);
                events.Add(new GameEvent(GameEventType.Crumble, s.Pos));
            }
            // Slid on from the first (ice) cell: Slide at that cell, before what happens where the slide ends.
            if (pos != first) events.Add(new GameEvent(GameEventType.Slide, first));

            char t = TileAt(next, pos);
            bool lucky = stepIdx == Steps;
            if (t == 'o')
            {
                if (lucky)
                {
                    next.Bridges = Add(next.Bridges, pos);
                    events.Add(new GameEvent(GameEventType.Bridge, pos, stepIdx));
                }
                else
                {
                    events.Add(new GameEvent(GameEventType.Fall, pos, stepIdx));
                }
                return new StepResult(NextBody(next, events, next.Start), events, pathArr);
            }
            if (!lucky)
            {
                events.Add(new GameEvent(GameEventType.Step, pos, stepIdx));
                return new StepResult(next, events, pathArr);
            }
            // Lever: the 7th step ending on it flips every flip wall, then the body is spent as usual (choose/stone).
            // On the start cell the 7th step only vanishes, so the lever there is not pulled.
            if (t == 'L' && pos != s.Start)
            {
                next.Flipped = !s.Flipped;
                events.Add(new GameEvent(GameEventType.Flip, pos));
            }
            if (t == 'E')
            {
                events.Add(new GameEvent(GameEventType.Win, pos, stepIdx));
                next.Status = Status.Win;
                return new StepResult(next, events, pathArr);
            }
            if (t == 'x')
            {
                // Ending on cracked floor seals it: the floor gives way and the statue becomes a bridge.
                next.Broken = Add(next.Broken, pos);
                next.Bridges = Add(next.Bridges, pos);
                events.Add(new GameEvent(GameEventType.Seal, pos, stepIdx));
            }
            else if (pos == s.Start)
            {
                events.Add(new GameEvent(GameEventType.Vanish, pos, stepIdx));
            }
            else if (s.Bodies > 1)
            {
                // KEEP or SWITCH: the player decides what this body leaves behind.
                events.Add(new GameEvent(GameEventType.Choose, pos, stepIdx));
                next.Status = Status.Choose;
                return new StepResult(next, events, pathArr);
            }
            else
            {
                next.Statues = Add(next.Statues, pos);
                events.Add(new GameEvent(GameEventType.Stone, pos, stepIdx));
                DoorChanges(s, next, events);
            }
            return new StepResult(NextBody(next, events, next.Start), events, pathArr);
        }

        /// After the 7th step. KEEP: become a statue here, next body wakes at the current start.
        /// SWITCH: leave nothing, next body wakes here (this spot becomes the new start). Both spend the body.
        public static StepResult Choose(State s, bool keep)
        {
            var events = new List<GameEvent>();
            if (s.Status != Status.Choose) return new StepResult(s, events);
            if (keep)
            {
                events.Add(new GameEvent(GameEventType.Stone, s.Pos));
                var withStatue = s.Clone();
                withStatue.Statues = Add(s.Statues, s.Pos);
                DoorChanges(s, withStatue, events);
                return new StepResult(NextBody(withStatue, events, withStatue.Start), events);
            }
            events.Add(new GameEvent(GameEventType.Switch, s.Pos));
            return new StepResult(NextBody(s, events, s.Pos), events);
        }

        /// Every legal action kind in a state; the solver walks these.
        public static Act[] Actions(State s, bool noSwitch = false)
        {
            if (s.Status == Status.Choose) return noSwitch ? KeepOnly : ChooseActs;
            return s.Status == Status.Play ? MoveActs : NoActs;
        }

        public static StepResult Apply(State s, Act a) => a switch
        {
            Act.Keep => Choose(s, true),
            Act.Switch => Choose(s, false),
            _ => Step(s, (Dir)(int)a),
        };

        /// Single entry point for the game shell. Keeps an undo chain through Prev.
        public static StepResult Dispatch(State s, Command cmd)
        {
            switch (cmd.Type)
            {
                case CommandType.Move:
                case CommandType.Keep:
                case CommandType.Switch:
                {
                    var r = cmd.Type == CommandType.Move ? Step(s, cmd.Dir) : Choose(s, cmd.Type == CommandType.Keep);
                    if (ReferenceEquals(r.State, s)) return r;
                    var withPrev = r.State.Clone();
                    withPrev.Prev = s;
                    r.State = withPrev;
                    return r;
                }
                case CommandType.Undo:
                    return s.Prev != null
                        ? new StepResult(s.Prev, new List<GameEvent> { new GameEvent(GameEventType.Undo) })
                        : new StepResult(s, new List<GameEvent>());
                case CommandType.Restart:
                    return new StepResult(NewState(s.Level), new List<GameEvent> { new GameEvent(GameEventType.Restart) });
                default:
                    return new StepResult(s, new List<GameEvent>());
            }
        }

        /// Identity of a state for the solver (same fields as core.js keyOf).
        public static string Key(State s)
        {
            var sb = new StringBuilder(64);
            sb.Append(s.Start).Append('|').Append(s.Pos).Append('|').Append(s.Used).Append('|').Append(s.Bodies).Append('|');
            AppendList(sb, s.Statues); sb.Append('|');
            AppendList(sb, s.Bridges); sb.Append('|');
            AppendList(sb, s.Broken); sb.Append('|');
            sb.Append((int)s.Status).Append('|');
            sb.Append(s.Flipped ? '1' : '0');
            return sb.ToString();
        }

        static void AppendList(StringBuilder sb, int[] arr)
        {
            for (int k = 0; k < arr.Length; k++)
            {
                if (k > 0) sb.Append(',');
                sb.Append(arr[k]);
            }
        }

        // Replay letters, same as the web prototype's ?replay= (U D L R moves, K keep, X switch).
        public static char Letter(Act a) => "UDLRKX"[(int)a];

        public static bool TryParseLetter(char c, out Act a)
        {
            int k = "UDLRKX".IndexOf(char.ToUpperInvariant(c));
            a = k < 0 ? Act.Up : (Act)k;
            return k >= 0;
        }
    }
}
