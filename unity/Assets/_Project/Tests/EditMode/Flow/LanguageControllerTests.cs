using System;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;

namespace SevenSteps.Tests
{
    /// Language picker behavior (docs/architecture.md 13.3, architect N1) and the -loadDelay option (S7b).
    public sealed class LanguageControllerTests
    {
        sealed class Panel : ILanguagePanel
        {
            public bool Shown;
            public event Action<Language> LanguagePicked;
            public event Action ContinueRequested;
            public event Action CloseRequested;
            public void Show() => Shown = true;
            public void Hide() => Shown = false;
            public void Pick(Language l) => LanguagePicked?.Invoke(l);
            public void Continue() => ContinueRequested?.Invoke();
        }

        static Localizer Loc(Language current) => new Localizer(new[]
        {
            new StringTable(Language.Ko, new StringTable.Entry[0]),
            new StringTable(Language.En, new StringTable.Entry[0]),
        }, current);

        [Test]
        public void ContinueSavesTheShownLanguageEvenWhenUnchanged()
        {
            var save = new SaveSystem(new MemoryStore());
            var loc = Loc(Language.Ko);
            var panel = new Panel();
            var overlays = new OverlayStack();
            using var c = new LanguageController(panel, overlays, loc, save, null);
            Assert.IsFalse(save.LanguageChosen);
            c.Open();
            Assert.IsTrue(panel.Shown);
            panel.Continue();
            Assert.IsTrue(save.LanguageChosen, "the first-run picker must not come back");
            Assert.AreEqual(Language.Ko, save.Language);
            Assert.AreEqual(0, overlays.Count);
        }

        [Test]
        public void PickPreviewsAndEscReturnsToTheRunLanguage()
        {
            var save = new SaveSystem(new MemoryStore());
            save.SetLanguage(Language.Ko);
            var loc = Loc(Language.Ko);
            var panel = new Panel();
            var overlays = new OverlayStack();
            using var c = new LanguageController(panel, overlays, loc, save, null);
            c.Open();
            panel.Pick(Language.En);
            Assert.AreEqual(Language.En, loc.Current, "preview at once");
            Assert.AreEqual(Language.Ko, save.Language, "not saved yet");
            overlays.Pop(); // Esc
            Assert.AreEqual(Language.Ko, loc.Current, "back to the saved language");
        }

        [Test]
        public void EscReturnsToTheLaunchLanguageWhenGiven()
        {
            var save = new SaveSystem(new MemoryStore());
            save.SetLanguage(Language.Ko);
            var loc = Loc(Language.En);
            var panel = new Panel();
            var overlays = new OverlayStack();
            using var c = new LanguageController(panel, overlays, loc, save, Language.En);
            c.Open();
            panel.Pick(Language.Ko);
            overlays.Pop();
            Assert.AreEqual(Language.En, loc.Current, "-lang en wins over the saved ko");
        }

        [Test]
        public void LoadDelayParses()
        {
            Assert.AreEqual(1.5f, LaunchOptions.Parse(new[] { "x.exe", "-loadDelay", "1.5" }, "").LoadDelay);
            Assert.AreEqual(2f, LaunchOptions.Parse(null, "http://h/?loadDelay=2").LoadDelay);
            var bad = LaunchOptions.Parse(new[] { "x.exe", "-loadDelay", "99" }, "");
            Assert.AreEqual(0f, bad.LoadDelay);
            Assert.AreEqual(1, bad.Warnings.Count);
        }
    }
}
