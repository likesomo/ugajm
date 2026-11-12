using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// <summary>Storybook cutscenes (design/story/cutscenes.md): the data contract and the player. Owner: gameplay-ui.</summary>
    public sealed class CutsceneControllerTests
    {
        sealed class View : ICutsceneView
        {
            public readonly List<CutsceneFrame> Frames = new List<CutsceneFrame>();
            public bool Shown;
            public event Action NextRequested, SkipRequested;
            public event Action CloseRequested { add { } remove { } }
            public void ShowFrame(CutsceneFrame f) => Frames.Add(f);
            public void Show() => Shown = true;
            public void Hide() => Shown = false;
            public void Next() => NextRequested?.Invoke();
            public void Skip() => SkipRequested?.Invoke();
        }

        static CutsceneCatalog Data() =>
            CutsceneCatalog.FromJson(AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset").cutscenesJson.text);

        // Two pages: two lines, then one.
        const string Small = @"{ ""version"": 1, ""languages"": [""ko"", ""en""], ""scenes"": [
            { ""id"": ""prologue"", ""when"": ""newGame"", ""replay"": ""title"", ""pages"": [
              { ""id"": ""1"", ""image"": ""cutscene.prologue.1"", ""lines"": [
                { ""line"": ""가"", ""text"": [{ ""lang"": ""en"", ""line"": ""A"" }] },
                { ""line"": ""나"", ""text"": [{ ""lang"": ""en"", ""line"": ""B"" }] } ] },
              { ""id"": ""2"", ""image"": ""cutscene.prologue.2"", ""lines"": [
                { ""line"": ""다"", ""text"": [{ ""lang"": ""en"", ""line"": ""C"" }] } ] } ] } ] }";

        Language lang = Language.Ko;
        (CutsceneController, View, OverlayStack, CutsceneSceneDto) Make()
        {
            var view = new View();
            var stack = new OverlayStack();
            return (new CutsceneController(stack, view, () => lang), view, stack, CutsceneCatalog.FromJson(Small).Scene("prologue"));
        }

        [Test]
        public void TheShippedDataLoadsWithAPrologueAndAnEpilogue()
        {
            var c = Data();
            var prologue = c.SceneFor(CutsceneCatalog.WhenNewGame);
            Assert.AreEqual(CutsceneCatalog.Prologue, prologue.id);
            Assert.AreEqual(7, prologue.pages.Length);
            Assert.AreSame(prologue, c.TitleReplay(), "the title's story entry replays the prologue");
            Assert.AreEqual(CutsceneCatalog.Epilogue, c.SceneFor(CutsceneCatalog.WhenAfterEnding).id);
            Assert.AreEqual(8, c.ImageKeys.Count);
            Assert.AreEqual("cutscene.prologue.1", c.ImageKeys[0]);
            foreach (var s in c.Scenes)
                foreach (var p in s.pages)
                    foreach (var l in Languages.All)
                        Assert.That(CutsceneCatalog.Lines(p, l), Has.All.Not.Empty, $"{p.image} {l}");
            var set = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset").cutsceneImages;
            Assert.IsNotNull(set, "GameAssets.cutsceneImages");
            Assert.That(set.entries.Select(e => e.key), Is.EquivalentTo(c.ImageKeys), "one entry per page");
            // Codex pictures (shared-assets) wired by unity.sh cutscene-art, imported by the CutsceneArtImport rule.
            Assert.IsTrue(set.required, "every page has its picture: a missing one is a scene-validate error");
            foreach (var e in set.entries)
            {
                Assert.IsNotNull(e.sprite, e.key);
                var path = AssetDatabase.GetAssetPath(e.sprite);
                Assert.AreEqual($"Assets/Shared/Art/Story/Cutscenes/{e.key}.png", path, e.key);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(TextureImporterType.Sprite, ti.textureType, e.key);
                Assert.IsFalse(ti.mipmapEnabled, e.key);
                var web = ti.GetPlatformTextureSettings("WebGL");
                Assert.IsTrue(web.overridden && web.crunchedCompression, e.key + ": WebGL crunched");
                Assert.AreEqual(1024, web.maxTextureSize, e.key);
            }
        }

        [Test]
        public void TheLoadCheckNamesWhatIsWrong()
        {
            void Bad(string json, string part) =>
                Assert.That(Assert.Throws<FormatException>(() => CutsceneCatalog.FromJson(json)).Message, Does.Contain(part));
            Bad("", "empty");
            Bad(Small.Replace(@"""version"": 1", @"""version"": 2"), "version 2");
            Bad(Small.Replace(@"[""ko"", ""en""]", @"[""ko""]"), "\"en\"");
            Bad(Small.Replace("newGame", "later"), "when \"later\"");
            Bad(Small.Replace("cutscene.prologue.2", "cutscene.prologue.9"), "expected \"cutscene.prologue.2\"");
            Bad(Small.Replace(@"{ ""lang"": ""en"", ""line"": ""C"" }", ""), "line 1: no \"en\"");
            Bad(Small.Replace(@"""lines"": [
                { ""line"": ""다"", ""text"": [{ ""lang"": ""en"", ""line"": ""C"" }] } ]", @"""lines"": []"), "0 lines");
        }

        [Test]
        public void LinesComeOneByOneThenThePageTurnsThenItEnds()
        {
            var (c, view, stack, scene) = Make();
            var ends = new List<bool>();
            c.Begin(scene, ends.Add);
            Assert.IsTrue(c.IsOpen && view.Shown);
            Assert.AreSame(view, stack.Top);
            Assert.AreEqual((0, 1), (view.Frames[0].Page, view.Frames[0].Shown));
            Assert.AreEqual(new[] { "가", "나" }, view.Frames[0].Lines);
            Assert.AreEqual("cutscene.prologue.1", view.Frames[0].ImageKey);
            view.Next();
            Assert.AreEqual((0, 2), (c.Page, c.Shown));
            view.Next();
            Assert.AreEqual((1, 1), (c.Page, c.Shown));
            Assert.AreEqual(2, view.Frames.Last().PageCount);
            Assert.IsEmpty(ends);
            view.Next();
            Assert.AreEqual(new[] { false }, ends, "read through: once, not skipped");
            Assert.IsFalse(c.IsOpen || view.Shown);
            Assert.AreEqual(0, stack.Count);
            view.Next();
            Assert.AreEqual(1, ends.Count, "nothing after the end");
        }

        [Test]
        public void SkipAndEscEndAtOnce()
        {
            var (c, view, stack, scene) = Make();
            var ends = new List<bool>();
            c.Begin(scene, ends.Add);
            view.Skip();
            Assert.AreEqual(new[] { true }, ends);
            c.Begin(scene, ends.Add);
            stack.Pop(); // Esc: the BackNavigator pops the top overlay
            Assert.AreEqual(new[] { true, true }, ends);
            Assert.IsFalse(c.IsOpen);
        }

        [Test]
        public void TheLanguageRedrawsTheSameFrame()
        {
            var (c, view, _, scene) = Make();
            c.Begin(scene, _ => { });
            view.Next();
            lang = Language.En;
            c.Redraw();
            var f = view.Frames.Last();
            Assert.AreEqual((0, 2), (f.Page, f.Shown));
            Assert.AreEqual(new[] { "A", "B" }, f.Lines);
        }

        [Test]
        public void AssertsItsInputs()
        {
            var (c, view, stack, scene) = Make();
            Assert.Throws<ArgumentNullException>(() => new CutsceneController(null, view, () => lang));
            Assert.Throws<ArgumentNullException>(() => c.Begin(null, _ => { }));
            Assert.Throws<ArgumentNullException>(() => c.Begin(scene, null));
            c.Begin(scene, _ => { });
            Assert.Throws<InvalidOperationException>(() => c.Begin(scene, _ => { }), "one at a time");
            Assert.Throws<ArgumentOutOfRangeException>(() => new CutsceneFrame("p", 0, 1, "", new[] { "a" }, 2));
        }

        [Test]
        public void TheProloguePlaysOnlyOnAFreshNewGame()
        {
            var p = Data().SceneFor(CutsceneCatalog.WhenNewGame);
            Assert.IsTrue(CutsceneController.PlaysOnNewGame(p, canContinue: false, seen: false, devRun: false));
            Assert.IsFalse(CutsceneController.PlaysOnNewGame(null, false, false, false), "no data");
            Assert.IsFalse(CutsceneController.PlaysOnNewGame(p, true, false, false), "a stage was played");
            Assert.IsFalse(CutsceneController.PlaysOnNewGame(p, false, true, false), "seen (until -newplayer)");
            Assert.IsFalse(CutsceneController.PlaysOnNewGame(p, false, false, true), "title / flow checks");
            Assert.IsTrue(TutorialIds.IsValid(TutorialIds.Prologue));
        }
    }
}
