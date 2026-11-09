using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// Chapter entry (docs/scene-flow.md 12.8 hook): chapter keys from chapters.json, the card shows on a chapter change.
    public sealed class ChapterIntroTests
    {
        const string Levels = @"{ ""steps"": 7, ""levels"": [
            { ""name"": ""a"", ""hint"": """", ""bodies"": 3, ""map"": [ ""#####"", ""#S.E#"", ""#####"" ] },
            { ""name"": ""b"", ""hint"": """", ""bodies"": 3, ""chapter"": ""ruins"", ""map"": [ ""#####"", ""#S.E#"", ""#####"" ] },
            { ""name"": ""c"", ""hint"": """", ""bodies"": 3, ""chapter"": ""moss"", ""map"": [ ""#####"", ""#S.E#"", ""#####"" ] } ] }";
        const string Chapters = @"{ ""chapters"": [
            { ""id"": ""ruins"", ""planet"": ""폐허 행성"", ""intro"": ""첫 별"", ""text"": [ { ""lang"": ""en"", ""planet"": ""Ruins Planet"", ""intro"": ""First star"" } ] },
            { ""id"": ""moss"", ""planet"": ""이끼 행성"", ""intro"": """", ""text"": [ { ""lang"": ""en"", ""planet"": ""Moss Planet"", ""intro"": ""Go"" } ] } ] }";

        GameAssets assets;

        static void ResetCatalog() =>
            typeof(LevelCatalog).GetMethod("ResetForPlay", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        [SetUp]
        public void SetUp()
        {
            ResetCatalog();
            assets = ScriptableObject.CreateInstance<GameAssets>();
            assets.levelsJson = new TextAsset(Levels);
            assets.chaptersJson = new TextAsset(Chapters);
        }

        [TearDown]
        public void TearDown()
        {
            ResetCatalog();
            UnityEngine.Object.DestroyImmediate(assets.levelsJson);
            UnityEngine.Object.DestroyImmediate(assets.chaptersJson);
            UnityEngine.Object.DestroyImmediate(assets);
        }

        [Test]
        public void EmptyChapterIsTheFirstChapter()
        {
            LevelCatalog.Load(assets);
            Assert.AreEqual("", LevelCatalog.ChapterIdOf(0), "raw levels.json value");
            Assert.AreEqual("ruins", LevelCatalog.ChapterKeyOf(0), "normalized: stage a and b are the same chapter");
            Assert.AreEqual("ruins", LevelCatalog.ChapterKeyOf(1));
            Assert.AreEqual("moss", LevelCatalog.ChapterKeyOf(2));
            var card = LevelCatalog.ChapterCard("moss", Language.Ko);
            Assert.AreEqual(2, card.Number);
            Assert.AreEqual("이끼 행성", card.Planet);
            CollectionAssert.AreEqual(new[] { "폐허 행성", "이끼 행성" }, card.Planets, "every planet, chapter order");
            Assert.Throws<ArgumentException>(() => new ChapterCardModel("x", 2, "p", "", new[] { "p" }), "number outside the list");
            var en = LevelCatalog.ChapterCard("moss", Language.En);
            Assert.AreEqual("Moss Planet", en.Planet, "the card is in the UI language");
            Assert.AreEqual("Go", en.Intro);
            CollectionAssert.AreEqual(new[] { "Ruins Planet", "Moss Planet" }, en.Planets, "the planet row in the UI language too");
        }

        [Test]
        public void UnknownChapterInLevelsFailsTheLoad()
        {
            UnityEngine.Object.DestroyImmediate(assets.chaptersJson);
            assets.chaptersJson = new TextAsset(@"{ ""chapters"": [ { ""id"": ""ruins"", ""planet"": ""폐허"" } ] }");
            Assert.Throws<ArgumentException>(() => LevelCatalog.Load(assets), "stage c says moss, chapters.json has no moss");
        }

        [Test]
        public void WithoutChaptersJsonThereIsNoCard()
        {
            UnityEngine.Object.DestroyImmediate(assets.chaptersJson);
            assets.chaptersJson = null;
            LevelCatalog.Load(assets);
            Assert.IsNull(LevelCatalog.ChapterOf(0));
            Assert.AreEqual("ruins", LevelCatalog.ChapterKeyOf(1), "raw id when there is no file");
            Assert.IsNull(LevelCatalog.ChapterCard("ruins", Language.Ko));
        }

        // ---------- controller ----------

        sealed class Scenes : ISceneLoader
        {
            public string Current => AppScenes.Game;
            public void Load(string sceneName) { }
            public event Action<string> Loaded { add { } remove { } }
        }

        sealed class Board : IStageBoard
        {
            public State State { get; private set; }
            public void LoadStage(int stage, string via) => State = Rules.NewState(Level.Parse("s", "", 3, new[] { "#####", "#S.E#", "#####" }));
            public AttemptCounts Attempt => default;
            public event Action<State> Won { add { } remove { } }
            public event Action<State> WinPresented { add { } remove { } }
            public event Action<State> Lost { add { } remove { } }
            public event Action Recovered { add { } remove { } }
        }

        sealed class Panel : IResultPanel { public void Show(ResultModel m) { } public void Hide() { } }

        sealed class Card : IChapterCard
        {
            public readonly List<string> Bound = new List<string>();
            public bool Visible;
            public event Action CloseRequested;
            public void Bind(ChapterCardModel model) => Bound.Add(model.ChapterId);
            public void Show() => Visible = true;
            public void Hide() => Visible = false;
            public void TimerEnds() => CloseRequested?.Invoke();
        }

        [Test]
        public void CardShowsOnEachChapterChangeAsAnOverlay()
        {
            string[] keys = { "ruins", "ruins", "moss" };
            var flow = new AppFlow(new Scenes(), 3);
            var game = new GameFlowController(flow, EntryTracker.ForLaunch(false), new SaveSystem(new MemoryStore()), new Board(), new Panel(),
                _ => 0, stage => keys[stage]);
            var overlays = new OverlayStack();
            var card = new Card();
            using var intro = new ChapterIntroController(game, overlays, card,
                id => new ChapterCardModel(id, id == "ruins" ? 1 : 2, id + " planet", ""));

            game.Start(0);                 // first entry: ruins card
            Assert.IsTrue(card.Visible);
            Assert.AreEqual(1, overlays.Count, "board input waits while the card is up");
            card.TimerEnds();
            Assert.IsFalse(card.Visible);

            game.SelectStage(1);           // same chapter: no card
            game.SelectStage(2);           // moss
            CollectionAssert.AreEqual(new[] { "ruins", "moss" }, card.Bound);
            game.Dispose();
        }

        [Test]
        public void NoCardOrNoDataMeansNothing()
        {
            var flow = new AppFlow(new Scenes(), 3);
            var game = new GameFlowController(flow, EntryTracker.ForLaunch(false), new SaveSystem(new MemoryStore()), new Board(), new Panel(), _ => 0, _ => "");
            var overlays = new OverlayStack();
            using (new ChapterIntroController(game, overlays, null, id => new ChapterCardModel("x", 1, "p", "")))
                game.Start(0);
            Assert.AreEqual(0, overlays.Count);
            var card = new Card();
            using (new ChapterIntroController(game, overlays, card, _ => null))
                game.SelectStage(1);
            Assert.AreEqual(0, overlays.Count);
            Assert.Throws<ArgumentException>(() => new ChapterCardModel("x", 1, "", ""), "a card needs a planet name");
            game.Dispose();
        }
    }
}
