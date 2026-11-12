using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;

namespace SevenSteps.Tests
{
    /// StarTrail (design/art/star-trail.md): the trail is worked out from the State (Trail + undo chain) alone, so
    /// Undo, Restart and replays draw what the state holds.
    public sealed class StarTrailTests
    {
        static Level Map(int bodies, params string[] map) => Level.Parse("t", "", bodies, map);

        static State Run(State s, string letters)
        {
            foreach (char c in letters)
            {
                if (c == 'Z') { s = Rules.Dispatch(s, Command.Undo).State; continue; }
                if (c == 'N') { s = Rules.Dispatch(s, Command.Restart).State; continue; }
                Assert.IsTrue(Rules.TryParseLetter(c, out var act), $"letter {c}");
                s = Rules.Dispatch(s, Command.FromAct(act)).State;
            }
            return s;
        }

        static List<TrailMark> Marks(State s) => StarTrail.Of(s).Marks();
        static List<TrailMark> Of(List<TrailMark> marks, TrailMarkKind kind) => marks.Where(m => m.Kind == kind).ToList();

        // Two rows of 8: body 0 walks the top row to cell 7, body 1 the bottom row to cell 14.
        static readonly string[] Field = { "S.......", "........" };

        [Test]
        public void OneBodyWalking()
        {
            var marks = Marks(Run(Rules.NewState(Map(1, "S........")), "RRR"));
            Assert.AreEqual(3, Of(marks, TrailMarkKind.Line).Count);
            Assert.AreEqual(3, Of(marks, TrailMarkKind.Rim).Count);
            var stars = Of(marks, TrailMarkKind.Star);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, stars.Select(m => m.Step));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, stars.Select(m => m.Cell));
            Assert.IsTrue(stars.All(m => m.Aux == 0 && m.Body == 0 && !m.Dim));
            Assert.AreEqual(new TrailMark(TrailMarkKind.Line, 0, 0, 1, 1), Of(marks, TrailMarkKind.Line)[0], "first line from the wake-up cell");
        }

        [Test]
        public void ThreeBodiesSeventhKeepAndSwitch()
        {
            var s = Run(Rules.NewState(Map(3, Field)), "RRRRRRRK" + "DRRRRRRX" + "L");
            var trail = StarTrail.Of(s);
            CollectionAssert.AreEqual(new[] { TrailEnd.Kept, TrailEnd.Switched, TrailEnd.Walking }, trail.Bodies.Select(b => b.End));
            var marks = trail.Marks();
            CollectionAssert.AreEqual(new[] { 7, 14 }, Of(marks, TrailMarkKind.Seventh).Select(m => m.Cell), "the 7th step's glyph, not a star");
            Assert.IsFalse(Of(marks, TrailMarkKind.Star).Any(m => m.Step >= 7));
            Assert.AreEqual(6 + 6 + 1, Of(marks, TrailMarkKind.Star).Count);
            var stone = Of(marks, TrailMarkKind.StoneStar).Single();
            Assert.AreEqual((7, 0), (stone.Cell, stone.Body), "KEEP: a star on body 0's stone");
            // Cell 13: body 1 stepped there (6th), body 2 too (1st): the second rim sits inside, the second star takes slot 1.
            var rims13 = Of(marks, TrailMarkKind.Rim).Where(m => m.Cell == 13).ToList();
            CollectionAssert.AreEqual(new[] { (1, 0), (2, 1) }, rims13.Select(m => (m.Body, m.Aux)));
            CollectionAssert.AreEqual(new[] { (1, 6, 0), (2, 1, 1) }, Of(marks, TrailMarkKind.Star).Where(m => m.Cell == 13).Select(m => (m.Body, m.Step, m.Aux)));
            Assert.AreEqual(new TrailMark(TrailMarkKind.Line, 2, 14, 13, 1), Of(marks, TrailMarkKind.Line).Last(), "SWITCH: body 2 sets off from body 1's 7th cell");
        }

        [Test]
        public void V2StatesKeepHardensSwitchKnots()
        {
            var level = Map(3, Field);
            var choosing = Run(Rules.NewState(level), "RRRRRRR");
            Assert.IsTrue(Marks(choosing).Where(m => m.Kind != TrailMarkKind.Ring).All(m => m.End == TrailEnd.Walking), "choosing: still the active look");
            var kept = Marks(Run(choosing, "K"));
            Assert.IsTrue(kept.Where(m => m.Body == 0 && m.Kind != TrailMarkKind.StoneStar).All(m => m.End == TrailEnd.Kept), "KEEP: body 0 hardens");
            Assert.IsEmpty(Of(kept, TrailMarkKind.Knot));

            var s = Run(Rules.NewState(level), "RRRRRRRK" + "DRRRRRRX" + "L");
            var marks = Marks(s);
            var knot = Of(marks, TrailMarkKind.Knot).Single();
            Assert.AreEqual((1, 14, 2, TrailEnd.Switched), (knot.Body, knot.Cell, knot.Aux, knot.End), "SWITCH: the knot of body 1 and 2 on body 1's last cell");
            Assert.IsTrue(Of(marks, TrailMarkKind.Star).Where(m => m.Body == 1).All(m => m.End == TrailEnd.Switched));
            Assert.IsTrue(Of(marks, TrailMarkKind.Star).Where(m => m.Body == 2).All(m => m.End == TrailEnd.Walking));
            // Undo past the SWITCH: the knot goes, body 1 is active (choosing) again.
            var back = Marks(Run(s, "ZZ"));
            Assert.IsEmpty(Of(back, TrailMarkKind.Knot));
            Assert.IsTrue(Of(back, TrailMarkKind.Star).Where(m => m.Body == 1).All(m => m.End == TrailEnd.Walking));
        }

        [Test]
        public void UndoAndRestartRedrawFromTheState()
        {
            var level = Map(3, Field);
            var s0 = Rules.NewState(level);
            // (played, one more legal command): undoing that command gives back exactly the trail of "played".
            foreach (var (played, more) in new[] { ("RRRRRRRK" + "DR", "R"), ("RRRRRRRK", "D"), ("RRRRRR", "R"), ("RRRRRRR", "K"), ("RRRRRRRK" + "DRRRRRRX", "L") })
            {
                var before = Run(s0, played);
                var after = Run(before, more);
                Assert.AreNotSame(before, after, $"{more} after {played} is legal");
                CollectionAssert.AreNotEqual(Marks(before), Marks(after));
                CollectionAssert.AreEqual(Marks(before), Marks(Run(after, "Z")), $"undo after {played}{more}");
            }
            // Undo across KEEP: the stone, its star and the body's 7th glyph go; body 0 is choosing again.
            var back = Run(s0, "RRRRRRRK" + "Z");
            var m = Marks(back);
            Assert.IsEmpty(Of(m, TrailMarkKind.StoneStar));
            Assert.AreEqual(7, Of(m, TrailMarkKind.Seventh).Single().Cell);
            Assert.AreEqual(TrailEnd.Walking, StarTrail.Of(back).Bodies.Single().End);
            var fresh = Marks(Run(s0, "RRRRRRRK" + "DRR" + "N"));
            Assert.IsTrue(fresh.Count == Rules.Steps && fresh.All(x => x.Kind == TrailMarkKind.Ring && x.Body == 0 && !x.Dim), "Restart: only the first brother's full ring");
        }

        [Test]
        public void ReplayDrawsTheSameTrail()
        {
            const string moves = "RRRRRRRK" + "DRRRRRRX" + "LL";
            CollectionAssert.AreEqual(Marks(Run(Rules.NewState(Map(3, Field)), moves)), Marks(Run(Rules.NewState(Map(3, Field)), moves)));
        }

        [Test]
        public void IceGlideHasLinesButOneStar()
        {
            var s = Run(Rules.NewState(Map(1, "S~~..")), "R");
            Assert.AreEqual(3, s.Pos);
            var marks = Marks(s);
            Assert.AreEqual(3, Of(marks, TrailMarkKind.Line).Count);
            Assert.AreEqual(3, Of(marks, TrailMarkKind.Rim).Count);
            var star = Of(marks, TrailMarkKind.Star).Single();
            Assert.AreEqual((3, 1), (star.Cell, star.Step), "one step: the star where the slide stops");
        }

        [Test]
        public void MudCellHoldsBothSteps()
        {
            var s = Run(Rules.NewState(Map(1, "Sm..")), "RR");
            Assert.AreEqual(3, s.Used);
            var stars = Of(Marks(s), TrailMarkKind.Star);
            CollectionAssert.AreEqual(new[] { (1, 1, 0), (1, 2, 1), (2, 3, 0) }, stars.Select(m => (m.Cell, m.Step, m.Aux)));
        }

        [Test]
        public void FallKeepsTheLastStepDimmed()
        {
            var s = Run(Rules.NewState(Map(2, "S.o.")), "RR");
            var trail = StarTrail.Of(s);
            Assert.AreEqual(2, trail.Bodies.Count);
            Assert.AreEqual(TrailEnd.Fell, trail.Bodies[0].End);
            var stars = Of(trail.Marks(), TrailMarkKind.Star);
            CollectionAssert.AreEqual(new[] { (1, false), (2, true) }, stars.Select(m => (m.Cell, m.Dim)), "the pit cell's star, dimmed");

            var lost = Run(Rules.NewState(Map(1, "S.o.")), "RR");
            Assert.AreEqual(Status.Lose, lost.Status);
            Assert.AreEqual(TrailEnd.Fell, StarTrail.Of(lost).Bodies.Single().End);
            Assert.IsTrue(Of(Marks(lost), TrailMarkKind.Star).Last().Dim);
        }

        [Test]
        public void PresentedResultWinsOverAGuess()
        {
            // Falling left or right leaves the same state; only the presented StepResult says which pit it was.
            var level = Map(2, "oSo");
            var s0 = Rules.NewState(level);
            var r = Rules.Dispatch(s0, Command.Move(Dir.Right));
            Assert.AreEqual(Rules.Key(Run(s0, "L")), Rules.Key(r.State));
            Assert.AreEqual(2, StarTrail.Of(r.State, st => ReferenceEquals(st, r.State) ? r : null).Bodies[0].Steps.Last().Cell);
            Assert.AreEqual(0, StarTrail.Of(r.State).Bodies[0].Steps.Last().Cell, "without it: the first direction that fits");
        }

        static byte Byte(StarTrailMap map, int cell, int layer, int channel) => map.Bytes[map.Index(cell, layer, channel)];

        [Test]
        public void WritesTheShaderMap()
        {
            var level = Map(3, Field);
            var s = Run(Rules.NewState(level), "RRRRRRRK" + "DRRRRRRX" + "L");
            var map = new StarTrailMap(level.W, level.H);
            StarTrail.Of(s).Write(map, true);
            Assert.AreEqual(1 << 6 | StarTrailMap.StatueBit, Byte(map, 7, 0, 0), "body 0: 7th step and KEEP statue on cell 7");
            Assert.AreEqual(1 << 0, Byte(map, 1, 0, 0));
            Assert.AreEqual(1 << 6, Byte(map, 14, 0, 1), "body 1's 7th on cell 14");
            Assert.AreEqual(StarTrailMap.EndSwitch, Byte(map, 14, 1, 3), "SWITCH seam on the new start");
            Assert.AreEqual(1 << 5, Byte(map, 13, 0, 1));
            Assert.AreEqual(1 << 0, Byte(map, 13, 0, 2), "body 2's first step");
            Assert.AreEqual(StarTrailMap.NewestBit, Byte(map, 13, 0, 3), "newest step cell; ReservedBit 0 (no ring at the feet since 2026-11-12 C)");
            Assert.AreEqual(StarTrailMap.EdgeLeft, Byte(map, 14, 1, 2));
            Assert.AreEqual(StarTrailMap.EdgeRight, Byte(map, 13, 1, 2));

            var snap = new StarTrailMap(level.W, level.H);
            StarTrail.Of(s).Write(snap, false);
            Assert.AreEqual(0, Byte(snap, 13, 0, 3), "no newest cell on Snap / Rebuild");
            StarTrail.Of(Run(s, "N")).Write(snap, false);
            Assert.IsTrue(snap.Bytes.All(b => b == 0), "Restart: an empty map");
            StarTrail.Of(Run(Rules.NewState(level), "RRRRRRR")).Write(snap, false);
            Assert.IsFalse(Enumerable.Range(0, level.Count).Any(c => (Byte(snap, c, 0, 3) & StarTrailMap.ReservedBit) != 0), "ReservedBit never set");
        }

        [Test]
        public void MapIceMudAndFall()
        {
            var ice = Map(1, "S~~..");
            var map = new StarTrailMap(ice.W, ice.H);
            StarTrail.Of(Run(Rules.NewState(ice), "R")).Write(map, false);
            Assert.AreEqual(0, Byte(map, 1, 0, 0), "glided over: no step bit");
            Assert.AreEqual(StarTrailMap.EdgeLeft | StarTrailMap.EdgeRight, Byte(map, 1, 1, 0), "but both path edges");
            Assert.AreEqual(1, Byte(map, 3, 0, 0));

            var mud = Map(1, "Sm..");
            map = new StarTrailMap(mud.W, mud.H);
            StarTrail.Of(Run(Rules.NewState(mud), "R")).Write(map, false);
            Assert.AreEqual(1 << 0 | 1 << 1, Byte(map, 1, 0, 0), "mud: steps 1 and 2 on one cell");

            var pit = Map(2, "S.o.");
            map = new StarTrailMap(pit.W, pit.H);
            StarTrail.Of(Run(Rules.NewState(pit), "RR")).Write(map, false);
            Assert.AreEqual(1 << 1, Byte(map, 2, 0, 0), "the fall's step on the pit");
            Assert.AreEqual(StarTrailMap.EndFell, Byte(map, 2, 1, 3));
        }

        [Test]
        public void RingOfSevenStarsCountsTheStepsLeft()
        {
            var level = Map(3, Field);
            var s = Run(Rules.NewState(level), "RRR");
            var ring = Of(Marks(s), TrailMarkKind.Ring);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7 }, ring.Select(m => m.Aux));
            Assert.IsTrue(ring.All(m => m.Cell == s.Pos && m.Body == 0));
            CollectionAssert.AreEqual(new[] { true, true, true, false, false, false, false }, ring.Select(m => m.Dim), "3 spent, 4 left");

            var six = Run(s, "RRR");
            Assert.AreEqual(6, six.Used);
            Assert.IsFalse(Of(Marks(six), TrailMarkKind.Ring).Last().Dim, "one left: star 7 lit (the view pulses it)");
            Assert.IsEmpty(Of(Marks(Run(six, "R")), TrailMarkKind.Ring), "choosing KEEP / SWITCH: no ring");
            var next = Of(Marks(Run(six, "RK")), TrailMarkKind.Ring);
            Assert.IsTrue(next.All(m => m.Body == 1 && m.Cell == level.Start && !m.Dim), "the next brother wakes with seven");
            Assert.IsEmpty(Of(Marks(Run(Rules.NewState(Map(1, "S......E")), "RRRRRRR")), TrailMarkKind.Ring), "won: no ring");
        }

        [Test]
        public void WinningBodyEndsWon()
        {
            var s = Run(Rules.NewState(Map(1, "S......E")), "RRRRRRR");
            Assert.AreEqual(Status.Win, s.Status);
            var trail = StarTrail.Of(s);
            Assert.AreEqual(TrailEnd.Won, trail.Bodies.Single().End);
            Assert.AreEqual(7, Of(trail.Marks(), TrailMarkKind.Seventh).Single().Cell);
        }
    }
}
