using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Core;

namespace SevenSteps.Tests
{
    /// Contracts of the core API (docs/architecture.md 3) that the golden comparison does not state directly:
    /// immutability, undo by snapshot, event order, enum values, parsing edge cases.
    public sealed class RulesTests
    {
        static Level Map(int bodies, params string[] map) => Level.Parse("t", "", bodies, map);

        static State Run(State s, string commands)
        {
            foreach (char c in commands) s = Rules.Dispatch(s, Golden.ToCommand(c)).State;
            return s;
        }

        /// Every command of all golden traces with the state it was applied to and its result.
        static IEnumerable<(State prev, char cmd, StepResult r, string where)> AllResults()
        {
            foreach (var t in Golden.Traces.traces)
            {
                var s = Rules.NewState(Golden.ToLevel(t.level));
                for (int k = 0; k < t.commands.Length; k++)
                {
                    var r = Rules.Dispatch(s, Golden.ToCommand(t.commands[k]));
                    yield return (s, t.commands[k], r, $"{t.id} #{k} '{t.commands[k]}'");
                    s = r.State;
                }
            }
        }

        // ---- API shape -----------------------------------------------------------------------------------

        [Test]
        public void EventEnumOnlyGrowsAtTheEnd()
        {
            // Values may be saved or logged: never renumber, only append.
            string[] order = { "Bump", "Step", "Crumble", "Bridge", "Fall", "Win", "Seal", "Vanish", "Choose", "Stone", "Switch", "Respawn", "Lose", "Undo", "Restart", "Slide", "DoorOpen", "DoorClose", "Flip" };
            for (int k = 0; k < order.Length; k++) Assert.AreEqual(order[k], ((GameEventType)k).ToString());
            Assert.AreEqual(order.Length, Enum.GetValues(typeof(GameEventType)).Length, "new events go at the end of this list too");
        }

        [Test]
        public void ReplayLettersRoundTrip()
        {
            foreach (Act a in Enum.GetValues(typeof(Act)))
            {
                Assert.IsTrue(Rules.TryParseLetter(Rules.Letter(a), out var back));
                Assert.AreEqual(a, back);
            }
            Assert.IsFalse(Rules.TryParseLetter('Q', out _));
        }

        // ---- immutability and undo -----------------------------------------------------------------------

        [Test]
        public void DispatchNeverChangesThePreviousState()
        {
            foreach (var t in Golden.Traces.traces)
            {
                var s = Rules.NewState(Golden.ToLevel(t.level));
                for (int k = 0; k < t.commands.Length; k++)
                {
                    string where = $"{t.id} #{k} '{t.commands[k]}'";
                    string before = Golden.Describe(s);
                    var r = Rules.Dispatch(s, Golden.ToCommand(t.commands[k]));
                    Assert.AreEqual(before, Golden.Describe(s), $"{where}: previous state was modified");
                    var again = Rules.Dispatch(s, Golden.ToCommand(t.commands[k]));
                    Assert.AreEqual(Golden.Describe(r.State), Golden.Describe(again.State), $"{where}: not deterministic");
                    Assert.AreEqual(Golden.Describe(r.Events), Golden.Describe(again.Events), $"{where}: events not deterministic");
                    s = r.State;
                }
            }
        }

        [Test]
        public void UndoReturnsThePreviousObject()
        {
            foreach (var (prev, cmd, r, where) in AllResults())
            {
                if (cmd == 'Z' || cmd == '*' || ReferenceEquals(r.State, prev)) continue;
                Assert.AreSame(prev, r.State.Prev, $"{where}: Prev link");
                var undo = Rules.Dispatch(r.State, Command.Undo);
                Assert.AreSame(prev, undo.State, $"{where}: undo must hand back the same snapshot");
                CollectionAssert.AreEqual(new[] { GameEventType.Undo }, undo.Events.ConvertAll(e => e.Type), where);
            }
        }

        [Test]
        public void UndoAtTheBeginningDoesNothing()
        {
            var s = Rules.NewState(Map(3, "#S..#"));
            var r = Rules.Dispatch(s, Command.Undo);
            Assert.AreSame(s, r.State);
            Assert.IsEmpty(r.Events);
        }

