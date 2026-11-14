using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// User request 2026-11-14: locked planets (the one before is not completed) and the planet interludes in cutscenes.json.
    public sealed class PlanetLockInterludeTests
    {
        static readonly string[] Keys = { "moss", "hollow", "carved" };

        static StageSelectModel Model(params bool[][] cleared) =>
            new StageSelectModel(Keys, cleared, -1, -1, new List<(int, int)> { (0, 1), (1, 2) });

        [Test]
        public void OnlyTheFirstPlanetIsOpenOnAFreshSave()
        {
            var m = Model(new[] { false, false }, new[] { false, false }, new[] { false, false });
            Assert.IsFalse(m.Locked(0), "moss is always open");
            Assert.IsTrue(m.Locked(1));
            Assert.IsTrue(m.Locked(2));
        }

        [Test]
        public void APlanetOpensWhenTheOneBeforeHasItsFinaleCleared()
        {
            var partly = Model(new[] { true, false }, new[] { false, false }, new[] { false, false });
            Assert.IsTrue(partly.Locked(1), "moss started, finale not cleared");
            var done = Model(new[] { false, true }, new[] { false, false }, new[] { false, false });
            Assert.IsFalse(done.Locked(1), "moss finale cleared (FinaleCleared, the trail rule)");
            Assert.IsTrue(done.Locked(2));
            Assert.AreEqual(done.TrailLit(0), !done.Locked(1), "the trail to a planet is lit exactly when it is open");
            Assert.AreEqual(done.TrailLit(1), !done.Locked(2));
        }

        static CutsceneCatalog Shipped() =>
            CutsceneCatalog.FromJson(File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Data", "cutscenes.json")));

        [Test]
        public void EveryChapterHasAnInterludeAfterItsFinale()
        {
            var c = Shipped();
            foreach (var chapter in new[] { "moss", "hollow", "carved", "frost", "joinery", "crumble", "altar" })
            {
                var s = c.Interlude(chapter);
                Assert.IsNotNull(s, chapter);
                Assert.AreEqual(CutsceneCatalog.WhenPlanetComplete, s.when);
                Assert.That(s.pages.Length, Is.InRange(2, 3), chapter);
            }
            Assert.IsNull(c.Interlude("nowhere"));
            Assert.AreEqual("prologue", c.SceneFor(CutsceneCatalog.WhenNewGame).id, "the prologue still plays on a new game");
        }

        [Test]
        public void InterludeSeenIsStoryMemoryAndAResetClearsIt()
        {
            var store = new MemoryStore();
            var save = new SaveSystem(store);
            string id = CutsceneCatalog.InterludeSeenId("moss");
            save.Story.MarkShown(id);
            save.SaveStory();
            Assert.AreEqual(1, new SaveSystem(store).Story.Count(id), "saved");
            save.ResetProgress();
            Assert.AreEqual(0, save.Story.Count(id));
            Assert.AreEqual(0, new SaveSystem(store).Story.Count(id), "reset saved");
        }

        [Test]
        public void ACatalogRejectsAMisnamedInterlude()
        {
            string json = "{\"version\":3,\"languages\":[\"ko\",\"en\"],\"scenes\":[{\"id\":\"moss\",\"when\":\"planetComplete\",\"pages\":[{\"id\":\"1\",\"image\":\"cutscene.moss.1\",\"lines\":[{\"line\":\"가\",\"text\":[{\"lang\":\"en\",\"line\":\"a\"}]}]}]}]}";
            Assert.Throws<System.FormatException>(() => CutsceneCatalog.FromJson(json));
        }
    }
}
