using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;

namespace SevenSteps.Tests
{
    /// Title scene flow without Unity (docs/scene-flow.md S3): Boot -> Title -> StageSelect -> Game, back, last stage.
    public sealed class TitleFlowControllerTests
    {
        sealed class Scenes : ISceneLoader
        {
            public string Current { get; set; } = AppScenes.Title;
            public string Pending;
            public event Action<string> Loaded;
            public void Load(string sceneName) { Pending = sceneName; }
            public void Finish() { Current = Pending; Pending = null; Loaded?.Invoke(Current); }
        }

        sealed class Panels : ITitlePanels
        {
            public readonly List<AppState> Shown = new List<AppState>();
            public void Show(AppState state) => Shown.Add(state);
        }

        Scenes scenes;
        AppFlow flow;
        Panels panels;
        SaveSystem save;
        TitleFlowController ctl;

        [SetUp]
        public void SetUp()
        {
            scenes = new Scenes();
            flow = new AppFlow(scenes, 17);
            panels = new Panels();
            save = new SaveSystem(new MemoryStore());
            ctl = new TitleFlowController(flow, save, panels);
        }

        [TearDown] public void TearDown() => ctl.Dispose();

        [Test]
        public void StartFromBootShowsTitleThenStageSelectThenBack()
        {
            ctl.Start();
            Assert.AreEqual(AppState.Title, flow.State);
            ctl.Back();                               // Title has no back target
            Assert.AreEqual(AppState.Title, flow.State);
            ctl.OpenStageSelect();
            ctl.Back();
            CollectionAssert.AreEqual(new[] { AppState.Title, AppState.StageSelect, AppState.Title }, panels.Shown);
            Assert.IsNull(scenes.Pending, "Title and StageSelect share the scene");
        }

        [Test]
        public void PlayingAStageLoadsGameAndIgnoresKeysWhileLoading()
        {
            ctl.Start();
            ctl.OpenStageSelect();
            ctl.PlayStage(17);                        // out of range: ignored
            Assert.AreEqual(AppState.StageSelect, flow.State);
            ctl.PlayStage(4);
            Assert.AreEqual(AppState.Game, flow.State);
            Assert.AreEqual(4, flow.Stage);
            Assert.AreEqual(AppScenes.Game, scenes.Pending);
            ctl.Back();                               // same frame Esc: ignored while loading
            ctl.PlayStage(5);
            Assert.AreEqual(4, flow.Stage);
            Assert.AreEqual(AppState.Game, panels.Shown[panels.Shown.Count - 1], "panels hide while the scene changes");
        }

        [Test]
        public void EnterPlaysTheLastStageClampedFromSaveData()
        {
            save.Progress.lastStage = 99;             // save from a build with more stages
            ctl.Start();
            ctl.OpenStageSelect();
            ctl.PlayLastStage();
            Assert.AreEqual(16, flow.Stage);
        }

        [Test]
        public void ArrivingOnStageSelectShowsIt()
        {
            scenes.Current = AppScenes.Game;          // Game -> StageSelect loads the Title scene
            flow.GoToGame(0);
            flow.Go(AppState.StageSelect);
            scenes.Finish();
            panels.Shown.Clear();
            ctl.Start();
            CollectionAssert.AreEqual(new[] { AppState.StageSelect }, panels.Shown);
        }
    }
}