        [Test]
        public void RestartDropsTheUndoChain()
        {
            var s = Run(Rules.NewState(Map(3, "#S......#")), "RRR");
            var r = Rules.Dispatch(s, Command.Restart);
            Assert.IsNull(r.State.Prev);
            Assert.AreEqual(r.State.Level.Start, r.State.Pos);
            Assert.AreEqual(3, r.State.Bodies);
            CollectionAssert.AreEqual(new[] { GameEventType.Restart }, r.Events.ConvertAll(e => e.Type));
            Assert.AreSame(r.State, Rules.Dispatch(r.State, Command.Undo).State, "nothing to undo after restart");
        }

        [Test]
        public void RejectedCommandsReturnTheSameObject()
        {
            var s = Rules.NewState(Map(2, "#S.#"));
            var bump = Rules.Dispatch(s, Command.Move(Dir.Left));
            Assert.AreSame(s, bump.State);
            CollectionAssert.AreEqual(new[] { GameEventType.Bump }, bump.Events.ConvertAll(e => e.Type));
            Assert.AreSame(s, Rules.Dispatch(s, Command.Keep).State, "KEEP outside the choice");
            Assert.AreSame(s, Rules.Dispatch(s, Command.Switch).State, "SWITCH outside the choice");
            Assert.IsEmpty(Rules.Dispatch(s, Command.Keep).Events);
        }

        // ---- event order (docs/architecture.md 3.3) ------------------------------------------------------

        [Test]
        public void EventOrderFollowsTheContract()
        {
            int slides = 0, doors = 0;
            foreach (var (prev, _, r, where) in AllResults())
            {
                var types = r.Events.ConvertAll(e => e.Type);
                for (int k = 0; k < types.Count; k++)
                {
                    var t = types[k];
                    if (t == GameEventType.Respawn || t == GameEventType.Lose)
                        Assert.AreEqual(types.Count - 1, k, $"{where}: Respawn/Lose must be last ({Golden.Describe(r.Events)})");
                    if (t == GameEventType.Crumble) Assert.AreEqual(0, k, $"{where}: Crumble first");
                    if (t == GameEventType.Slide)
                    {
                        slides++;
                        Assert.AreEqual(types.Contains(GameEventType.Crumble) ? 1 : 0, k, $"{where}: Slide right after Crumble");
                        Assert.AreEqual(r.Path[0], r.Events[k].At, $"{where}: Slide.At is the first ice cell");
                        Assert.AreEqual(0, r.Events[k].StepIdx, $"{where}: Slide carries no step number");
                    }
                    if (t == GameEventType.DoorOpen || t == GameEventType.DoorClose)
                    {
                        doors++;
                        var before = types[k - 1];
                        Assert.IsTrue(before == GameEventType.Stone || before == GameEventType.DoorOpen || before == GameEventType.DoorClose,
                            $"{where}: door events right after Stone ({Golden.Describe(r.Events)})");
                        char door = prev.Level.Tile(r.Events[k].At);
                        bool opens = t == GameEventType.DoorOpen;
                        Assert.AreEqual(opens, Rules.DoorOpen(r.State, door), $"{where}: door state after");
                        Assert.AreEqual(!opens, Rules.DoorOpen(prev, door), $"{where}: door state before");
                        Assert.AreEqual(0, r.Events[k].StepIdx, $"{where}: door events carry no step number");
                    }
                }
                Assert.AreEqual(r.Path.Length > 1, types.Contains(GameEventType.Slide), $"{where}: Slide iff the path is longer than one cell");
            }
            Assert.Greater(slides, 0, "golden traces never slide");
            Assert.Greater(doors, 0, "golden traces never open or close a door");
        }

        [Test]
        public void EveryDoorChangeGetsAnEvent()
        {
            foreach (var (prev, _, r, where) in AllResults())
            {
                if (r.Has(GameEventType.Undo) || r.Has(GameEventType.Restart)) continue;
                var changed = new List<string>();
                for (int i = 0; i < prev.Level.Count; i++)
                {
                    char t = prev.Level.Tile(i);
                    if (!Rules.IsDoor(t)) continue;
                    bool was = Rules.DoorOpen(prev, t), now = Rules.DoorOpen(r.State, t);
                    if (was != now) changed.Add((now ? "open@" : "close@") + i);
                }
                var events = new List<string>();
                foreach (var e in r.Events)
                {
                    if (e.Type == GameEventType.DoorOpen) events.Add("open@" + e.At);
                    if (e.Type == GameEventType.DoorClose) events.Add("close@" + e.At);
                }
                CollectionAssert.AreEqual(changed, events, where);
            }
        }

        // ---- statue-count door (Q) ----------------------------------------------------------------------

        static readonly string[] CountCorridor = { "##########", "#S.......#", "#Q########", "##########" };

