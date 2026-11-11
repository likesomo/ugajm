using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;

namespace SevenSteps.Tests
{
    /// docs/architecture.md 4.4 (choice preview) and 11.3.2 (tutorial data, accessibility settings): minimal UI v3 contracts.
    public sealed class MinimalUiContractTests
    {
        const int W = 11;
        static Level Field() => Level.Parse("t", "", 3, new[]
        {
            "###########",
            "#S........#",
            "#.........#",
            "###########",
        });

        static State Choose()
        {
            var s = Rules.NewState(Field());
            for (int i = 0; i < 7; i++) s = Rules.Dispatch(s, Command.Move(Dir.Right)).State; // body 0 ends on (8,1)
            Assert.AreEqual(Status.Choose, s.Status);
            return s;
        }

        [Test]
        public void PreviewIsPureAndMatchesTheSession()
        {
            var s = Choose();
            string key = Rules.Key(s);
            Assert.IsTrue(ChoicePreview.TryOf(s, out var p));
            Assert.AreEqual(key, Rules.Key(s), "the Choose state is not changed");
            Assert.AreEqual(1 * W + 8, p.Cell);
            Assert.AreEqual(1 * W + 1, p.KeepStart, "KEEP: next body at the old start");
            Assert.AreEqual(1 * W + 8, p.SwitchStart, "SWITCH: next body here");
            Assert.AreEqual(1, p.NextBody);
            CollectionAssert.Contains(p.Keep.State.Statues, 1 * W + 8);
            CollectionAssert.IsEmpty(p.Switch.State.Statues);
            Assert.AreSame(s, p.Keep.State.Prev, "Undo after the confirm returns to the Choose state");

            var session = new GameSession();
            session.Load(Field());
            for (int i = 0; i < 7; i++) session.Send(Command.Move(Dir.Right));
            Assert.IsTrue(ChoicePreview.TryOf(session.State, out var q));
            session.Send(Command.Keep);
            Assert.AreEqual(Rules.Key(q.Keep.State), Rules.Key(session.State), "preview = what the session applies");
            session.Send(Command.Undo);
            Assert.AreEqual(Status.Choose, session.State.Status, "Z goes back to the choice");
            session.Send(Command.Switch);
            Assert.AreEqual(Rules.Key(q.Switch.State), Rules.Key(session.State));
        }

        [Test]
        public void NoPreviewOutsideChoose()
        {
            Assert.IsFalse(ChoicePreview.TryOf(Rules.NewState(Field()), out _));
            Assert.IsFalse(ChoicePreview.TryOf(null, out _));
        }

        [Test]
        public void TutorialSeenAndChoiceCountAreSavedAndCapped()
        {
            var store = new MemoryStore();
            var save = new SaveSystem(store);
            Assert.IsFalse(save.TutorialSeen(TutorialIds.Move));
            Assert.IsTrue(save.ExplainChoice);
            save.MarkTutorialSeen(TutorialIds.Move);
            save.MarkTutorialSeen(TutorialIds.Move);
            for (int i = 0; i < 5; i++) save.CountChoiceConfirm();
            Assert.AreEqual(TutorialData.ChoiceExplainCount, save.ChoiceConfirms);
            Assert.IsFalse(save.ExplainChoice);

            var again = new SaveSystem(store);
            Assert.IsTrue(again.TutorialSeen(TutorialIds.Move), "saved at once");
            Assert.IsFalse(again.TutorialSeen("move@2"), "a new guide version shows again");
            Assert.AreEqual(1, again.Tutorial.seen.Count);
            Assert.AreEqual(3, again.ChoiceConfirms);
            Assert.IsFalse(store.Get(SaveSystem.SettingsKey).Contains("move@1"), "own key, not settings");
            Assert.Throws<System.ArgumentException>(() => save.MarkTutorialSeen("Move"));
        }

        [Test]
        public void NewPlayerClearsTutorialButKeepsAccessibility()
        {
            var store = new MemoryStore();
            var save = new SaveSystem(store);
            save.MarkTutorialSeen(TutorialIds.Choose);
            save.CountChoiceConfirm();
            save.SetReduceMotion(true);
            save.StartNewPlayer();
            var again = new SaveSystem(store);
            Assert.IsFalse(again.TutorialSeen(TutorialIds.Choose));
            Assert.AreEqual(0, again.ChoiceConfirms);
            Assert.IsTrue(again.Settings.reduceMotion);
        }

        [Test]
        public void BrokenOrNewerTutorialSave()
        {
            var store = new MemoryStore();
            store.Set(SaveSystem.TutorialKey, "{not json");
            Assert.AreEqual(0, new SaveSystem(store).ChoiceConfirms);
            store.Set(SaveSystem.TutorialKey, "{\"version\":1,\"seen\":[\"move@1\"],\"choiceConfirms\":40}");
            Assert.AreEqual(3, new SaveSystem(store).ChoiceConfirms, "clamped");
            const string future = "{\"version\":9,\"seen\":[\"move@1\"],\"choiceConfirms\":1}";
            store.Set(SaveSystem.TutorialKey, future);
            var save = new SaveSystem(store);
            Assert.IsFalse(save.TutorialSeen(TutorialIds.Move));
            save.MarkTutorialSeen(TutorialIds.Move);
            Assert.AreEqual(future, store.Get(SaveSystem.TutorialKey), "a newer build's data is not overwritten");
        }

        [Test]
        public void AccessibilitySettingsDefaultOffAndSave()
        {
            var store = new MemoryStore();
            var save = new SaveSystem(store);
            int changed = 0;
            save.SettingsChanged += _ => changed++;
            Assert.IsFalse(save.Settings.showStepNumbers);
            Assert.IsFalse(save.Settings.reduceMotion);
            save.SetShowStepNumbers(true);
            save.SetReduceMotion(true);
            Assert.AreEqual(2, changed);
            var again = new SaveSystem(store);
            Assert.IsTrue(again.Settings.showStepNumbers);
            Assert.IsTrue(again.Settings.reduceMotion);

            store.Set(SaveSystem.SettingsKey, "{\"version\":1,\"language\":\"en\",\"languageChosen\":true}");
            Assert.IsFalse(new SaveSystem(store).Settings.showStepNumbers, "older saves read as off");
        }

        [Test]
        public void TutorialIdFormat()
        {
            Assert.IsTrue(TutorialIds.IsValid(TutorialIds.Move));
            Assert.IsTrue(TutorialIds.IsValid("keep-switch@12"));
            foreach (var bad in new[] { "", "move", "@1", "move@0", "move@01", "move@1@2", "Move@1", "move@x", "move@100" })
                Assert.IsFalse(TutorialIds.IsValid(bad), bad);
        }
    }
}
