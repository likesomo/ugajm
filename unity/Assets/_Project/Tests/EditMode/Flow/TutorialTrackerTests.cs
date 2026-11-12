using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Flow;

namespace SevenSteps.Tests
{
    /// One-time guides (design/art/ui-minimal.md "등장·해제·한 번 안내") on a headless GameSession.
    public sealed class TutorialTrackerTests
    {
        // Exit past the seventh step: seven moves right ask for a choice (3 bodies).
        static Level Long() => Level.Parse("long", "", 3, new[] { "#############", "#S.........E#", "#############" });

        static (GameSession, TutorialTracker, MemoryTutorialStore) Make(int[] changes = null)
        {
            var store = new MemoryTutorialStore();
            var t = new TutorialTracker(store);
            var s = new GameSession();
            s.Updated += t.Apply;
            if (changes != null) t.Changed += () => changes[0]++;
            s.Load(Long());
            return (s, t, store);
        }

        static void Send(GameSession s, string moves)
        {
            foreach (char c in moves)
                s.Send(c switch
                {
                    'R' => Command.Move(Dir.Right), 'L' => Command.Move(Dir.Left), 'U' => Command.Move(Dir.Up), 'D' => Command.Move(Dir.Down),
                    'K' => Command.Keep, 'X' => Command.Switch, 'Z' => Command.Undo, '!' => Command.Restart,
                    _ => throw new System.ArgumentException(c.ToString()),
                });
        }

        [Test]
        public void MoveGuideUntilTheFirstAppliedMove()
        {
            var (s, t, store) = Make();
            Assert.IsTrue(t.ShowMoveGuide);
            Send(s, "U"); // bump: not used up
            Assert.IsTrue(t.ShowMoveGuide);
            Assert.IsFalse(store.Seen(TutorialTracker.MoveId));
            Send(s, "R");
            Assert.IsFalse(t.ShowMoveGuide);
            Assert.IsTrue(store.Seen(TutorialTracker.MoveId));
        }

        [Test]
        public void UndoRestartAndReloadDoNotBringItBack()
        {
            var (s, t, store) = Make();
            Send(s, "RZ");
            Assert.IsFalse(t.ShowMoveGuide);
            Send(s, "RR!");
            Assert.IsFalse(t.ShowMoveGuide);
            s.Load(Long());
            Assert.IsFalse(t.ShowMoveGuide);
            // A new tracker on the same store (next scene, next run) remembers; a fresh profile shows it again.
            Assert.IsFalse(new TutorialTracker(store).ShowMoveGuide);
            Assert.IsTrue(new TutorialTracker(new MemoryTutorialStore()).ShowMoveGuide);
        }

        [Test]
        public void ChoiceGuideWhenFirstNeededUntilConfirmed()
        {
            var (s, t, store) = Make();
            Send(s, "RRRRRR");
            Assert.IsFalse(t.ShowChoiceGuide);
            Send(s, "R");
            Assert.AreEqual(Status.Choose, s.State.Status);
            Assert.IsTrue(t.ShowChoiceGuide);
            Send(s, "R"); // a move while choosing is refused
            Assert.IsTrue(t.ShowChoiceGuide);
            Send(s, "Z"); // undo back before the choice: hidden, nothing counted
            Assert.IsFalse(t.ShowChoiceGuide);
            Assert.AreEqual(0, store.ChoiceConfirms);
            Send(s, "RK");
            Assert.IsFalse(t.ShowChoiceGuide);
            Assert.IsTrue(store.Seen(TutorialTracker.ChoiceId));
            Assert.AreEqual(1, store.ChoiceConfirms);
            Send(s, "Z"); // undo of the KEEP: seen stays, the count stays
            Assert.AreEqual(Status.Choose, s.State.Status);
            Assert.IsFalse(t.ShowChoiceGuide);
            Assert.AreEqual(1, store.ChoiceConfirms);
        }

        [Test]
        public void ChoiceResultLinesForTheFirstThreeConfirms()
        {
            var (s, t, store) = Make();
            for (int k = 0; k < 4; k++)
            {
                Assert.AreEqual(k < TutorialTracker.ChoiceExplanations, t.ExplainChoices, $"before confirm {k + 1}");
                Send(s, "RRRRRRRX!");
            }
            Assert.AreEqual(TutorialTracker.ChoiceExplanations, store.ChoiceConfirms); // capped
            Send(s, "K"); // refused K (not choosing): no count
            Assert.AreEqual(TutorialTracker.ChoiceExplanations, store.ChoiceConfirms);
        }

