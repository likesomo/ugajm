using NUnit.Framework;
using SevenSteps.Contracts;

namespace SevenSteps.Tests
{
    /// Settings "처음부터 다시" / "Reset progress" (user request 2026-11-14): progress, guides and story are erased, settings stay.
    public sealed class ResetProgressTests
    {
        static SaveSystem Played(MemoryStore store)
        {
            var save = new SaveSystem(store);
            save.Progress.stages.Add(new StageRecord { name = "a", levelKey = "k", cleared = true, bestBodies = 2, starts = 3, bestStars = 3 });
            save.Progress.lastStage = "a";
            save.SaveProgress();
            save.MarkTutorialSeen(TutorialIds.Prologue);
            save.MarkTutorialSeen(TutorialIds.Move);
            save.CountChoiceConfirm();
            save.Story.MarkShown("line.1");
            save.SaveStory();
            save.SetMasterVolume(0.3f);
            save.SetSfxVolume(0f);
            save.SetLanguage(Language.En);
            save.SetReduceMotion(true);
            save.Settings.playerId = "player-1";
            save.SaveSettings();
            store.Set(SaveSystem.ProgressBackupKey, "{\"version\":1}");
            return save;
        }

        [Test]
        public void ResetEmptiesProgressKeysAndKeepsSettings()
        {
            var store = new MemoryStore();
            var save = Played(store);
            var story = save.Story;
            Assert.IsTrue(save.TutorialSeen(TutorialIds.Prologue));
            Assert.IsTrue(store.Data.ContainsKey(SaveSystem.ProgressKey));

            save.ResetProgress();

            foreach (var key in new[] { SaveSystem.ProgressKey, SaveSystem.TutorialKey, SaveSystem.StoryKey, SaveSystem.ProgressBackupKey, "seven-steps-cleared" })
                Assert.AreEqual("", store.Get(key), key + " is empty after reset");
            Assert.IsTrue(store.Data.ContainsKey(SaveSystem.SettingsKey), "settings key kept");
            Assert.AreEqual(0, save.Progress.stages.Count);
            Assert.AreEqual("", save.Progress.lastStage);
            Assert.IsFalse(save.TutorialSeen(TutorialIds.Prologue));
            Assert.IsFalse(save.TutorialSeen(TutorialIds.Move));
            Assert.AreEqual(0, save.ChoiceConfirms);
            Assert.AreSame(story, save.Story, "reset in place: StoryBook keeps a live reference");
            Assert.AreEqual(0, save.Story.Count("line.1"));

            var reloaded = new SaveSystem(store); // the next launch
            Assert.AreEqual(0, reloaded.Progress.stages.Count);
            Assert.AreEqual("", reloaded.Progress.lastStage);
            Assert.IsFalse(reloaded.TutorialSeen(TutorialIds.Prologue));
            Assert.AreEqual(0, reloaded.ChoiceConfirms);
            Assert.AreEqual(0, reloaded.Story.Count("line.1"));
            Assert.AreEqual(0.3f, reloaded.Settings.masterVolume, 1e-6f);
            Assert.AreEqual(0f, reloaded.Settings.sfxVolume, 1e-6f);
            Assert.AreEqual(Language.En, reloaded.Language);
            Assert.IsTrue(reloaded.LanguageChosen, "no language picker again");
            Assert.IsTrue(reloaded.Settings.reduceMotion);
            Assert.AreEqual("player-1", reloaded.Settings.playerId);
        }

        [Test]
        public void ProgressAfterResetIsSavedAgain()
        {
            var store = new MemoryStore();
            var save = Played(store);
            save.ResetProgress();
            save.MarkTutorialSeen(TutorialIds.Prologue);
            Assert.IsTrue(new SaveSystem(store).TutorialSeen(TutorialIds.Prologue));
        }
    }
}
