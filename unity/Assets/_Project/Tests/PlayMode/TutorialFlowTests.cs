using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests.PlayMode
{
    /// <summary>
    /// The new game path in Play Mode (docs/tutorial.md 2.4, lead 2026-11-13): play on a fresh save -> the prologue ->
    /// T1..T3 -> stage 1; -newgamecheck (the smoke step) end to end; -tutorial N; the map's tutorial entry; and -flowcheck
    /// under U5 (the smoke flow step read the old result panel). Owner: gameplay-ui.
    /// </summary>
    public sealed class TutorialFlowTests
    {
        const float Long = 120f;

        [SetUp] public void SetUp() => PlayEnv.Reset();
        [TearDown] public void TearDown() => PlayEnv.Reset();

        static IEnumerator Boot(AppState state, string scene, params string[] extra)
        {
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch(extra));
            SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
            yield return PlayEnv.WaitForState(state, scene);
        }

        static IEnumerator WaitFor(Func<bool> done, string what, float timeout = PlayEnv.Timeout)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!done())
            {
                if (Time.realtimeSinceStartup - t0 > timeout) Assert.Fail("timed out waiting for " + what);
                yield return null;
            }
        }

        /// Collects "[SevenSteps] ..." log lines that contain <paramref name="part"/> while it is subscribed.
        sealed class LogLines : IDisposable
        {
            readonly string part;
            public readonly List<string> Lines = new List<string>();
            public LogLines(string part) { this.part = part; Application.logMessageReceived += On; }
            void On(string msg, string stack, LogType type) { if (msg.Contains(part)) Lines.Add(msg); }
            public void Dispose() => Application.logMessageReceived -= On;
        }

        [UnityTest]
        public IEnumerator NewGameGoesThroughThePrologueToT1AndSkipLeadsToStageOne()
        {
            yield return Boot(AppState.Title, AppScenes.Title, "-ui5");
            var title = Object.FindAnyObjectByType<TitleScreen>();
            title.U5Title.Play.Press();
            Assert.IsTrue(title.Cutscene.IsOpen, "the prologue first");
            title.CutsceneView.Skip.Press();
            yield return PlayEnv.WaitForState(AppState.Game, AppScenes.Game, 20f);
            var flow = Services.Get<AppFlow>();
            Assert.IsTrue(flow.InTutorial, "then the tutorial");
            Assert.AreEqual(0, flow.Tutorial, "from T1");
            var game = Object.FindAnyObjectByType<GameController>();
            Assert.IsTrue(game.Board.InTutorial);
            Assert.AreEqual(0, game.TutorialRun.Index);
            Assert.IsTrue(game.TutorialCue.Visible);
            Assert.AreEqual(LevelCatalog.Tutorials[0].cues[TutorialCues.Start(LevelCatalog.Tutorials[0])].Line("ko"), game.TutorialCue.Line);
            Assert.IsFalse(game.U5Hud.Hint.gameObject.activeSelf, "no H on a tutorial board");
            game.TutorialRun.Skip(); // the skip icon / Esc
            yield return WaitFor(() => !flow.InTutorial && game.Board.LevelIndex == 0, "stage 1 after the skip");
            Assert.IsFalse(game.TutorialCue.Visible);
            Assert.IsFalse(Services.Get<SaveSystem>().TutorialSeen(TutorialBoards.SaveId(LevelCatalog.Tutorials[0])), "a skipped board is not done");
            Assert.AreEqual(0, Services.Get<SaveSystem>().Progress.ClearedCount, "the tutorial wrote no progress");
            for (int f = 0; f < 10; f++) yield return null;
            Assert.IsFalse(game.U5Hud.Hint.gameObject.activeSelf, "stage 1 on a fresh save: the hint stays hidden until 3 tries (HintGate, 2026-11-14)");
        }

        [UnityTest]
        public IEnumerator NewGameCheckSolvesEveryTutorialBoardToStageOne()
        {
            using (var log = new LogLines("New game check"))
            {
                yield return Boot(AppState.Title, AppScenes.Title, "-ui5", "-newgamecheck");
                yield return WaitFor(() => log.Lines.Exists(l => l.Contains(": arrived") || l.Contains(": FAIL")), "the new game check's result", Long);
                Assert.That(log.Lines, Has.Some.EqualTo(NewGameCheck.Ok), string.Join(" | ", log.Lines));
                Assert.That(log.Lines, Has.None.Contains("FAIL"));
            }
            var save = Services.Get<SaveSystem>();
            foreach (var t in LevelCatalog.Tutorials) Assert.IsTrue(save.TutorialSeen(TutorialBoards.SaveId(t)), t.id + " done");
            Assert.AreEqual(0, save.Progress.ClearedCount, "no stage progress from the tutorial");
            Assert.AreEqual(0, Services.Get<AppFlow>().Stage);
        }

        [UnityTest]
        public IEnumerator TutorialLaunchOptionOpensThatBoard()
        {
            yield return Boot(AppState.Game, AppScenes.Game, "-ui5", "-tutorial", "2");
            var flow = Services.Get<AppFlow>();
            var game = Object.FindAnyObjectByType<GameController>();
            Assert.AreEqual(1, flow.Tutorial);
            Assert.AreEqual(1, game.TutorialRun.Index);
            Assert.AreEqual(LevelCatalog.Tutorials[1].level.bodies, game.Board.State.Level.Bodies);
            PlayEnv.Reset();
            yield return Boot(AppState.Title, AppScenes.Title, "-ui5", "-tutorial", "9"); // past the boards: the title
        }

        [UnityTest]
        public IEnumerator TheMapsTutorialEntryReplaysFromT1AndSkipReturnsToTheMap()
        {
            yield return Boot(AppState.Title, AppScenes.Title, "-ui5");
            var save = Services.Get<SaveSystem>();
            var levels = LevelCatalog.Load(Object.FindAnyObjectByType<TitleScreen>().assets);
            save.Progress.MarkCleared(levels[0], 1, 1); // a player who has played: the replay goes back to the map
            var title = Object.FindAnyObjectByType<TitleScreen>();
            title.U5Title.Map.Press();
            yield return WaitFor(() => title.U5Map != null && title.U5Map.Visible, "the map controls");
            Assert.IsTrue(title.U5Map.Tutorial.gameObject.activeSelf, "the tutorial entry on the map");
            title.U5Map.Tutorial.Press();
            yield return PlayEnv.WaitForState(AppState.Game, AppScenes.Game, 20f);
            Assert.AreEqual(0, Services.Get<AppFlow>().Tutorial, "T1");
            Object.FindAnyObjectByType<GameController>().TutorialRun.Skip();
            yield return PlayEnv.WaitForState(AppState.StageSelect, AppScenes.Title, 20f);
        }

        /// The smoke flow step under U5 (the default): the U5 clear's model replaces the old result text checks.
        [UnityTest]
        public IEnumerator FlowCheckPassesUnderU5AtAPlanetEnd()
        {
            yield return Boot(AppState.Title, AppScenes.Title, "-ui5");
            int stage = -1;
            var levels = LevelCatalog.Load(Object.FindAnyObjectByType<TitleScreen>().assets);
            for (int s = 0; s + 1 < levels.Length && stage < 0; s++)
                if (LevelCatalog.ChapterKeyOf(s) != LevelCatalog.ChapterKeyOf(s + 1)) stage = s;
            string path = LevelCatalog.PathOf(stage);
            PlayEnv.Reset();
            using (var log = new LogLines("Flow check"))
            {
                yield return Boot(AppState.Game, AppScenes.Game, "-ui5", "-level", (stage + 1).ToString(), "-replay", path, "-flowcheck");
                yield return WaitFor(() => log.Lines.Count > 0, "the flow check's result", Long);
                Assert.That(log.Lines[0], Does.StartWith($"[SevenSteps] Flow check stage {stage + 1}: OK"), log.Lines[0]);
            }
        }
    }
}