        [Test]
        public void CountDoorOpensOnlyAtTheExactCount()
        {
            var s = Rules.NewState(Level.Parse("t", "", 4, CountCorridor));
            Assert.AreEqual(0, Rules.StatueCount(s));
            Assert.IsTrue(Rules.DoorOpen(s, 'Q'), "statueCount 0 (the default): open with no statues");

            var l1 = Level.Parse("t", "", 4, CountCorridor, 1);
            s = Rules.NewState(l1);
            Assert.AreEqual(1, l1.StatueCount);
            Assert.IsFalse(Rules.DoorOpen(s, 'Q'));
            Assert.IsFalse(Rules.Passable(s, 21, 1));
            s = Run(s, "RRRRRRRK");
            Assert.AreEqual(1, Rules.StatueCount(s));
            Assert.IsTrue(Rules.DoorOpen(s, 'Q'));
            Assert.IsTrue(Rules.Passable(s, 21, 1));
            s = Run(s, "RRRRRRLK");
            Assert.AreEqual(2, Rules.StatueCount(s));
            Assert.IsFalse(Rules.DoorOpen(s, 'Q'), "one statue too many closes it");
        }

        [Test]
        public void BridgesAndSealsAreNotStatues()
        {
            var pit = Run(Rules.NewState(Map(3, "#########", "#S..o...#", "#########")), "RRLRLRR");
            Assert.AreEqual(1, pit.Bridges.Length);
            Assert.AreEqual(0, Rules.StatueCount(pit));
            var crack = Run(Rules.NewState(Map(3, "##########", "#S....x..#", "##########")), "RRRRLRR");
            Assert.AreEqual(1, crack.Bridges.Length);
            Assert.AreEqual(0, Rules.StatueCount(crack));
        }

        // ---- lever (L) and flip walls (R raised, r lowered) ----------------------------------------------

        [Test]
        public void FlipEventMatchesTheFlippedField()
        {
            int flips = 0;
            foreach (var (prev, cmd, r, where) in AllResults())
            {
                if (cmd == 'Z' || cmd == '*') continue;
                bool flipped = r.State.Flipped != prev.Flipped;
                Assert.AreEqual(flipped, r.Has(GameEventType.Flip), $"{where}: Flip event iff Flipped changed");
                if (!flipped) continue;
                flips++;
                var e = r.Events.Find(x => x.Type == GameEventType.Flip);
                Assert.AreEqual('L', prev.Level.Tile(e.At), $"{where}: Flip.At is the lever");
                Assert.AreEqual(r.Path[r.Path.Length - 1], e.At, $"{where}: the move ended on the lever");
                Assert.AreNotEqual(prev.Start, e.At, $"{where}: a lever on the start is never pulled");
                Assert.AreEqual(Rules.Steps, prev.Used + 1, $"{where}: only the 7th step pulls");
                Assert.AreEqual(0, e.StepIdx, $"{where}: Flip carries no step number");
                int k = r.Events.IndexOf(e);
                var after = r.Events[k + 1].Type;
                Assert.IsTrue(after == GameEventType.Choose || after == GameEventType.Stone, $"{where}: Flip comes before Choose/Stone ({Golden.Describe(r.Events)})");
            }
            Assert.Greater(flips, 0, "golden traces never pull a lever");
        }

        [Test]
        public void FlipWallsReadThroughTileAt()
        {
            var s = Rules.NewState(Map(3, "##########", "#S......L#", "#Rr######", "##########"));
            int raised = 21, lowered = 22;
            Assert.AreEqual('R', Rules.TileAt(s, raised));
            Assert.AreEqual('r', Rules.TileAt(s, lowered));
            Assert.IsFalse(Rules.Passable(s, raised, 1));
            Assert.IsTrue(Rules.Passable(s, lowered, 1));
            s = Run(s, "RRRRRRR");
            Assert.IsTrue(s.Flipped);
            Assert.AreEqual('r', Rules.TileAt(s, raised), "R reads as lowered after a pull");
            Assert.AreEqual('R', Rules.TileAt(s, lowered), "r reads as raised after a pull");
            Assert.IsTrue(Rules.Passable(s, raised, 1));
            Assert.IsFalse(Rules.Passable(s, lowered, 1));
            Assert.AreEqual('R', s.Level.Tile(raised), "the level itself never changes");
            Assert.IsTrue(Rules.IsFlipWall('R') && Rules.IsFlipWall('r') && !Rules.IsFlipWall('L'));
        }

