using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests.PlayMode
{
    /// <summary>
    /// Stage select v4 journey in the Editor Play Mode (docs/stage-select-transitions.md 5, docs/scene-flow.md 10 P4). This
    /// replaces the player-build smoke step (-journeycheck). With -newmap, the solver's path (levels.json expected.path) wins:
    /// stage 1 advances to stage 2 in the Game scene with no result; the first planet's last stage pulls back and the Title
    /// scene shows the star map, settles JourneyReturn on the map pose and lights that planet's outgoing trail.
    /// Same environment as SceneFlowTests (injected launch options, save key "playtest", reset between tests).
    /// </summary>
    public sealed class JourneyFlowTests
    {
        const float Timeout = 20f;

        [SetUp] public void SetUp() => PlayEnv.Reset();
        [TearDown] public void TearDown() => PlayEnv.Reset();

        [UnityTest]
        public IEnumerator ClearAdvancesToTheNextStageWithoutTheResult()
        {
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch("-newmap", "-level", "1"));
            SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
            yield return PlayEnv.WaitForState(AppState.Game, AppScenes.Game);
            var game = Object.FindAnyObjectByType<GameController>();
            Assert.IsNotNull(game.Journey, "-newmap turns the journey on");
            var flow = Services.Get<AppFlow>();

            Win(game, 0);
            yield return WaitFor(() => game.Journey.Blocks, "the advance transition to start");
            yield return WaitFor(() => !game.Journey.Blocks, "the advance transition to settle");
            yield return null;

            var view = game.Board.View;
            Assert.AreEqual(AppState.Game, flow.State, "no Result state");
            Assert.AreEqual(1, flow.Stage, "stage 2");
            Assert.AreEqual(1, game.LevelIndex);
            Assert.IsFalse(view.CameraHeld, "the camera is the board's again");
            Assert.AreEqual(Vector3.zero, view.BoardRoot.localPosition, "the new board has risen");
            Assert.IsTrue(view.Player.activeSelf, "the brothers stand on the new board");
            Assert.IsNull(GameObject.Find("Board (snapshot)"), "the cleared board's copy is gone");
        }

        [UnityTest]
        public IEnumerator PlanetCompleteReturnsToTheMapAndLightsTheTrail()
        {
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch("-newmap", "-level", "1"));
            SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
            yield return PlayEnv.WaitForState(AppState.Game, AppScenes.Game);
            var game = Object.FindAnyObjectByType<GameController>();
            var flow = Services.Get<AppFlow>();

            int last = FirstPlanetLastStage(flow.StageCount);
            flow.GoToGame(last);
            yield return WaitFor(() => game.LevelIndex == last && !game.Journey.Blocks, $"stage {last + 1} on the board");

            Win(game, last);
            yield return PlayEnv.WaitForState(AppState.StageSelect, AppScenes.Title, Timeout);
            var ret = Services.Get<JourneyReturn>();
            yield return WaitFor(() => !ret.Pending, "the return pullback to settle");
            yield return null;

            var title = Object.FindAnyObjectByType<TitleScreen>();
            Assert.IsNotNull(title.StageSelect, "the new map is up");
            title.StageSelect.MapPose(out var position, out _, out float size);
            var cam = title.StageSelect.MapCamera;
            Assert.Less(Vector3.Distance(cam.transform.position, position), 0.01f, "the camera ends on the map pose");
            Assert.AreEqual(size, cam.orthographicSize, 0.01f);
            var model = title.StageSelect.Model;
            bool lit = Enumerable.Range(0, model.Trails.Count).Any(e => model.Trails[e].from == 0 && model.TrailLit(e));
            Assert.IsTrue(lit, "the completed planet's outgoing trail is lit");
        }

        /// Plays the stored solution: all moves but the last snapped, the last one presented (like a player's winning step).
        static void Win(GameController game, int stage)
        {
            string path = LevelCatalog.PathOf(stage);
            Assert.IsNotEmpty(path, $"stage {stage + 1} has expected.path (unity.sh levels)");
            if (path.Length > 1) game.Board.Replay(path.Substring(0, path.Length - 1));
            Assert.IsTrue(Rules.TryParseLetter(path[path.Length - 1], out var act));
            game.Board.Send(Command.FromAct(act));
        }

        static int FirstPlanetLastStage(int stageCount)
        {
            for (int s = 0; s + 1 < stageCount; s++)
                if (LevelCatalog.ChapterKeyOf(s) != LevelCatalog.ChapterKeyOf(s + 1)) return s;
            Assert.Fail("levels.json has one planet");
            return -1;
        }

        static IEnumerator WaitFor(Func<bool> done, string what)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!done())
            {
                if (Time.realtimeSinceStartup - t0 > Timeout) Assert.Fail("timed out waiting for " + what);
                yield return null;
            }
        }
    }
}
