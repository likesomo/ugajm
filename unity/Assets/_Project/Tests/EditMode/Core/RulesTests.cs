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
            string[] order = { "Bump", "Step", "Crumble", "Bridge", "Fall", "Win", "Seal", "Vanish", "Choose", "Stone", "Switch", "Respawn", "Lose", "Undo", "Restart", "Slide", "DoorOpen" };
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
                    if (t == GameEventType.DoorOpen)
                    {
                        doors++;
                        var before = types[k - 1];
                        Assert.IsTrue(before == GameEventType.Stone || before == GameEventType.DoorOpen, $"{where}: DoorOpen right after Stone");
                        Assert.IsTrue(Rules.DoorOpen(r.State, prev.Level.Tile(r.Events[k].At)), $"{where}: door is open");
                        Assert.IsFalse(Rules.DoorOpen(prev, prev.Level.Tile(r.Events[k].At)), $"{where}: door was shut");
                        Assert.AreEqual(0, r.Events[k].StepIdx, $"{where}: DoorOpen carries no step number");
                    }
                }
                Assert.AreEqual(r.Path.Length > 1, types.Contains(GameEventType.Slide), $"{where}: Slide iff the path is longer than one cell");
            }
            Assert.Greater(slides, 0, "golden traces never slide");
            Assert.Greater(doors, 0, "golden traces never open a door");
        }

        [Test]
        public void EveryDoorThatOpensGetsAnEvent()
        {
            foreach (var (prev, _, r, where) in AllResults())
            {
                if (r.Has(GameEventType.Undo) || r.Has(GameEventType.Restart)) continue;
                var opened = new List<int>();
                for (int i = 0; i < prev.Level.Count; i++)
                {
                    char t = prev.Level.Tile(i);
                    if (t >= 'A' && t <= 'C' && !Rules.DoorOpen(prev, t) && Rules.DoorOpen(r.State, t)) opened.Add(i);
                }
                var events = new List<int>();
                foreach (var e in r.Events) if (e.Type == GameEventType.DoorOpen) events.Add(e.At);
                CollectionAssert.AreEqual(opened, events, where);
            }
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