        [Test]
        public void SolverKeyIncludesFlipped()
        {
            var s = Run(Rules.NewState(Map(3, "##########", "#S......L#", "##########")), "RRRRRRRX");
            var unflipped = Rules.NewState(s.Level);
            Assert.IsTrue(s.Flipped);
            Assert.AreNotEqual(Rules.Key(s).Substring(Rules.Key(s).Length - 1), Rules.Key(unflipped).Substring(Rules.Key(unflipped).Length - 1));
        }

        [Test]
        public void DoorLettersAreRecognised()
        {
            foreach (char c in "ABCQ") Assert.IsTrue(Rules.IsDoor(c), c.ToString());
            foreach (char c in "abc#.SEo~x17=") Assert.IsFalse(Rules.IsDoor(c), c.ToString());
        }

        [Test]
        public void LuckyStepEventsCarryStepSeven()
        {
            foreach (var (_, _, r, where) in AllResults())
                foreach (var e in r.Events)
                    if (e.Type == GameEventType.Bridge || e.Type == GameEventType.Win || e.Type == GameEventType.Seal ||
                        e.Type == GameEventType.Vanish || e.Type == GameEventType.Choose)
                        Assert.AreEqual(Rules.Steps, e.StepIdx, $"{where}: {e.Type}");
        }

        // ---- mud ----------------------------------------------------------------------------------------

        [Test]
        public void MudCostsTwoStepsOnlyWhenSteppedIntoDirectly()
        {
            int moves = 0, mud = 0;
            foreach (var (prev, cmd, r, where) in AllResults())
            {
                var s = r.State;
                if ("UDLR".IndexOf(cmd) < 0 || s.Used <= prev.Used) continue; // not a move, rejected, or a new body
                moves++;
                int first = s.Trail[prev.Trail.Length].Cell;
                int cost = Rules.IsMud(Rules.TileAt(prev, first)) ? 2 : 1;
                if (cost == 2) mud++;
                Assert.AreEqual(prev.Used + cost, s.Used, $"{where}: step cost");
            }
            Assert.Greater(mud, 0, "golden traces step into mud");
            Assert.Greater(moves, mud);
        }

        [Test]
        public void MudBlocksWithOneStepLeft()
        {
            var s = Run(Rules.NewState(Map(2, "##########", "#S......m#", "##########")), "RRRRRR");
            Assert.AreEqual(6, s.Used);
            var r = Rules.Dispatch(s, Command.FromAct(Act.Right));
            Assert.AreSame(s, r.State);
            Assert.AreEqual(GameEventType.Bump, r.Events[0].Type);
            Assert.AreEqual(s.Pos + 1, r.Events[0].At);
        }

        [Test]
        public void SeventhStepCanEndOnMud()
        {
            var s = Run(Rules.NewState(Map(2, "#########", "#S.....m#", "#########")), "RRRRRR");
            Assert.AreEqual(Status.Choose, s.Status);
            Assert.AreEqual('m', Rules.TileAt(s, s.Pos));
            Assert.AreEqual(Rules.Steps, s.Used);
        }

        [Test]
        public void IceSlideStopsOnMudWithoutExtraStep()
        {
            var s = Run(Rules.NewState(Map(2, "#########", "#S~~m...#", "#########")), "R");
            Assert.AreEqual('m', Rules.TileAt(s, s.Pos));
            Assert.AreEqual(1, s.Used);
        }

        [Test]
        public void MudLetter()
        {
            Assert.IsTrue(Rules.IsMud('m'));
            foreach (char c in "#.SEo~x=MLRr") Assert.IsFalse(Rules.IsMud(c), c.ToString());
        }

        // ---- parsing ------------------------------------------------------------------------------------

        [Test]
        public void ParsePadsRaggedRowsWithWalls()
        {
            var l = Map(3, "#S.", "#", "#..E#");
            Assert.AreEqual(5, l.W);
            Assert.AreEqual(3, l.H);
            Assert.AreEqual('#', l.Tile(3));
            Assert.AreEqual('#', l.Tile(4));
            Assert.AreEqual('#', l.Tile(6));
            Assert.AreEqual('.', l.Tile(l.Start), "S reads as floor");
        }

        [Test]
        public void ParseNeedsAStart()
        {
            Assert.Throws<ArgumentException>(() => Map(3, "#..E#"));
        }

        [Test]
        public void ParseDefaultsBodiesToSeven()
        {
            Assert.AreEqual(Rules.Steps, Map(0, "#S.#").Bodies);
        }
    }
}
