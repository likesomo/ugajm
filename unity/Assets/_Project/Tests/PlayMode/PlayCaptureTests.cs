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
    /// Play Mode capture, headless (docs/scene-flow.md 9.2, capture rule 2026-11-12: the Rig switches canvases before the scene builds; user rule 2026-11-11: batch mode only, no Editor GUI, no player
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

        /// U5 title, language and credits (-ui5; docs/ui-u5-kit.md 3), KO and EN title; before = `title` (current UI).
        /// `unity.sh playcapture U5Title`.
        [UnityTest]
        public IEnumerator U5Title()
        {
            foreach (string lang in new[] { "ko", "en" })
            {
                PlayEnv.Reset();
                yield return Start(AppState.Title, AppScenes.Title, "-ui5", "-lang", lang);
                var title = Object.FindAnyObjectByType<SevenSteps.Game.UI.TitleScreen>();
                Assert.IsNotNull(title.U5Title, "-ui5: the U5 title");
                var px = new Pixels();
                yield return Capture($"u5-title-{lang}", px);
                AssertVaried($"u5-title-{lang}", px.Value);
                if (lang != "ko") continue;
                title.U5Title.Language.Press();
                yield return null;
                px = new Pixels();
                yield return Capture("u5-language", px);
                AssertVaried("u5-language", px.Value);
                title.U5Language.Next.Press();
                yield return null;
                title.U5Title.Credits.Press();
                yield return null;
                px = new Pixels();
                yield return Capture("u5-credits", px);
                AssertVaried("u5-credits", px.Value);
            }
        }

        /// The v4 star map (-newmap) with a save where the first planet is cleared (lit trail, stars, the brothers' dots).
        [UnityTest]
        public IEnumerator Map()
        {
            yield return Start(AppState.Title, AppScenes.Title, "-newmap");
            var flow = Services.Get<AppFlow>();
            var save = Services.Get<SaveSystem>();
            var levels = LevelCatalog.Load(Object.FindAnyObjectByType<SevenSteps.Game.UI.TitleScreen>().assets);
            for (int s = 0; s < levels.Length && LevelCatalog.ChapterKeyOf(s) == LevelCatalog.ChapterKeyOf(0); s++)
                save.Progress.MarkCleared(levels[s], levels[s].Bodies, 3);
            flow.Go(AppState.StageSelect);
            yield return PlayEnv.WaitForState(AppState.StageSelect, AppScenes.Title);
            var assets = Object.FindAnyObjectByType<SevenSteps.Game.UI.TitleScreen>().assets;
            Debug.Log($"[SevenSteps] Play capture map: planetMapSprites = {(assets.planetMapSprites != null ? assets.planetMapSprites.name : "none")}");
            var px = new Pixels();
            yield return Capture("map", px);
            AssertVaried("map", px.Value);
            // V7 (user choice 2026-11-12): every map planet with a sprite in the set is drawn as that sprite, not its mesh.
            var bodies = Object.FindObjectsByType<SevenSteps.Game.UI.Planets.PlanetBody>(FindObjectsInactive.Exclude);
            int sprites = bodies.Count(b => b.Impostor != null && b.Impostor.enabled && !b.Renderer.enabled);
            Debug.Log($"[SevenSteps] Play capture map: {sprites} of {bodies.Length} planets drawn as sprites");
            if (assets.PlanetMapSprites != null) Assert.AreEqual(bodies.Length, sprites, "every map planet is a V7 sprite");
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

        /// R2 on 40a0561: at the 7th step of stage 38 (KEEP) and the wide stage 14 (SWITCH), the choice (current cards,
        /// -ui5 glyphs) beside the board, clear of the step-star sky band, both UIs, stars visible. `playcapture ChoiceClearOfSky`.
        [UnityTest]
        public IEnumerator ChoiceClearOfSky()
        {
            foreach (var (stage, replay) in new[] { ("38", "DUDUDUR"), ("14", "LLLRRRR") })
            foreach (bool u5 in new[] { false, true })
            {
                PlayEnv.Reset();
                var args = u5 ? new[] { "-ui5", "-level", stage, "-replay", replay } : new[] { "-level", stage, "-replay", replay };
                yield return Start(AppState.Game, AppScenes.Game, args);
                var game = Object.FindAnyObjectByType<GameController>();
                float t0 = Time.realtimeSinceStartup;
                while (!game.Board.Choice.Shown)
                {
                    Assert.Less(Time.realtimeSinceStartup - t0, 10f, "the 7th step's choice");
                    yield return null;
                }
                string name = $"r2-{stage}-{(u5 ? "u5" : "u4")}";
                var px = new Pixels();
                Rect r = default, band = default, area = default;
                var view = game.Board.View;
                yield return Capture(name, px, SettleSeconds, () =>
                {
                    // Measured on the 1280x720 capture canvas, after the choice was placed again on it.
                    band = view.StepSkyBand;
                    area = view.ProjectedArea();
                    RectTransform group = u5 ? Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5GameHud>().Glyphs
                        : Object.FindAnyObjectByType<SevenSteps.Game.UI.HudView>().choice;
                    r = new Rect(group.anchoredPosition.x, -group.anchoredPosition.y, group.sizeDelta.x, group.sizeDelta.y);
                    Debug.Log($"[SevenSteps] Play capture {name}: choice {r}, sky {band}, board {area}, brother {view.BrotherOnCanvas()}");
                });
                AssertVaried(name, px.Value);
                Assert.IsTrue(view.Sky != null && view.Sky.Shown, "the step stars show while choosing");
                Assert.IsFalse(r.Overlaps(band), $"{name}: the choice covers the step stars");
                Assert.IsFalse(r.Overlaps(area), $"{name}: the choice is on the board area");
            }
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
            Rig.Ensure(); // before the scene loads: views lay out at 1280x720 from the start (capture rule 2026-11-12)
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch(args));
            SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
            yield return PlayEnv.WaitForState(state, scene);
        }

        sealed class Pixels { public Color32[] Value; }

        /// <summary>
        /// The capture setup, in place before a scene loads and kept while it plays (architect capture rule 2026-11-12,
        /// docs/reviews/2026-11-12-feat-view-40a0561-u5-hud.md: switch the canvas mode and size first, then build the screen
        /// state). Each scene load (after Awake, before Start) and every frame after: every camera renders into one 1280x720
        /// texture, and every Screen Space - Overlay root canvas becomes Screen Space - Camera on the main camera, so canvases
        /// are 1280x720 when the views place themselves. A DontDestroyOnLoad object; PlayEnv.Reset removes it with the rest.
        /// </summary>
        sealed class Rig : MonoBehaviour
        {
            public const string ObjectName = "PlayCapture rig";
            public static RenderTexture Target { get; private set; }
            /// Draws the converted canvases: still, far from the scene (y 10000), UI layer only, last. A canvas on a moving
            /// scene camera would lag a frame behind it, and views that project world points (the star map) would place
            /// against the old pose; an overlay canvas never moves, and neither does this one.
            static Camera ui;
            /// The UI camera's own texture, cleared to transparent: composited over the scene on read (a second URP base camera
            /// on the scene texture does not keep what the first drew).
            public static RenderTexture UiTarget { get; private set; }
            public static Camera UiCamera => ui;

            public static void Ensure()
            {
                if (FindAnyObjectByType<Rig>() != null) return;
                Target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "PlayCapture" };
                UiTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "PlayCapture UI" };
                var go = new GameObject(ObjectName);
                DontDestroyOnLoad(go);
                go.AddComponent<Rig>();
                var camGo = new GameObject("PlayCapture UI camera");
                camGo.transform.SetParent(go.transform, false);
                camGo.transform.position = new Vector3(0, 10000, 0);
                ui = camGo.AddComponent<Camera>();
                ui.orthographic = true;
                ui.cullingMask = 1 << 5; // UI
                ui.clearFlags = CameraClearFlags.SolidColor;
                ui.backgroundColor = new Color(0, 0, 0, 0);
                ui.depth = 1000;
                ui.nearClipPlane = 0.1f;
                ui.farClipPlane = 50f;
                ui.targetTexture = UiTarget;
                SceneManager.sceneLoaded += OnLoaded;
            }

            static void OnLoaded(Scene s, LoadSceneMode m) => Apply();
            void LateUpdate() => Apply();

            void OnDestroy()
            {
                SceneManager.sceneLoaded -= OnLoaded;
                foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Include))
                    if (c.targetTexture != null && (c.targetTexture == Target || c.targetTexture == UiTarget)) c.targetTexture = null;
                if (Target != null) { Target.Release(); Destroy(Target); }
                if (UiTarget != null) { UiTarget.Release(); Destroy(UiTarget); }
                UiTarget = null;
                Target = null;
                ui = null;
            }

            public static void Apply()
            {
                if (Target == null || ui == null) return;
                foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Include))
                    if (c.targetTexture == null) c.targetTexture = Target;
                foreach (var cv in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                    if (cv.isRootCanvas && cv.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        cv.renderMode = RenderMode.ScreenSpaceCamera;
                        cv.worldCamera = ui;
                        cv.planeDistance = ui.nearClipPlane + 0.05f + 0.001f * (1000 - Mathf.Clamp(cv.sortingOrder, -999, 999)); // higher order nearer
                    }
                    else if (cv.isRootCanvas && cv.renderMode == RenderMode.ScreenSpaceCamera && cv.worldCamera == null) cv.worldCamera = ui;
            }
        }

        /// Waits <paramref name="settle"/>, checks that every screen canvas is 1280x720 (logged; another size fails), renders
        /// every active camera into the rig's texture in depth order, reads it back and saves NAME.png. Views that place
        /// themselves when shown are placed again before the shot (the KEEP / SWITCH choice; <paramref name="afterSettle"/>
        /// for a test's own measurements on the capture canvas).
        static IEnumerator Capture(string name, Pixels result, float settle = SettleSeconds, Action afterSettle = null)
        {
            Assert.IsNotNull(Rig.Target, "the capture rig is installed (Start)");
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < settle) yield return null; // board refit and UI layout settle
            Rig.Apply();
            Canvas.ForceUpdateCanvases();
            var sizes = new List<string>();
            foreach (var cv in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!cv.isRootCanvas || cv.renderMode == RenderMode.WorldSpace || !cv.enabled) continue;
                var px = cv.pixelRect.size;
                sizes.Add($"{cv.name} {px.x:0}x{px.y:0}");
                Assert.IsTrue(Mathf.Abs(px.x - Width) < 0.5f && Mathf.Abs(px.y - Height) < 0.5f,
                    $"capture {name}: canvas {cv.name} is {px.x:0}x{px.y:0}, not {Width}x{Height} (capture rule 2026-11-12)");
            }
            Debug.Log($"[SevenSteps] Play capture {name}: canvases {string.Join(", ", sizes)}");
            var gc = Object.FindAnyObjectByType<GameController>();
            if (gc != null && gc.Board != null && gc.Board.Choice != null) gc.Board.Choice.Refresh();
            afterSettle?.Invoke();
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            // Batch mode has no Game view: WaitForEndOfFrame never comes. Render each camera into the texture now, in depth
            // order, with its own clear flags (as the screen would composite them).
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude).Where(c => c.isActiveAndEnabled && c != Rig.UiCamera).OrderBy(c => c.depth))
                c.Render();
            Rig.UiCamera.Render();
            var tex = Read(Rig.Target);
            var ui = Read(Rig.UiTarget);
            // UI over the scene. UI shaders blend alpha too (SrcAlpha, OneMinusSrcAlpha) onto a clear target: rgb holds
            // rgb x a and alpha holds a x a, so the coverage is sqrt(alpha).
            var scene = tex.GetPixels32();
            var over = ui.GetPixels32();
            for (int i = 0; i < scene.Length; i++)
            {
                float a = Mathf.Sqrt(over[i].a / 255f);
                scene[i] = new Color32((byte)Mathf.Min(255, over[i].r + scene[i].r * (1 - a)), (byte)Mathf.Min(255, over[i].g + scene[i].g * (1 - a)),
                    (byte)Mathf.Min(255, over[i].b + scene[i].b * (1 - a)), 255);
            }
            tex.SetPixels32(scene);
            tex.Apply();
            Object.Destroy(ui);
            Directory.CreateDirectory(OutDir);
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            result.Value = tex.GetPixels32();
            Object.Destroy(tex);
            Debug.Log($"[SevenSteps] Play capture {name}: {Path.Combine(OutDir, name + ".png")}");
        }

        static Texture2D Read(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
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
