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
    }
}
