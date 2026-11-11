using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;

namespace SevenSteps.Game
{
    /// How a body's walk ended (Walking = the body playing now, or choosing KEEP / SWITCH).
    public enum TrailEnd { Walking, Kept, Switched, Fell, Bridged, Sealed, Vanished, Won }

    /// One body's footprints from its wake-up cell (step 0) to where it ended.
    public sealed class TrailBody
    {
        public int Index;
        public Footprint[] Steps;
        public TrailEnd End;
    }

    public enum TrailMarkKind { Rim, Line, Seventh, Star, StoneStar, Ring }

    /// One drawn piece of the star trail. Equal marks draw the same thing, so a view can diff two trails by value.
    public readonly struct TrailMark : IEquatable<TrailMark>
    {
        public readonly TrailMarkKind Kind;
        public readonly int Body;
        /// Cell of the mark; for a line, the cell it starts from.
        public readonly int Cell;
        /// Line: the cell it goes to. Rim: how many earlier bodies already ring this cell (inset). Star: slot on the cell.
        /// Ring: the star's place 1..7 around the feet.
        public readonly int Aux;
        /// Step number (1-7); 0 for rims, lines from the wake-up cell and stone stars.
        public readonly int Step;
        /// The star of a fall: dimmed (the step let go). A bridge's 7th: sunk. A ring star: spent.
        public readonly bool Dim;

        public TrailMark(TrailMarkKind kind, int body, int cell, int aux, int step, bool dim = false)
        {
            Kind = kind; Body = body; Cell = cell; Aux = aux; Step = step; Dim = dim;
        }

        public bool Equals(TrailMark o) => Kind == o.Kind && Body == o.Body && Cell == o.Cell && Aux == o.Aux && Step == o.Step && Dim == o.Dim;
        public override bool Equals(object obj) => obj is TrailMark o && Equals(o);
        public override int GetHashCode() => ((((int)Kind * 31 + Body) * 397 + Cell) * 397 + Aux) * 31 + Step * 2 + (Dim ? 1 : 0);
        public override string ToString() => Kind == TrailMarkKind.Line ? $"Line b{Body} {Cell}->{Aux}" : $"{Kind} b{Body} @{Cell} s{Step} #{Aux}{(Dim ? " dim" : "")}";
    }

    /// <summary>
    /// The star trail (design/art/star-trail.md, production/star-trail-plan.md) worked out from a State alone: the body
    /// playing now from <see cref="State.Trail"/>, earlier bodies from the undo chain (<see cref="State.Prev"/>). Nothing is
    /// accumulated from events, so Undo, Restart and replays draw exactly what the state holds. Plain C#, no rule logic
    /// beyond calling <see cref="Rules"/>.
    /// </summary>
    public sealed class StarTrail
    {
        public readonly List<TrailBody> Bodies = new List<TrailBody>();
        /// Statues standing now with the body that left each (KEEP, or the last body's 7th step).
        public readonly List<(int Cell, int Body)> Stones = new List<(int, int)>();
        /// The body playing now stands here with this many steps used (the ring of seven stars); Ring is false when no
        /// body is walking (choosing KEEP / SWITCH, won, lost).
        public int Pos, Used;
        public bool Ring;

        static readonly Dir[] Dirs = { Dir.Up, Dir.Down, Dir.Left, Dir.Right };

        /// <param name="known">Optional: the StepResult that produced a state, when the caller saw it (GameSession Present).
        /// Used for a body that ended inside a move (fall, bridge, seal, vanish, last body's stone), whose last steps are
        /// not in any state's Trail; without it the move is found again by trying the four directions from the state before.</param>
        public static StarTrail Of(State s, Func<State, StepResult> known = null)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var chain = new List<State>();
            for (var t = s; t != null; t = t.Prev) chain.Add(t);
            chain.Reverse();