        [Test]
        public void MenuControlsReplayDoesNotSave()
        {
            var changes = new int[1];
            var (s, t, store) = Make(changes);
            Send(s, "R");
            Assert.IsFalse(t.ShowMoveGuide);
            int before = changes[0];
            t.PlayControls();
            Assert.IsTrue(t.ShowMoveGuide && t.ManualControls);
            Assert.AreEqual(before + 1, changes[0]);
            Send(s, "U"); // bump into the wall: still shown
            Assert.IsTrue(t.ShowMoveGuide);
            Send(s, "R");
            Assert.IsFalse(t.ShowMoveGuide || t.ManualControls);
            t.PlayControls();
            t.StopControls();
            Assert.IsFalse(t.ShowMoveGuide);
            Assert.AreEqual(0, store.ChoiceConfirms);
        }

        [Test]
        public void ReplaySnapsDoNotCount()
        {
            var (s, t, store) = Make();
            s.Replay("RRRRRRRK");
            Assert.IsTrue(t.ShowMoveGuide);
            Assert.IsFalse(store.Seen(TutorialTracker.MoveId) || store.Seen(TutorialTracker.ChoiceId));
            Assert.AreEqual(0, store.ChoiceConfirms);
        }

        // ---------- first slide (user decision 2026-11-12-b (3)) ----------

        // S, two ice cells, floor: R from S slides to the floor cell past the ice (one step).
        static Level Ice() => Level.Parse("ice", "", 3, new[] { "#########", "#S~~...E#", "#########" });
        // Six floor steps, then ice: the 7th move slides and asks for the choice, so its line stays and the guide waits.
        static Level IceOnSeventh() => Level.Parse("ice-7th", "", 3, new[] { "##############", "#S......~~..E#", "##############" });

        static (GameSession, TutorialTracker, MemoryTutorialStore, int[]) MakeOn(Level level, MemoryTutorialStore store = null)
        {
            store ??= new MemoryTutorialStore();
            var t = new TutorialTracker(store);
            var s = new GameSession();
            s.Updated += t.Apply;
            var raised = new int[1];
            t.FirstSlide += () => raised[0]++;
            s.Load(level);
            return (s, t, store, raised);
        }

        [Test]
        public void FirstSlideIsRaisedOnceEverAndSaved()
        {
            var (s, _, store, raised) = MakeOn(Ice());
            Send(s, "R");
            Assert.AreEqual(4, s.State.Pos % 9, "slid past both ice cells in one move");
            Assert.AreEqual(1, raised[0]);
            Assert.IsTrue(store.Seen(TutorialTracker.SlideId), "saved before it is raised");
            Send(s, "L"); // slides back over the ice
            Send(s, "!R");
            Assert.AreEqual(1, raised[0], "once ever: later slides, restarts and undos do not raise it again");
            var (s2, _, _, raised2) = MakeOn(Ice(), store); // next scene, same profile
            Send(s2, "R");
            Assert.AreEqual(0, raised2[0]);
        }

        [Test]
        public void ASlideOnTheSeventhStepKeepsItsLineAndTheGuideWaits()
        {
            var (s, _, store, raised) = MakeOn(IceOnSeventh());
            Send(s, "RRRRRR");
            Assert.AreEqual(0, raised[0]);
            Send(s, "R");
            Assert.AreEqual(10, s.State.Pos % 14, "slid over the ice");
            Assert.AreEqual(Status.Choose, s.State.Status);
            Assert.AreEqual(0, raised[0]);
            Assert.IsFalse(store.Seen(TutorialTracker.SlideId));
        }

        [Test]
        public void AMoveWithoutIceIsNotASlideAndReplaysDoNotCount()
        {
            var (s, _, store, raised) = MakeOn(Long());
            Send(s, "RR");
            Assert.AreEqual(0, raised[0]);
            var (s2, _, store2, raised2) = MakeOn(Ice());
            s2.Replay("R");
            Assert.AreEqual(0, raised2[0]);
            Assert.IsFalse(store.Seen(TutorialTracker.SlideId) || store2.Seen(TutorialTracker.SlideId));
            Assert.IsTrue(new MemoryTutorialStore(allSeen: true).Seen(TutorialTracker.SlideId), "dev runs start with it seen");
        }
    }
}
