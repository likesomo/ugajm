using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests.PlayMode
{
    /// <summary>
    /// The Unity scene structure in Play Mode (docs/scene-flow.md 9): Boot → Title → Game → Title with LoadSceneAsync Single,
    /// one scene loaded at a time, the DontDestroyOnLoad roots (at most one of each name: SceneFader after the first load,
    /// TelemetryRunner only with telemetry on, so none here), a -level start, and Title / Game played directly.
    /// Every test runs with injected launch options (Bootstrap.UseLaunchOptionsForNextRun): telemetry off, Korean, and the
    /// save key suffix "playtest", which is wiped before and after (the developer's own progress is not touched).
    /// One Play session runs all tests, so each test ends by resetting the bootstrap and destroying the DDOL roots, as entering
    /// Play Mode would (domain reload is off: docs/scene-flow.md 5).
    /// </summary>
    public sealed class SceneFlowTests
    {
        [SetUp] public void SetUp() => PlayEnv.Reset();
        [TearDown] public void TearDown() => PlayEnv.Reset();

        // ---------- Scene flow ----------

        [UnityTest]
        public IEnumerator BootLoadsTitleAsTheOnlyScene()
        {
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch());
            SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
            yield return WaitForState(AppState.Title, AppScenes.Title);

            Assert.AreEqual(1, SceneManager.sceneCount, "Single loads: one scene at a time");
            Assert.AreEqual(AppScenes.Title, SceneManager.GetActiveScene().name);
            var roots = DdolRootNames();
            Assert.AreEqual(1, roots.Count(n => n == "SceneFader"), "after the first load: exactly one SceneFader");
            Assert.AreEqual(0, roots.Count(n => n == TelemetryRunnerName), "telemetry off: no TelemetryRunner");
        }

        [UnityTest]
        public IEnumerator RoundTripKeepsOneOfEachDdolRoot()
        {
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch());
            SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
            yield return WaitForState(AppState.Title, AppScenes.Title);
            var before = DdolRootNames();

            var flow = Services.Get<AppFlow>();
            flow.GoToGame(0);
            yield return WaitForState(AppState.Game, AppScenes.Game);
            Assert.AreEqual(1, SceneManager.sceneCount);
            Assert.AreEqual(0, flow.Stage);

            flow.Go(AppState.StageSelect);
            yield return WaitForState(AppState.StageSelect, AppScenes.Title);
            Assert.AreEqual(1, SceneManager.sceneCount);

            var after = DdolRootNames();
            CollectionAssert.AreEquivalent(before, after, "Title → Game → Title adds no DontDestroyOnLoad root");
            Assert.LessOrEqual(after.GroupBy(n => n).Max(g => g.Count()), 1, "at most one root of each name");
        }

        [UnityTest]
        public IEnumerator LevelLaunchGoesFromBootStraightToGame()
        {
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch("-level", "3"));
            SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
            yield return WaitForState(AppState.Game, AppScenes.Game);

            var flow = Services.Get<AppFlow>();
            Assert.AreEqual(2, flow.Stage, "-level 3 = stage index 2");
            var game = Object.FindAnyObjectByType<GameController>();
            Assert.IsNotNull(game);
            Assert.AreEqual(2, game.LevelIndex, "the board loaded the launch stage");
        }

        [UnityTest]
        public IEnumerator TitlePlayedDirectlyBootstraps()
        {
            Assert.IsFalse(Bootstrap.HasRunThisPlay);
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch());
            SceneManager.LoadScene(AppScenes.Title, LoadSceneMode.Single); // as pressing Play on Title.unity
            yield return WaitForState(AppState.Title, AppScenes.Title);
            Assert.IsTrue(Bootstrap.HasRunThisPlay, "TitleScreen ran the bootstrap");
            Assert.IsTrue(Services.Has<AppFlow>());
        }

        [UnityTest]
        public IEnumerator GamePlayedDirectlyBootstraps()
        {
            Assert.IsFalse(Bootstrap.HasRunThisPlay);
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch());
            SceneManager.LoadScene(AppScenes.Game, LoadSceneMode.Single); // as pressing Play on Game.unity
            yield return WaitForState(AppState.Game, AppScenes.Game);
            Assert.IsTrue(Bootstrap.HasRunThisPlay, "GameController ran the bootstrap");
            var game = Object.FindAnyObjectByType<GameController>();
            Assert.AreEqual(game.startLevel - 1, game.LevelIndex, "the inspector start level");
        }

        [UnityTest]
        public IEnumerator ASecondRunInTheSamePlayStartsFresh()
        {
            // Domain reload is off: what entering Play Mode resets (SubsystemRegistration) must be enough for a second run.
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch());
            SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
            yield return WaitForState(AppState.Title, AppScenes.Title);
            var firstFlow = Services.Get<AppFlow>();

            Bootstrap.ResetForTest();
            DestroyDdolRoots();
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch());
            SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
            yield return WaitForState(AppState.Title, AppScenes.Title);
            Assert.AreNotSame(firstFlow, Services.Get<AppFlow>(), "a new AppFlow, not the previous run's");
            Assert.AreEqual(1, DdolRootNames().Count(n => n == "SceneFader"));
        }

        static string TelemetryRunnerName => SevenSteps.Game.Telemetry.TelemetryRunner.ObjectName;
        static IEnumerator WaitForState(AppState state, string scene) => PlayEnv.WaitForState(state, scene);
        static List<string> DdolRootNames() => PlayEnv.DdolRootNames();
        static void DestroyDdolRoots() => PlayEnv.DestroyDdolRoots();
    }
}