            var trail = new StarTrail();
            for (int i = 1; i < chain.Count; i++)
            {
                State a = chain[i - 1], b = chain[i];
                if (b.Bodies < a.Bodies) trail.Bodies.Add(Ended(a, b, trail.Bodies.Count, known));
            }
            if (s.Status != Status.Lose)
                trail.Bodies.Add(new TrailBody { Index = trail.Bodies.Count, Steps = s.Trail, End = s.Status == Status.Win ? TrailEnd.Won : TrailEnd.Walking });
            foreach (int cell in s.Statues) trail.Stones.Add((cell, BodyIndex.OfStatue(s, cell)));
            trail.Pos = s.Pos;
            trail.Used = s.Used;
            trail.Ring = s.Status == Status.Play;
            return trail;
        }

        /// The body spent between a and b (b = the command's result).
        static TrailBody Ended(State a, State b, int index, Func<State, StepResult> known)
        {
            if (a.Status == Status.Choose)
                return new TrailBody { Index = index, Steps = a.Trail, End = Rules.Contains(b.Statues, a.Pos) ? TrailEnd.Kept : TrailEnd.Switched };

            var r = known?.Invoke(b);
            if (r == null)
            {
                string key = Rules.Key(b);
                foreach (var d in Dirs)
                {
                    var tried = Rules.Step(a, d);
                    if (!ReferenceEquals(tried.State, a) && Rules.Key(tried.State) == key) { r = tried; break; }
                }
            }
            if (r == null) return new TrailBody { Index = index, Steps = a.Trail, End = TrailEnd.Fell }; // not reachable by one move

            int step = 0;
            var end = TrailEnd.Fell;
            foreach (var e in r.Events)
            {
                switch (e.Type)
                {
                    case GameEventType.Fall: end = TrailEnd.Fell; break;
                    case GameEventType.Bridge: end = TrailEnd.Bridged; break;
                    case GameEventType.Seal: end = TrailEnd.Sealed; break;
                    case GameEventType.Vanish: end = TrailEnd.Vanished; break;
                    case GameEventType.Stone: end = TrailEnd.Kept; break;
                    default: continue;
                }
                step = e.StepIdx;
                break;
            }
            var steps = new List<Footprint>(a.Trail);
            foreach (int cell in r.Path) steps.Add(new Footprint(cell, step));
            return new TrailBody { Index = index, Steps = steps.ToArray(), End = end };
        }

        /// What to draw, in draw order (rims, lines, 7th-step glyphs, stars, stone stars):
        /// - a rim on every cell a body stepped on (inset by the number of earlier bodies ringing that cell);
        /// - a line between each pair of consecutive cells of a body (ice slides too);
        /// - a star for steps 1-6 where the step ended (not on the cells an ice slide glides over); a mud cell (entered
        ///   with 2 steps) gets both numbers' stars; stars on one cell take the next free slot;
        /// - the Big Dipper glyph on the 7th step's cell; the star of a fall is dim;
        /// - a star on top of each statue;
        /// - the ring of seven stars at the walking body's feet: star k is spent (Dim) once k steps are used, so the lit
        ///   stars are the steps left and star 7 is the last one (the view pulses it after the 6th step).
        public List<TrailMark> Marks()
        {
            var rims = new List<TrailMark>();
            var lines = new List<TrailMark>();
            var sevenths = new List<TrailMark>();
            var stars = new List<TrailMark>();
            var rimCount = new Dictionary<int, int>();
            var slotCount = new Dictionary<int, int>();
            var ringed = new HashSet<int>();

            foreach (var body in Bodies)
            {
                var f = body.Steps;
                ringed.Clear();
                for (int k = 1; k < f.Length; k++)
                {
                    int cell = f[k].Cell, step = f[k].Step;
                    lines.Add(new TrailMark(TrailMarkKind.Line, body.Index, f[k - 1].Cell, cell, step));
                    if (ringed.Add(cell))
                    {
                        rimCount.TryGetValue(cell, out int inset);
                        rims.Add(new TrailMark(TrailMarkKind.Rim, body.Index, cell, inset, 0));
                        rimCount[cell] = inset + 1;
                    }
                    if (k + 1 < f.Length && f[k + 1].Step == step) continue; // gliding over this ice cell
                    bool last = k == f.Length - 1;
                    bool dim = last && body.End == TrailEnd.Fell;
                    int first = step - f[k - 1].Step == 2 ? step - 1 : step; // mud: two steps here
                    for (int n = first; n <= step; n++)
                    {
                        if (n >= Rules.Steps)
                        {
                            // A brother who became a bridge (pit or crack sealed): the sunken star, drawn dim.
                            bool sunk = body.End == TrailEnd.Bridged || body.End == TrailEnd.Sealed;
                            sevenths.Add(new TrailMark(TrailMarkKind.Seventh, body.Index, cell, 0, n, sunk));
                            continue;
                        }
                        slotCount.TryGetValue(cell, out int slot);
                        stars.Add(new TrailMark(TrailMarkKind.Star, body.Index, cell, slot, n, dim && n == step));
                        slotCount[cell] = slot + 1;
                    }
                }
            }

            var all = new List<TrailMark>(rims.Count + lines.Count + sevenths.Count + stars.Count + Stones.Count);
            all.AddRange(rims);
            all.AddRange(lines);
            all.AddRange(sevenths);
            all.AddRange(stars);
            foreach (var (cell, body) in Stones) all.Add(new TrailMark(TrailMarkKind.StoneStar, body, cell, 0, 0));
            var cur = Current;
            if (Ring && cur != null)
                for (int k = 1; k <= Rules.Steps; k++) all.Add(new TrailMark(TrailMarkKind.Ring, cur.Index, Pos, k, k, k <= Used));
            return all;
        }

