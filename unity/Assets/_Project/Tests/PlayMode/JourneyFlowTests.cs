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
using SevenSteps.Game.UI.U5;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests.PlayMode
{
    /// <summary>
    /// Stage select v4 journey in the Editor Play Mode (docs/stage-select-transitions.md 5, docs/scene-flow.md 10 P4;
    /// architect review 2026-11-12: the journey never skips the result). This replaces the player-build smoke step
    /// (-journeycheck). With -newmap, the solver's path (levels.json expected.path) wins and the clear shows first; then
    /// - U5 clear's Next (-ui5) on stage 1: the advance to stage 2 in the Game scene, no reload;
    /// - the current result panel's Next on the first planet's last stage: the pullback, the Title scene's star map settles
    ///   JourneyReturn on the map pose and lights that planet's outgoing trail;
    /// - U5 clear's map on stage 1: the same pullback, JourneyReturn kind Back, nothing newly lit.
    /// Same environment as SceneFlowTests (injected launch options, save key "playtest", reset between tests).
    /// </summary>
    public sealed class JourneyFlowTests
    {
        const float Timeout = 20f;

        [SetUp] public void SetUp() => PlayEnv.Reset();
        [TearDown] public void TearDown() => PlayEnv.Reset();

        static IEnumerator Boot(params string[] args)
        {
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch(args));
            SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
            yield return PlayEnv.WaitForState(AppState.Game, AppScenes.Game);
        }

        /// Wins <paramref name="stage"/> and waits for the clear (Result) with the given panel up.
        static IEnumerator WinToTheClear(GameController game, int stage, Func<bool> panelShown)
        {
            Win(game, stage);
            yield return WaitFor(() => Services.Get<AppFlow>().State == AppState.Result && panelShown(), "the clear (the journey never skips it)");
            Assert.IsFalse(game.Journey.Blocks, "nothing moves before Next");
        }

        [UnityTest]
        public IEnumerator U5ClearNextAdvancesToTheNextStage()
        {
            yield return Boot("-ui5", "-newmap", "-level", "1");
            var game = Object.FindAnyObjectByType<GameController>();
            Assert.IsNotNull(game.Journey, "-newmap turns the journey on");
            var flow = Services.Get<AppFlow>();
            var clear = Object.FindAnyObjectByType<U5ClearView>(FindObjectsInactive.Include);
            Assert.IsNotNull(clear, "-ui5: the U5 clear");

            yield return WinToTheClear(game, 0, () => clear.IsShown);
            clear.Next.Press();
            Assert.IsTrue(game.Flow.Leaving);
            Assert.IsFalse(clear.IsShown, "the clear hides before the advance");
            clear.Next.Press(); // a second press starts nothing
            yield return WaitFor(() => game.Journey.Blocks, "the advance transition to start");
            yield return WaitFor(() => !game.Journey.Blocks, "the advance transition to settle");
            yield return null;

            var view = game.Board.View;
            Assert.AreEqual(AppState.Game, flow.State);
            Assert.AreEqual(1, flow.Stage, "stage 2");
            Assert.AreEqual(1, game.LevelIndex);
            Assert.IsFalse(view.CameraHeld, "the camera is the board's again");
            Assert.AreEqual(Vector3.zero, view.BoardRoot.localPosition, "the new board has risen");
            Assert.IsTrue(view.Player.activeSelf, "the brothers stand on the new board");
            Assert.IsNull(GameObject.Find("Board (snapshot)"), "the cleared board's copy is gone");
            Assert.IsFalse(game.Flow.Leaving);
        }

        [UnityTest]
        public IEnumerator NextOnThePlanetsLastStageReturnsToTheMapAndLightsTheTrail()
        {
            yield return Boot("-newmap", "-level", "1");
            var game = Object.FindAnyObjectByType<GameController>();
            var flow = Services.Get<AppFlow>();

            int last = FirstPlanetLastStage(flow.StageCount);
            flow.GoToGame(last);
            yield return WaitFor(() => game.LevelIndex == last && !game.Journey.Blocks, $"stage {last + 1} on the board");

            var panel = Object.FindAnyObjectByType<ResultView>(FindObjectsInactive.Include);
            yield return WinToTheClear(game, last, () => panel != null && panel.IsShown);
            game.Flow.Next(); // the current result panel's Next
            yield return PlayEnv.WaitForState(AppState.StageSelect, AppScenes.Title, Timeout);
            var ret = Services.Get<JourneyReturn>();
            yield return WaitFor(() => !ret.Pending, "the return pullback to settle");
            yield return null;

            Assert.AreEqual(ReturnKind.PlanetComplete, ret.LastKind);
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

        /// The last stage under -ui5: the U5 clear, its Next asks for the ending, the U5 ending cards step through with Next
        /// and the last card's map goes to the star map (the story off: straight to the map).
        [UnityTest]
        public IEnumerator U5EndingStepsThroughTheCardsToTheMap()
        {
            yield return Boot("-ui5", "-level", "1");
            var game = Object.FindAnyObjectByType<GameController>();
            int lastStage = Services.Get<AppFlow>().StageCount - 1;
            Services.Get<AppFlow>().GoToGame(lastStage);
            yield return WaitFor(() => game.LevelIndex == lastStage && Services.Get<AppFlow>().State == AppState.Game, "the last stage on the board");
            var clear = Object.FindAnyObjectByType<U5ClearView>(FindObjectsInactive.Include);
            Win(game, lastStage);
            yield return WaitFor(() => Services.Get<AppFlow>().State == AppState.Result && clear.IsShown, "the last clear");
            clear.Next.Press(); // the last stage: [끝까지 보기]
            yield return null;
            var flow = Services.Get<AppFlow>();
            if (game.Ending.IsOpen)
            {
                Assert.IsNotNull(game.U5Ending, "-ui5: the U5 ending cards");
                Assert.IsTrue(game.U5Ending.Visible);
                int guard = 0;
                while (!game.U5Ending.Map.Shown)
                {
                    Assert.IsTrue(game.U5Ending.Next.Shown, "next on every card but the last");
                    game.U5Ending.Next.Press();
                    yield return null;
                    Assert.Less(++guard, 10, "the cards end");
                }
                Assert.IsFalse(game.U5Ending.Skip.Shown, "the last card has no skip");
                game.U5Ending.Map.Press();
            }
            yield return PlayEnv.WaitForState(AppState.StageSelect, AppScenes.Title, Timeout);
            Assert.AreEqual(AppState.StageSelect, flow.State);
        }

        [UnityTest]
        public IEnumerator U5ClearMapReturnsWithoutLighting()
        {
            yield return Boot("-ui5", "-newmap", "-level", "1");
            var game = Object.FindAnyObjectByType<GameController>();
            var clear = Object.FindAnyObjectByType<U5ClearView>(FindObjectsInactive.Include);
            yield return WinToTheClear(game, 0, () => clear.IsShown);
            clear.Map.Press();
            Assert.AreEqual(AppState.Result, Services.Get<AppFlow>().State, "the pullback plays first");
            yield return PlayEnv.WaitForState(AppState.StageSelect, AppScenes.Title, Timeout);
            var ret = Services.Get<JourneyReturn>();
            yield return WaitFor(() => !ret.Pending, "the return pullback to settle");
            yield return null;

            Assert.AreEqual(ReturnKind.Back, ret.LastKind);
            var title = Object.FindAnyObjectByType<TitleScreen>();
            title.StageSelect.MapPose(out var position, out _, out _);
            Assert.Less(Vector3.Distance(title.StageSelect.MapCamera.transform.position, position), 0.01f, "the camera ends on the map pose");
            var model = title.StageSelect.Model;
            bool lit = Enumerable.Range(0, model.Trails.Count).Any(e => model.Trails[e].from == 0 && model.TrailLit(e));
            Assert.IsFalse(lit, "one stage of the planet: nothing lit");
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
