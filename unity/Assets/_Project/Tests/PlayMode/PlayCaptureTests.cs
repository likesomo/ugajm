using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// Play Mode capture, headless (docs/scene-flow.md 9.2; user rule 2026-11-11: batch mode only, no Editor GUI, no player
    /// build). Runs inside `unity.sh playcapture` = -batchmode -runTests -testPlatform PlayMode with a filter on this class
    /// ([Explicit]: the normal `playtest` run skips it). Batch mode has a graphics device but no Game view, so every active
    /// camera renders (Camera.Render, depth order) into one 1280x720 RenderTexture, and Screen Space - Overlay canvases are switched to Screen Space -
    /// Camera on the main camera for the capture so the UI is in the picture. A few frames pass at that size (the board and
    /// the UI lay out for 16:9) before ReadPixels. Checks: not one flat colour; the board shot (SmokeView.Board, magenta
    /// clear, UI hidden) has at least 500 non-magenta pixels in the board area and its control (SmokeView.Empty) fewer.
    /// Output: reports/playcapture/NAME.png and a log line "[SevenSteps] Play capture NAME: ...".
    /// </summary>
    [Explicit("headless capture: unity.sh playcapture")]
    [Category("Capture")]
    [Timeout(120000)]
    public sealed class PlayCaptureTests
    {
        const int Width = 1280, Height = 720, BoardMin = 500;
        const float SettleSeconds = 1.5f;

        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/playcapture"));

        [SetUp] public void SetUp() => PlayEnv.Reset();
        [TearDown] public void TearDown() => PlayEnv.Reset();

        [UnityTest]
        public IEnumerator Title()
        {
            yield return Start(AppState.Title, AppScenes.Title);
            var px = new Pixels();
            yield return Capture("title", px);
            AssertVaried("title", px.Value);
        }

        [UnityTest]
        public IEnumerator Game()
        {
            yield return Start(AppState.Game, AppScenes.Game, "-level", "1");
            var px = new Pixels();
            yield return Capture("game", px);
            AssertVaried("game", px.Value);
        }

        /// Step constellation (user decision 2026-11-12 "C") on a small board after 6 steps (the 7th star calls), on the wide
        /// 17 x 3 stage 14 after 3, and while choosing after the 7th step (stage 38): reports/playcapture/step-sky-*.png.
        [UnityTest]
        public IEnumerator StepSky([Values("6:DUDUDU:6", "14:RRR:3", "38:DUDUDUR:7")] string shot)
        {
            var part = shot.Split(':');
            yield return Start(AppState.Game, AppScenes.Game, "-level", part[0], "-replay", part[1]);
            var px = new Pixels();
            yield return Capture($"step-sky-{part[0]}", px);
            AssertVaried("step-sky", px.Value);
            var view = Object.FindAnyObjectByType<BoardView>();
            Assert.IsNotNull(view?.Sky, "BoardView with a step sky");
            Assert.IsTrue(view.Sky.Shown, "constellation shown");
            Assert.AreEqual(int.Parse(part[2]), view.Sky.Sky.Lit, "stars lit = steps walked");
            var band = view.StepSkyBand;
            Assert.Greater(band.width, 0f, "StepSkyBand while the sky shows");
            Assert.GreaterOrEqual(band.yMin, StepSkyView.HudTopBand - 8.01f, "band under the HUD top band (Band grows by 8)");
            Debug.Log($"[SevenSteps] Play capture step-sky-{part[0]}: lit {view.Sky.Sky.Lit}, star {view.Sky.Placed.Size:0}, bottom {view.Sky.Placed.Bottom:0}, top {view.Sky.Placed.Top:0}, band {band}");
        }

        [UnityTest]
        public IEnumerator GameBoardAgainstEmptyControl()
        {
            yield return Start(AppState.Game, AppScenes.Game, "-level", "1", "-smoke", "board");
            var board = new Pixels();
            yield return Capture("game-board", board);
            int b = BoardPixels(board.Value);

            PlayEnv.Reset();
            yield return Start(AppState.Game, AppScenes.Game, "-level", "1", "-smoke", "empty");
            var empty = new Pixels();
            yield return Capture("game-empty", empty);
            int e = BoardPixels(empty.Value);

            Debug.Log($"[SevenSteps] Play capture board: {b} board pixels, empty control {e} (min {BoardMin})");
            Assert.Less(e, BoardMin, "the empty control must measure below the threshold, or the measurement is broken");
            Assert.GreaterOrEqual(b, BoardMin, "the board is drawn");
        }

        /// The journey advance (stage 1 cleared, -newmap) frozen part-way: the JourneyRunner is disabled after about 0.45 s and
        /// 0.9 s of its 1.5 s, so the camera, the risen board and the cleared board stay where they are for the capture.
        [UnityTest]
        public IEnumerator AdvanceMidway()
        {
            yield return Start(AppState.Game, AppScenes.Game, "-newmap", "-level", "1");
            var game = Object.FindAnyObjectByType<GameController>();
            Assert.IsNotNull(game.Journey, "-newmap turns the journey on");
            float c0 = Time.realtimeSinceStartup; // the -level start shows the chapter card (2.5 s): play after it
            while (Object.FindAnyObjectByType<SevenSteps.Game.UI.ChapterCardView>() != null)
            {
                Assert.Less(Time.realtimeSinceStartup - c0, 10f, "the chapter card to close");
                yield return null;
            }
            string path = LevelCatalog.PathOf(0);
            if (path.Length > 1) game.Board.Replay(path.Substring(0, path.Length - 1));
            Assert.IsTrue(SevenSteps.Core.Rules.TryParseLetter(path[path.Length - 1], out var act));
            game.Board.Send(SevenSteps.Core.Command.FromAct(act));
            float t0 = Time.realtimeSinceStartup;
            while (!game.Journey.Blocks || Object.FindAnyObjectByType<SevenSteps.Game.Flow.JourneyRunner>() is not { Busy: true })
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 20f, "the advance to start");
                yield return null;
            }
            var runner = Object.FindAnyObjectByType<SevenSteps.Game.Flow.JourneyRunner>();
            foreach (var (label, at) in new[] { ("advance-30", 0.45f), ("advance-60", 0.9f) })
            {
                runner.enabled = true;
                float s = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - s < at - (label == "advance-60" ? 0.45f : 0f)) yield return null;
                runner.enabled = false;
                var px = new Pixels();
                yield return Capture(label, px, 0.1f);
                AssertVaried(label, px.Value);
            }
            runner.enabled = true;
        }

        /// U5 in-game before / after (docs/ui-u5-kit.md 3; gameplay-ui merge requests): stage 4 after one step (HUD) and
        /// after its 7th step (KEEP / SWITCH), the current UI ("u4-") and -ui5 ("u5-"), KO and EN; the choice also as a first
        /// visit (explanations on) and with SWITCH focused. `unity.sh playcapture U5InGame`.
        [UnityTest]
        public IEnumerator U5InGame()
        {
            foreach (var (name, args, explain, focusSwitch) in new (string, string[], bool, bool)[]
            {
                ("u4-hud", new[] { "-level", "4", "-replay", "D" }, false, false),
                ("u5-hud", new[] { "-ui5", "-level", "4", "-replay", "D" }, false, false),
                ("u4-choice", new[] { "-level", "4", "-replay", "DURRRRR" }, false, false),
                ("u5-choice", new[] { "-ui5", "-level", "4", "-replay", "DURRRRR" }, false, false),
                ("u5-choice-first", new[] { "-ui5", "-level", "4", "-replay", "DURRRRR" }, true, false),
                ("u5-choice-switch", new[] { "-ui5", "-level", "4", "-replay", "DURRRRR" }, false, true),
                ("u4-choice-en", new[] { "-lang", "en", "-level", "4", "-replay", "DURRRRR" }, true, false),
                ("u5-choice-en", new[] { "-ui5", "-lang", "en", "-level", "4", "-replay", "DURRRRR" }, true, false),
                ("u5-hud-en", new[] { "-ui5", "-lang", "en", "-level", "4", "-replay", "D" }, false, false),
            })
            {
                PlayEnv.Reset();
                yield return Start(AppState.Game, AppScenes.Game, args);
                var game = Object.FindAnyObjectByType<GameController>();
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 1f) yield return null; // the replay snap and the HUD refresh
                if (explain) { game.Board.Choice.ExplainChoices = () => true; game.Board.Choice.Refresh(); }
                if (focusSwitch) game.Board.Choice.Focus(false);
                var px = new Pixels();
                yield return Capture(name, px);
                AssertVaried(name, px.Value);
            }
        }

        // ---------- ----------

        static IEnumerator Start(AppState state, string scene, params string[] args)
        {
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch(args));
            SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
            yield return PlayEnv.WaitForState(state, scene);
        }

        sealed class Pixels { public Color32[] Value; }

        /// Renders every active camera (depth order) into a 1280x720 texture with the overlay UI on the main camera, settles,
        /// reads it back and saves NAME.png.
        static IEnumerator Capture(string name, Pixels result, float settle = SettleSeconds)
        {
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude).Where(c => c.enabled).OrderBy(c => c.depth).ToList();
            var main = Camera.main != null ? Camera.main : cams.LastOrDefault();
            Assert.IsNotNull(main, "a camera to capture");
            var canvases = new List<(Canvas canvas, RenderMode mode, Camera cam, float plane)>();
            try
            {
                foreach (var c in cams) c.targetTexture = rt;
                foreach (var cv in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                    if (cv.isRootCanvas && cv.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        canvases.Add((cv, cv.renderMode, cv.worldCamera, cv.planeDistance));
                        cv.renderMode = RenderMode.ScreenSpaceCamera;
                        cv.worldCamera = main;
                        cv.planeDistance = main.nearClipPlane + 0.05f + 0.001f * (1000 - Mathf.Clamp(cv.sortingOrder, -999, 999)); // higher order nearer
                    }
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < settle) yield return null; // board refit and UI layout at 16:9
                // Batch mode has no Game view: WaitForEndOfFrame never comes. Render each camera into the texture now, in
                // depth order, with its own clear flags (as the screen would composite them).
                Canvas.ForceUpdateCanvases();
                foreach (var c in cams) if (c != null && c.isActiveAndEnabled) c.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                Directory.CreateDirectory(OutDir);
                File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
                result.Value = tex.GetPixels32();
                Object.Destroy(tex);
                Debug.Log($"[SevenSteps] Play capture {name}: {Path.Combine(OutDir, name + ".png")}");
            }
            finally
            {
                foreach (var c in cams) if (c != null) c.targetTexture = null;
                foreach (var (cv, mode, cam, plane) in canvases)
                    if (cv != null) { cv.renderMode = mode; cv.worldCamera = cam; cv.planeDistance = plane; }
                rt.Release();
                Object.Destroy(rt);
            }
        }

        static void AssertVaried(string name, Color32[] px)
        {
            var c0 = px[0];
            int n = px.Count(c => Math.Abs(c.r - c0.r) + Math.Abs(c.g - c0.g) + Math.Abs(c.b - c0.b) > 24);
            Debug.Log($"[SevenSteps] Play capture {name}: {n} pixels differ from the corner");
            Assert.GreaterOrEqual(n, BoardMin, $"{name} is almost one flat colour");
        }

        /// Pixels in the board area (x 30-70 %, y 33-66 %) that are not the smoke clear #FF00FF (board-pixels.ps1 rule).
        static int BoardPixels(Color32[] px)
        {
            int n = 0;
            for (int y = Height * 33 / 100; y < Height * 66 / 100; y++)
                for (int x = Width * 30 / 100; x < Width * 70 / 100; x++)
                {
                    var c = px[y * Width + x];
                    if (Math.Abs(c.r - 255) + c.g + Math.Abs(c.b - 255) > 24) n++;
                }
            return n;
        }
    }
}