        /// The body playing now (Walking or Won), or null after the last body is spent.
        public TrailBody Current => Bodies.Count > 0 && (Bodies[Bodies.Count - 1].End == TrailEnd.Walking || Bodies[Bodies.Count - 1].End == TrailEnd.Won)
            ? Bodies[Bodies.Count - 1] : null;

        /// Fills <paramref name="map"/> (docs/architecture.md 15.2) from scratch: step bits where a step landed (the cells
        /// an ice slide glides over carry only path edges, like the stars), both bits on a mud cell, path edges, statues,
        /// how each finished brother ended (on his last cell), the walking body's cell (the ring), and with
        /// <paramref name="newest"/> the current body's last step cell. Brothers past <see cref="StarTrailMap.MaxBodies"/> are left out.
        public void Write(StarTrailMap map, bool newest)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            map.Clear();
            foreach (var m in Marks())
            {
                if (m.Body < 0 || m.Body >= StarTrailMap.MaxBodies) continue;
                switch (m.Kind)
                {
                    case TrailMarkKind.Line: map.AddPath(m.Body, m.Cell, m.Aux); break;
                    case TrailMarkKind.Star:
                    case TrailMarkKind.Seventh: map.AddStep(m.Body, m.Cell, m.Step); break;
                    case TrailMarkKind.StoneStar: map.AddStatue(m.Body, m.Cell); break;
                }
            }
            foreach (var b in Bodies)
            {
                if (b.Index >= StarTrailMap.MaxBodies || b.Steps.Length < 2) continue;
                int last = b.Steps[b.Steps.Length - 1].Cell;
                byte end = b.End switch
                {
                    TrailEnd.Switched => StarTrailMap.EndSwitch,
                    TrailEnd.Bridged => StarTrailMap.EndBridge,
                    TrailEnd.Sealed => StarTrailMap.EndBridge,
                    TrailEnd.Fell => StarTrailMap.EndFell,
                    TrailEnd.Vanished => StarTrailMap.EndVanish,
                    _ => 0,
                };
                if (end != 0) map.SetEnd(last, end);
            }
            var cur = Current;
            if (newest && cur != null && cur.Steps.Length > 1) map.SetNewest(cur.Steps[cur.Steps.Length - 1].Cell);
            // The ring of seven stars stands where the walking body is (15.6); its lit stars come from _StarTrailState.w.
            if (Ring && cur != null) map.SetPlayer(Pos);
        }
    }
}
