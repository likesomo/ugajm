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
using Core = SevenSteps.Core;

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

        /// U5 ending (-ui5): the last stage won, the U5 clear's Next, the first and the last ending card.
        /// `unity.sh playcapture U5Ending`.
        [UnityTest]
        public IEnumerator U5Ending()
        {
            yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-level", "1");
            var game = Object.FindAnyObjectByType<GameController>();
            var flow = Services.Get<AppFlow>();
            int last = flow.StageCount - 1;
            flow.GoToGame(last);
            float t0 = Time.realtimeSinceStartup;
            while (game.LevelIndex != last || flow.State != AppState.Game)
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 20f, "the last stage");
                yield return null;
            }
            string path = LevelCatalog.PathOf(last);
            if (path.Length > 1) game.Board.Replay(path.Substring(0, path.Length - 1));
            Assert.IsTrue(SevenSteps.Core.Rules.TryParseLetter(path[path.Length - 1], out var act));
            game.Board.Send(SevenSteps.Core.Command.FromAct(act));
            var clear = Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ClearView>(FindObjectsInactive.Include);
            t0 = Time.realtimeSinceStartup;
            while (flow.State != AppState.Result || !clear.IsShown)
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 20f, "the last clear");
                yield return null;
            }
            clear.Next.Press();
            yield return null;
            Assert.IsTrue(game.Ending.IsOpen, "story on: the ending cards");
            var px = new Pixels();
            yield return Capture("u5-ending-first", px, 0.5f);
            AssertVaried("u5-ending-first", px.Value);
            for (int guard = 0; !game.U5Ending.Map.Shown && guard < 10; guard++) { game.U5Ending.Next.Press(); yield return null; }
            px = new Pixels();
            yield return Capture("u5-ending-last", px, 0.5f);
            AssertVaried("u5-ending-last", px.Value);
        }

        /// U5 boot treatment (-ui5 -loadDelay 3): the Boot -> Title load behind the fader with the three scarf sparks.
        /// `unity.sh playcapture U5Boot`.
        [UnityTest]
        public IEnumerator U5Boot()
        {
            Rig.Ensure();
            Bootstrap.UseLaunchOptionsForNextRun(PlayEnv.Launch("-ui5", "-loadDelay", "3"));
            SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
            float t0 = Time.realtimeSinceStartup;
            GameObject sparks = null;
            while (sparks == null || sparks.GetComponent<CanvasGroup>().alpha < 0.5f)
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 10f, "the sparks to show");
                sparks = GameObject.Find("SceneFader/Scarf sparks");
                yield return null;
            }
            var px = new Pixels();
            yield return Capture("u5-boot", px, 0.4f);
            yield return PlayEnv.WaitForState(AppState.Title, AppScenes.Title, 15f);
        }

        /// U5 map and planet surface (-ui5; back 48 and settings 48 only): the first planet cleared, then its surface path.
        /// Before = `map` (current controls). `unity.sh playcapture U5Map`.
        [UnityTest]
        public IEnumerator U5Map()
        {
            yield return Start(AppState.Title, AppScenes.Title, "-ui5");
            var save = Services.Get<SaveSystem>();
            var title = Object.FindAnyObjectByType<SevenSteps.Game.UI.TitleScreen>();
            var levels = LevelCatalog.Load(title.assets);
            for (int s = 0; s < levels.Length && LevelCatalog.ChapterKeyOf(s) == LevelCatalog.ChapterKeyOf(0); s++)
                save.Progress.MarkCleared(levels[s], levels[s].Bodies, 3);
            title.U5Title.Map.Press();
            yield return PlayEnv.WaitForState(AppState.StageSelect, AppScenes.Title);
            Assert.IsTrue(title.U5Map.Visible, "the U5 map controls");
            var px = new Pixels();
            yield return Capture("u5-map", px);
            AssertVaried("u5-map", px.Value);
            // A cleared planet opens its surface path.
            GameObject.Find("TitleUI/PlanetMap3D/Planet 1").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            float t0 = Time.realtimeSinceStartup;
            while (GameObject.Find("TitleUI/PlanetSurface") == null)
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 10f, "the surface path");
                yield return null;
            }
            Assert.IsTrue(title.U5Map.Visible, "the same controls over the surface");
            px = new Pixels();
            yield return Capture("u5-surface", px);
            AssertVaried("u5-surface", px.Value);
        }

        /// S2 surfaces (user choice 2026-11-12): every planet's surface with the shipped GameAssets.planetSurfaceArt (Art's
        /// PlanetSurfaceSet, 91446f1), all stages cleared so every planet opens: the body carries its painting and the old
        /// v4 details are gone (crust only). `surface-<key>` per planet; before = `u5-surface`. `unity.sh playcapture SurfaceS2`.
        [UnityTest]
        public IEnumerator SurfaceS2()
        {
            yield return Start(AppState.Title, AppScenes.Title, "-ui5");
            var save = Services.Get<SaveSystem>();
            var title = Object.FindAnyObjectByType<SevenSteps.Game.UI.TitleScreen>();
            Assert.IsNotNull(title.assets.PlanetSurfaceArt, "GameAssets.planetSurfaceArt is set (Art's PlanetSurfaceSet)");
            var levels = LevelCatalog.Load(title.assets);
            foreach (var level in levels) save.Progress.MarkCleared(level, level.Bodies, 3);
            title.U5Title.Map.Press();
            yield return PlayEnv.WaitForState(AppState.StageSelect, AppScenes.Title);
            int planets = title.StageSelect.Model.PlanetCount;
            for (int p = 0; p < planets; p++)
            {
                GameObject.Find($"TitleUI/PlanetMap3D/Planet {p + 1}").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                float t0 = Time.realtimeSinceStartup;
                while (GameObject.Find("TitleUI/PlanetSurface") == null)
                {
                    Assert.Less(Time.realtimeSinceStartup - t0, 10f, $"planet {p + 1}'s surface");
                    yield return null;
                }
                var body = Object.FindObjectsByType<SevenSteps.Game.UI.Planets.PlanetBody>(FindObjectsInactive.Exclude).Single(b => b.transform.parent != null && b.transform.parent.name == "Surface");
                Assert.IsTrue(body.Projected, $"the surface body {body.Key} carries its painting");
                Assert.IsFalse(body.Seated, $"the projected {body.Key} surface uses the full round mesh so painted landmarks are not sheared by the board seat cut");
                Assert.IsTrue(body.Renderer.enabled && body.Impostor == null, "the body itself draws, no sprite");
                Assert.AreEqual(SevenSteps.Game.UI.Planets.PlanetStoneMesh.CrustTriangles * 3, body.GetComponent<MeshFilter>().sharedMesh.triangles.Length, "crust only: no old outline or details under the painting");
                int others = Object.FindObjectsByType<SevenSteps.Game.UI.Planets.PlanetBody>(FindObjectsInactive.Exclude).Count(b => b != body && b.Renderer.enabled && b.gameObject.activeInHierarchy && b.Renderer.isVisible);
                Debug.Log($"[SevenSteps] Play capture surface-{body.Key}: other planet bodies drawn = {others}");
                var px = new Pixels();
                yield return Capture("surface-" + body.Key, px);
                AssertVaried("surface-" + body.Key, px.Value);
                title.U5Map.Back.Press();
                t0 = Time.realtimeSinceStartup;
                while (GameObject.Find("TitleUI/PlanetSurface") != null)
                {
                    Assert.Less(Time.realtimeSinceStartup - t0, 10f, "back to the map");
                    yield return null;
                }
                yield return null;
            }
        }

        /// The U5 surface constellation (D2b Orbit sparks, user choice 2026-11-12): moss with nothing cleared, frost part way
        /// (stages 1-4 cleared with 0/1/2/3 stars, a stage other than the current focused), altar every stage at three stars
        /// (perfect), frost again in English. `surface-d2b-<planet>-<state>-<lang>.png`. `unity.sh playcapture U5Surface`.
        [UnityTest]
        public IEnumerator U5Surface([Values("moss:none:ko", "frost:partial:ko", "altar:perfect:ko", "frost:partial:en")] string shot)
        {
            var parts = shot.Split(':');
            string key = parts[0], state = parts[1], lang = parts[2];
            yield return Start(AppState.Title, AppScenes.Title, "-ui5", "-lang", lang);
            var save = Services.Get<SaveSystem>();
            var title = Object.FindAnyObjectByType<SevenSteps.Game.UI.TitleScreen>();
            var levels = LevelCatalog.Load(title.assets);
            var planets = SevenSteps.Game.UI.PlanetEntry.FromCatalog(levels.Length);
            int p = planets.FindIndex(e => e.Key == key);
            Assert.GreaterOrEqual(p, 0, key);
            var stages = planets[p].Stages;
            if (state == "partial") for (int s = 0; s < 4; s++)
                {
                    var level = levels[stages[s]];
                    if (s == 0) save.Progress.MarkCleared(level, level.Bodies); // cleared with no stars
                    else save.Progress.MarkCleared(level, level.Bodies, s);
                }
            if (state == "perfect") foreach (int s in stages) save.Progress.MarkCleared(levels[s], levels[s].Bodies, 3);
            // The planets before are completed (finale cleared): otherwise this one is locked (2026-11-14) and does not open.
            for (int q = 0; q < p; q++) { var fin = levels[planets[q].Stages[planets[q].Stages.Count - 1]]; save.Progress.MarkCleared(fin, fin.Bodies, 3); }
            title.U5Title.Map.Press();
            yield return PlayEnv.WaitForState(AppState.StageSelect, AppScenes.Title);
            GameObject.Find($"TitleUI/PlanetMap3D/Planet {p + 1}").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            float t0 = Time.realtimeSinceStartup;
            while (GameObject.Find("TitleUI/PlanetSurface") == null)
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 10f, "the surface");
                yield return null;
            }
            var stars = title.SurfaceView.Constellation;
            Assert.IsNotNull(stars, "-ui5: the constellation");
            Assert.AreEqual(stages.Count, stars.Count);
            if (state == "partial")
            {
                Assert.AreEqual("cleared 0", stars.State(0));
                Assert.AreEqual("cleared 3", stars.State(3));
                Assert.AreEqual("untouched current", stars.State(4));
                title.SurfaceView.Preview(2); // focus a stage other than the current one
            }
            if (state == "perfect") Assert.IsTrue(stars.State(0).EndsWith("perfect"), stars.State(0));
            if (state == "none") Assert.AreEqual("untouched current", stars.State(0));
            var plate = GameObject.Find("TitleUI/PlanetSurface/Planet plate"); // Find with a path also returns inactive children
            Assert.IsTrue(plate == null || !plate.activeInHierarchy, "no planet plate: back + settings only");
            Assert.IsTrue(title.U5Map.Visible);
            Debug.Log($"[SevenSteps] Play capture surface-d2b-{key}-{state}-{lang}: caption \"{title.SurfaceView.Step.text}\"");
            var px = new Pixels();
            yield return Capture($"surface-d2b-{key}-{state}-{lang}", px);
            AssertVaried($"surface-d2b-{key}-{state}-{lang}", px.Value);
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

        /// The journey after the clear (-ui5 -newmap, stage 1): the U5 clear first ("journey-clear"; the journey never skips
        /// the result), then its Next runs the advance, frozen part-way: the JourneyRunner is disabled after about 0.45 s and
        /// 0.9 s of its 1.5 s, so the camera, the risen board and the cleared board stay where they are for the capture.
        [UnityTest]
        public IEnumerator AdvanceMidway()
        {
            yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-newmap", "-level", "1");
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
            var flow = Services.Get<AppFlow>();
            float r0 = Time.realtimeSinceStartup;
            while (flow.State != AppState.Result)
            {
                Assert.Less(Time.realtimeSinceStartup - r0, 20f, "the clear");
                yield return null;
            }
            var shot = new Pixels();
            yield return Capture("journey-clear", shot);
            AssertVaried("journey-clear", shot.Value);
            var clear = Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ClearView>(FindObjectsInactive.Include);
            Assert.IsTrue(clear != null && clear.IsShown, "the U5 clear is up before the advance");
            clear.Next.Press(); // the journey from the clear's Next
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
            // r2-38-u5-en: the wake tags in English too (their KO overlap on 38, lead 2026-11-13).
            foreach (var (stage, replay, u5, en) in new[] { ("38", "DUDUDUR", false, false), ("38", "DUDUDUR", true, false), ("38", "DUDUDUR", true, true),
                         ("14", "LLLRRRR", false, false), ("14", "LLLRRRR", true, false) })
            {
                PlayEnv.Reset();
                var args = new List<string>();
                if (u5) args.Add("-ui5");
                if (en) args.AddRange(new[] { "-lang", "en" });
                args.AddRange(new[] { "-level", stage, "-replay", replay });
                yield return Start(AppState.Game, AppScenes.Game, args.ToArray());
                var game = Object.FindAnyObjectByType<GameController>();
                float t0 = Time.realtimeSinceStartup;
                while (!game.Board.Choice.Shown)
                {
                    Assert.Less(Time.realtimeSinceStartup - t0, 10f, "the 7th step's choice");
                    yield return null;
                }
                string name = $"r2-{stage}-{(u5 ? "u5" : "u4")}{(en ? "-en" : "")}";
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

        /// U5 clear before / after (docs/ui-u5-kit.md 3, U5-result): a won stage, toggle off ("u4-") and -ui5 ("u5-"), KO and
        /// EN, a planet's last stage (its farewell line) and the last stage (journey line, the ending). Waits for the result
        /// and the star reveal. `unity.sh playcapture U5Clear`.
        [UnityTest]
        public IEnumerator U5Clear()
        {
            foreach (var (name, args) in new (string, string[])[]
            {
                ("u4-clear", new[] { "-level", "4", "-replay", "DURRRRRKDDRRRRR" }),
                ("u5-clear", new[] { "-ui5", "-level", "4", "-replay", "DURRRRRKDDRRRRR" }),
                ("u5-clear-en", new[] { "-ui5", "-lang", "en", "-level", "4", "-replay", "DURRRRRKDDRRRRR" }),
                ("u5-clear-planet", new[] { "-ui5", "-level", "7", "-replay", "DUDUDULKDUDURRRKDUDDDDR" }),
                ("u5-clear-last", new[] { "-ui5", "-level", "50", "-replay", "UUUDURUKDRRRUUKDRRDLUU" }),
                // World star stones on a narrow (7, tall) and a wide (14, 17 x 3) board, both UIs.
                ("u4-clear-narrow", new[] { "-level", "7", "-replay", "DUDUDULKDUDURRRKDUDDDDR" }),
                ("u4-clear-wide", new[] { "-level", "14", "-replay", "LLLRRRRXRRRRRRRKLLLLLLL" }),
                ("u5-clear-wide", new[] { "-ui5", "-level", "14", "-replay", "LLLRRRRXRRRRRRRKLLLLLLL" }),
            })
            {
                PlayEnv.Reset();
                // The replay snaps (no presentation, no result): all but the last step, then the winning step is played.
                int ri = Array.IndexOf(args, "-replay") + 1;
                string path = args[ri];
                args[ri] = path.Substring(0, path.Length - 1);
                yield return Start(AppState.Game, AppScenes.Game, args);
                var game = Object.FindAnyObjectByType<GameController>();
                float tw = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - tw < 0.5f) yield return null;
                Assert.IsTrue(SevenSteps.Core.Rules.TryParseLetter(path[path.Length - 1], out var act));
                game.Board.Send(SevenSteps.Core.Command.FromAct(act));
                float t0 = Time.realtimeSinceStartup;
                while (Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ClearView>() is { IsShown: true } == false
                       && Object.FindAnyObjectByType<SevenSteps.Game.UI.ResultView>() is { IsShown: true } == false)
                {
                    Assert.Less(Time.realtimeSinceStartup - t0, 30f, $"{name}: the clear to show");
                    yield return null;
                }
                float t1 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t1 < 1.6f) yield return null; // the star reveal
                var px = new Pixels();
                yield return Capture(name, px);
                AssertVaried(name, px.Value);
                var u5 = Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ClearView>();
                if (name.StartsWith("u5")) Assert.IsTrue(u5 != null && u5.IsShown, $"{name}: the U5 clear is up");
                if (u5 != null)
                    foreach (var w in u5.WorldStoneObjects)
                    {
                        var rs = w != null ? w.GetComponentsInChildren<Renderer>() : new Renderer[0];
                        Debug.Log($"[SevenSteps] Play capture {name}: world stones {u5.WorldStones}, stone {(w == null ? "null" : $"{w.activeInHierarchy} at {w.transform.position} scale {w.transform.lossyScale} renderers {string.Join("+", System.Linq.Enumerable.Select(rs, r => r.name + ":" + (r.sharedMaterial != null ? r.sharedMaterial.name : "-")))} bounds {(rs.Length > 0 ? rs[0].bounds.ToString() : "-")} mat {(rs.Length > 0 && rs[0].sharedMaterial != null ? rs[0].sharedMaterial.shader.name : "-")}")}");
                    }
                else Assert.IsNull(u5, $"{name}: no U5 clear with the toggle off");
            }
        }

        /// U5 pause, settings and failure before / after (U5-pause, U5-settings, U5-failure): the menu opened from the HUD
        /// (u4: the current menu panel; u5: the icon row), the settings (u4: -settings; u5: the row's settings icon) and a
        /// loss shown by its view (u4: the current loss panel; u5: the failure line). `unity.sh playcapture U5Rows`.
        [UnityTest]
        public IEnumerator U5Rows()
        {
            foreach (bool u5 in new[] { false, true })
            {
                string ui = u5 ? "u5" : "u4";
                PlayEnv.Reset();
                yield return Start(AppState.Game, AppScenes.Game, u5 ? new[] { "-ui5", "-level", "4", "-replay", "D" } : new[] { "-level", "4", "-replay", "D" });
                var game = Object.FindAnyObjectByType<GameController>();
                game.Menu.Open();
                var px = new Pixels();
                yield return Capture($"{ui}-pause", px);
                AssertVaried($"{ui}-pause", px.Value);
                if (u5)
                {
                    var pause = Object.FindObjectsByType<SevenSteps.Game.UI.U5.U5RowOverlay>(FindObjectsInactive.Include).First(o => o.name == "U5 Pause");
                    Assert.IsTrue(pause.Visible, "the U5 pause row is the menu");
                    pause.Row.Buttons[4].Press(); // settings
                    px = new Pixels();
                    yield return Capture("u5-settings", px);
                    AssertVaried("u5-settings", px.Value);
                    Assert.IsTrue(Object.FindObjectsByType<SevenSteps.Game.UI.U5.U5RowOverlay>(FindObjectsInactive.Include).First(o => o.name == "U5 Settings").Visible);
                    pause.Plate.Model.Clear();
                }
                game.Menu.Toggle();
                PlayEnv.Reset();
                yield return Start(AppState.Game, AppScenes.Game, u5 ? new[] { "-ui5", "-level", "4", "-replay", "DUR" } : new[] { "-settings", "-level", "4", "-replay", "DUR" });
                if (!u5)
                {
                    px = new Pixels();
                    yield return Capture("u4-settings", px);
                    AssertVaried("u4-settings", px.Value);
                    game = Object.FindAnyObjectByType<GameController>();
                    game.Menu.Toggle(); // nothing: settings are open; Esc path is the overlay stack's
                }
                // A loss, shown by the result view the flow uses (the board stays as the replay left it).
                var loss = SevenSteps.Game.UI.ResultModel.Loss(3, 3, false);
                if (u5) Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ClearView>(FindObjectsInactive.Include).Show(loss);
                else
                {
                    foreach (var o in Object.FindObjectsByType<SevenSteps.Game.UI.SettingsPanel>(FindObjectsInactive.Exclude)) o.Hide();
                    Object.FindAnyObjectByType<SevenSteps.Game.UI.ResultView>().Show(loss);
                }
                px = new Pixels();
                yield return Capture($"{ui}-failure", px);
                AssertVaried($"{ui}-failure", px.Value);
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

        /// U5 chapter card (lead 2026-11-12): entering a planet from the board shows the planet name and its line only.
        /// Opening a stage directly enters its chapter, so the card is up right after the load. KO moss, KO + EN frost, and
        /// the -ui4 card for comparison. `unity.sh playcapture U5ChapterCard` -> u5-chapter-*.png, u4-chapter-frost.png.
        [UnityTest]
        public IEnumerator U5ChapterCard()
        {
            foreach (var (name, args, u5) in new (string, string[], bool)[]
            {
                ("u5-chapter-moss", new[] { "-ui5", "-level", "1" }, true),
                ("u5-chapter-frost", new[] { "-ui5", "-level", "22" }, true),
                ("u5-chapter-frost-en", new[] { "-ui5", "-lang", "en", "-level", "22" }, true),
                ("u4-chapter-frost", new[] { "-ui4", "-level", "22" }, false),
            })
            {
                PlayEnv.Reset();
                yield return Start(AppState.Game, AppScenes.Game, args);
                var card = Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ChapterCardView>(FindObjectsInactive.Include);
                if (u5)
                {
                    Assert.IsNotNull(card, name + ": U5 builds the U5 card");
                    float t0 = Time.realtimeSinceStartup;
                    while (!card.Visible)
                    {
                        Assert.Less(Time.realtimeSinceStartup - t0, 10f, name + ": the card on chapter entry");
                        yield return null;
                    }
                    card.Show(); // restart its 2.5 s so the capture sees it
                }
                else Assert.IsNull(card, name + ": -ui4 keeps the old card");
                var px = new Pixels();
                yield return Capture(name, px, 0.5f);
                AssertVaried(name, px.Value);
            }
        }

        /// Storybook viewer (design/story/cutscenes.md 2; gameplay-ui): a fresh save's play stone opens the prologue, with the
        /// Codex pictures (style A, watercolour; shared-assets). Pages 1, 4, 5 (KEEP), 5b (SWITCH) and 6 with every line out
        /// and their rule caption (v2, 2026-11-13), in KO and EN, after the epilogue played on the same viewer. `unity.sh playcapture U5Cutscene` -> u5-cutscene-*.png.
        [UnityTest]
        public IEnumerator U5Cutscene()
        {
            foreach (var lang in new[] { "ko", "en" })
            {
                PlayEnv.Reset();
                yield return Start(AppState.Title, AppScenes.Title, "-ui5", "-lang", lang);
                var title = Object.FindAnyObjectByType<SevenSteps.Game.UI.TitleScreen>();
                var c = title.Cutscene;
                var v = title.CutsceneView;
                var px = new Pixels();
                // The epilogue (played after the ending cards in the Game scene) on this viewer first: same view, its own picture.
                var epilogue = SevenSteps.Game.CutsceneLoad.Catalog(title.assets).SceneFor(CutsceneCatalog.WhenAfterEnding);
                c.Begin(epilogue, _ => { });
                while (c.Shown < c.Frame.Lines.Count) v.Next.Press();
                Assert.IsTrue(v.HasPicture, lang + " epilogue: its picture");
                yield return Capture($"u5-cutscene-epilogue-{lang}", px, 0.5f);
                AssertVaried($"u5-cutscene-epilogue-{lang}", px.Value);
                c.Skip();
                yield return null;
                title.U5Title.Play.Press();
                Assert.IsTrue(c.IsOpen && v.Visible, lang + ": the prologue on a new game");
                var pages = c.Scene.pages;
                foreach (string id in new[] { "1", "4", "5", "5b", "6" })
                {
                    int at = System.Array.FindIndex(pages, p => p.id == id);
                    Assert.GreaterOrEqual(at, 0, "prologue page " + id);
                    while (c.Page < at || c.Shown < c.Frame.Lines.Count) v.Next.Press();
                    Assert.IsTrue(v.HasPicture, $"{lang} p{id}: its picture, not the plain card");
                    Assert.AreEqual(CutsceneCatalog.HasRule(pages[at]), v.RuleVisible, $"{lang} p{id}: the rule caption with the last line");
                    string name = $"u5-cutscene-p{id}-{lang}";
                    yield return Capture(name, px, 0.5f);
                    AssertVaried(name, px.Value);
                }
                v.Skip.Press();
                yield return PlayEnv.WaitForState(AppState.Game, AppScenes.Game, 20f);
            }
        }

        /// Sends move / KEEP / SWITCH letters through the board as a player would, each after the last one is presented.
        static IEnumerator SendLetters(GameController game, string letters)
        {
            foreach (char c in letters)
            {
                while (game.Board.Session.IsLocked) yield return null;
                Assert.IsTrue(SevenSteps.Core.Rules.TryParseLetter(c, out var act), c.ToString());
                game.Board.Send(SevenSteps.Core.Command.FromAct(act));
                yield return null;
            }
            while (game.Board.Session.IsLocked) yield return null;
        }

        /// Tutorial cues (docs/tutorial.md 2.3; gameplay-ui): T1's start line, T1 at its 6th step (the line and the exit's
        /// ring) and T2's start (its ring on the plate), T3's choose screens (wake points) and wake highlights, KO and EN. `unity.sh playcapture U5Tutorial` -> u5-tutorial-*.png.
        [UnityTest]
        public IEnumerator U5Tutorial()
        {
            foreach (var lang in new[] { "ko", "en" })
            {
                PlayEnv.Reset();
                yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", lang, "-tutorial", "1");
                var game = Object.FindAnyObjectByType<GameController>();
                var px = new Pixels();
                Assert.IsTrue(game.TutorialCue.Visible && game.TutorialCue.Line.Length > 0, lang + ": T1's start cue");
                yield return Capture($"u5-tutorial-t1-start-{lang}", px, 0.8f);
                AssertVaried($"u5-tutorial-t1-start-{lang}", px.Value);
                foreach (char c in "RRRDDL") // six steps: the 7th-step cue and the exit's ring
                {
                    while (game.Board.Session.IsLocked) yield return null;
                    Assert.IsTrue(SevenSteps.Core.Rules.TryParseLetter(c, out var act));
                    game.Board.Send(SevenSteps.Core.Command.FromAct(act));
                    yield return null;
                }
                while (game.Board.Session.IsLocked) yield return null;
                Assert.AreEqual(6, game.Board.State.Used, "six steps");
                yield return Capture($"u5-tutorial-t1-step6-{lang}", px, 0.5f);
                AssertVaried($"u5-tutorial-t1-step6-{lang}", px.Value);
                PlayEnv.Reset();
                yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", lang, "-tutorial", "2");
                game = Object.FindAnyObjectByType<GameController>();
                yield return Capture($"u5-tutorial-t2-start-{lang}", px, 0.8f);
                AssertVaried($"u5-tutorial-t2-start-{lang}", px.Value);
                // T3 (KEEP then SWITCH, core f5bb105): the choose screen's wake points for each choice, and the wake
                // highlight after it (user request 2026-11-13).
                PlayEnv.Reset();
                yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", lang, "-tutorial", "3");
                game = Object.FindAnyObjectByType<GameController>();
                var marks = game.Board.View.Marks;
                string path = SevenSteps.Game.LevelCatalog.Tutorials[2].level.expected.path;
                int k = path.IndexOf('K'), x = path.IndexOf('X');
                Assert.IsTrue(k > 0 && x > k, "T3 needs KEEP then SWITCH: " + path);
                yield return SendLetters(game, path.Substring(0, k));
                while (!game.Board.Choice.Shown) yield return null;
                Assert.IsTrue(marks.WakeTagsShown && marks.KeepWakeShown, lang + ": both wake points tagged");
                yield return Capture($"u5-tutorial-t3-choose-keep-{lang}", px, 0.6f);
                AssertVaried($"u5-tutorial-t3-choose-keep-{lang}", px.Value);
                yield return SendLetters(game, "K");
                Assert.AreEqual(2, marks.WakeRingsShown, lang + ": the wake cell and the new stone");
                yield return Capture($"u5-tutorial-t3-wake-keep-{lang}", px, 0.25f);
                yield return SendLetters(game, path.Substring(k + 1, x - k - 1));
                while (!game.Board.Choice.Shown) yield return null;
                game.Board.Choice.Focus(false);
                Assert.IsTrue(marks.WakeTagsShown, lang + ": SWITCH focused, both still tagged");
                yield return Capture($"u5-tutorial-t3-choose-switch-{lang}", px, 0.6f);
                AssertVaried($"u5-tutorial-t3-choose-switch-{lang}", px.Value);
                yield return SendLetters(game, "X");
                Assert.AreEqual(1, marks.WakeRingsShown, lang + ": SWITCH: the wake cell only");
                yield return Capture($"u5-tutorial-t3-wake-switch-{lang}", px, 0.25f);
            }
        }

        /// itch.io page screenshots (lead 2026-11-13), EN, U5, 1280x720, from the current levels.json (stages picked by
        /// replaying each stage's solver path, so new content keeps working): shot-3-seventh-step (an early stage the moment
        /// the 7th step lands: lucky ring, 7 stars lit, the choice), shot-5-three-brothers (a later 3-brother stage, the first
        /// brother a stone on a plate, the second walking), shot-6-result-3stars (a 3-star clear). `unity.sh playcapture ItchShots`.
        [UnityTest]
        public IEnumerator ItchShots()
        {
            // 1) The 7th step: the first early stage whose first brother walks 7 plain steps into the choice.
            yield return Start(AppState.Title, AppScenes.Title, "-ui5", "-lang", "en");
            int count = LevelCatalog.Load(FindAssets()).Length;
            int seventh = -1;
            for (int i = 3; i < Math.Min(count, 12) && seventh < 0; i++)
            {
                var rs = Walk(i);
                if (rs.Count >= 7 && rs[6].State.Status == Core.Status.Choose && LevelCatalog.PathOf(i).Substring(0, 7).All(c => "UDLR".IndexOf(c) >= 0)) seventh = i;
            }
            Assert.GreaterOrEqual(seventh, 0, "an early stage with a 7-step first walk");
            yield return ShotAfterLive("shot-3-seventh-step", seventh, LevelCatalog.PathOf(seventh).Substring(0, 7), 0.55f);
            {
                // 4) The same choice with KEEP focused: its tip says what KEEP does.
                var g = Object.FindAnyObjectByType<GameController>();
                g.Board.Choice.Focus(true);
                var es = UnityEngine.EventSystems.EventSystem.current;
                g.U5Hud.Keep.OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(es)); // hover: the KEEP tip
                var p4 = new Pixels();
                yield return Capture("shot-4-keep-switch", p4, 0.6f);
                AssertVaried("shot-4-keep-switch", p4.Value);
            }

            // 1) Title, 2) mid-game map, 7) prologue page 6, 8) a planet interlude (c1ff970 content, 2026-11-14 re-take).
            PlayEnv.Reset();
            yield return Start(AppState.Title, AppScenes.Title, "-ui5", "-lang", "en");
            {
                var p1 = new Pixels();
                yield return Capture("shot-1-title", p1, 2f);
                AssertVaried("shot-1-title", p1.Value);
                var title = Object.FindAnyObjectByType<SevenSteps.Game.UI.TitleScreen>();
                var save = Services.Get<SaveSystem>();
                var levels = LevelCatalog.Load(title.assets);
                var planets = SevenSteps.Game.UI.PlanetEntry.FromCatalog(levels.Length);
                // Mid-game: moss, hollow, carved completed (3 stars), frost under way (4 of 7); joinery on stays locked.
                for (int q = 0; q < 3; q++) foreach (int s in planets[q].Stages) save.Progress.MarkCleared(levels[s], levels[s].Bodies, 3);
                for (int k = 0; k < 4; k++) { var l = levels[planets[3].Stages[k]]; save.Progress.MarkCleared(l, l.Bodies, k == 1 ? 2 : 3); }
                save.Progress.SetLastStage(levels[planets[3].Stages[4]]);
                title.U5Title.Map.Press();
                yield return PlayEnv.WaitForState(AppState.StageSelect, AppScenes.Title);
                Assert.IsTrue(title.StageSelect.Model.Locked(4), "joinery locked in the mid-game map");
                var p2 = new Pixels();
                yield return Capture("shot-2-map", p2, 2f);
                AssertVaried("shot-2-map", p2.Value);
                var catalog = SevenSteps.Game.CutsceneLoad.Catalog(title.assets);
                foreach (var (name, scene, pageId) in new[] { ("shot-7-cutscene", catalog.Scene("prologue"), "6"), ("shot-8-interlude", catalog.Interlude("hollow"), "2") })
                {
                    int at = Array.FindIndex(scene.pages, p => p.id == pageId);
                    Assert.GreaterOrEqual(at, 0, name);
                    title.Cutscene.Begin(scene, _ => { });
                    while (title.Cutscene.Page < at || title.Cutscene.Shown < title.Cutscene.Frame.Lines.Count) title.CutsceneView.Next.Press();
                    Assert.IsTrue(title.CutsceneView.HasPicture, name + ": its picture");
                    var pc = new Pixels();
                    yield return Capture(name, pc, 0.6f);
                    AssertVaried(name, pc.Value);
                    title.Cutscene.Skip();
                    yield return null;
                }
            }

            // 2) Three brothers: a later stage (from 20) with 3+ brothers whose first KEEP puts a stone on a plate, then 3 steps.
            int three = -1, cut = 0;
            for (int i = 19; i < count && three < 0; i++)
            {
                if (LevelCatalog.At(i).Bodies < 3) continue;
                var path = LevelCatalog.PathOf(i);
                var rs = Walk(i);
                for (int k = 0; k + 3 < rs.Count; k++)
                {
                    if (path[k] != 'K') continue;
                    bool plate = rs[k].Events.Any(e => e.Type == Core.GameEventType.Stone && e.At >= 0 && Plate(Core.Rules.TileAt(rs[k].State, e.At)));
                    bool walks = path.Substring(k + 1, 3).All(c => "UDLR".IndexOf(c) >= 0) && rs[k + 3].State.Status == Core.Status.Play;
                    if (plate && walks) { three = i; cut = k + 4; }
                    break; // only the first KEEP
                }
            }
            Assert.GreaterOrEqual(three, 0, "a later 3-brother stage whose first KEEP stands on a plate");
            yield return ShotAfterLive("shot-5-three-brothers", three, LevelCatalog.PathOf(three).Substring(0, cut), 0.12f);

            // 3) Three stars: the first stage from 4 whose solver path uses every brother.
            int full = -1;
            for (int i = 3; i < count && full < 0; i++)
            {
                var rs = Walk(i);
                var last = rs.Count > 0 ? rs[rs.Count - 1].State : null;
                if (last != null && last.Status == Core.Status.Win && Core.Stars.BodiesUsed(last) == last.Level.Bodies && last.Level.Bodies >= 2) full = i;
            }
            Assert.GreaterOrEqual(full, 0, "a stage whose path uses every brother");
            string fp = LevelCatalog.PathOf(full);
            PlayEnv.Reset();
            yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", "en", "-level", (full + 1).ToString(), "-replay", fp.Substring(0, fp.Length - 1));
            var game = Object.FindAnyObjectByType<GameController>();
            float tw = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tw < 0.5f) yield return null;
            yield return SendLetters(game, fp.Substring(fp.Length - 1));
            float t0 = Time.realtimeSinceStartup;
            while (Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ClearView>() is { IsShown: true } == false)
            {
                Assert.Less(Time.realtimeSinceStartup - t0, 30f, "the clear to show");
                yield return null;
            }
            float t1 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t1 < 2.4f) yield return null; // three stars revealed
            var px = new Pixels();
            yield return Capture("shot-6-result-3stars", px);
            AssertVaried("shot-6-result-3stars", px.Value);
            Debug.Log($"[SevenSteps] Play capture itch: shot-3 stage {seventh + 1}, shot-5 stage {three + 1} ({cut} moves), shot-6 stage {full + 1} ({fp})");
        }

        static GameAssets FindAssets() => Resources.FindObjectsOfTypeAll<GameAssets>().FirstOrDefault();

        static bool Plate(char t) => t >= 'a' && t <= 'c';

        /// Each move's result along stage <paramref name="stage"/>'s solver path (pure Core, no scene).
        static List<Core.StepResult> Walk(int stage)
        {
            var list = new List<Core.StepResult>();
            var s = Core.Rules.NewState(LevelCatalog.At(stage));
            foreach (char c in LevelCatalog.PathOf(stage))
            {
                if (!Core.Rules.TryParseLetter(c, out var act)) break;
                var r = Core.Rules.Dispatch(s, Core.Command.FromAct(act));
                list.Add(r);
                s = r.State;
            }
            return list;
        }

        /// Stage <paramref name="stage"/> (0-based) replayed to all but the last of <paramref name="moves"/>, the last played
        /// live (its presentation runs), then a capture <paramref name="settle"/> s later.
        IEnumerator ShotAfterLive(string name, int stage, string moves, float settle)
        {
            PlayEnv.Reset();
            var args = new List<string> { "-ui5", "-lang", "en", "-level", (stage + 1).ToString() };
            if (moves.Length > 1) args.AddRange(new[] { "-replay", moves.Substring(0, moves.Length - 1) });
            yield return Start(AppState.Game, AppScenes.Game, args.ToArray());
            var game = Object.FindAnyObjectByType<GameController>();
            float tw = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tw < 0.8f) yield return null;
            while (game.Board.Session.IsLocked) yield return null;
            Assert.IsTrue(Core.Rules.TryParseLetter(moves[moves.Length - 1], out var act));
            game.Board.Send(Core.Command.FromAct(act));
            var px = new Pixels();
            yield return Capture(name, px, settle);
            AssertVaried(name, px.Value);
        }

        /// Settings "처음부터 다시" (2026-11-14), KO and EN: the title settings with the reset button, the confirm panel, then
        /// [처음부터 다시] erases a played save (prologue seen, a last stage) and New Game plays the prologue page 1 again.
        /// The same reset from the in-game settings (stage 4) goes back to the title. `unity.sh playcapture ResetProgress`.
        [UnityTest]
        public IEnumerator ResetProgress()
        {
            foreach (string lang in new[] { "ko", "en" })
            {
                PlayEnv.Reset();
                yield return Start(AppState.Title, AppScenes.Title, "-ui5", "-lang", lang, "-settings");
                var save = Services.Get<SaveSystem>();
                save.Progress.lastStage = save.Progress.lastStage.Length > 0 ? save.Progress.lastStage : "played";
                save.SaveProgress();
                save.MarkTutorialSeen(TutorialIds.Prologue);
                var panel = Object.FindAnyObjectByType<SevenSteps.Game.UI.SettingsPanel>();
                Assert.IsNotNull(panel, "-settings: the settings panel is open");
                Assert.IsTrue(panel.ResetButton.gameObject.activeInHierarchy, "the reset button");
                var px = new Pixels();
                yield return Capture($"reset-settings-{lang}", px);
                AssertVaried($"reset-settings-{lang}", px.Value);
                AssertInside(panel.ResetButton.transform as RectTransform, $"reset button {lang}");
                panel.ResetButton.onClick.Invoke();
                Assert.IsTrue(panel.ResetConfirmOpen, "the confirm panel");
                yield return Capture($"reset-confirm-{lang}", px, 0.5f);
                AssertVaried($"reset-confirm-{lang}", px.Value);
                AssertInside(panel.ResetYes.transform as RectTransform, $"confirm yes {lang}");
                AssertInside(panel.ResetNo.transform as RectTransform, $"confirm no {lang}");
                panel.ResetNo.onClick.Invoke();
                Assert.IsFalse(panel.ResetConfirmOpen, "Cancel closes the confirm only");
                Assert.IsTrue(panel.gameObject.activeSelf, "settings stay open after Cancel");
                Assert.IsTrue(save.TutorialSeen(TutorialIds.Prologue), "Cancel erased nothing");
                panel.ResetButton.onClick.Invoke();
                panel.ResetYes.onClick.Invoke();
                yield return null;
                Assert.IsFalse(panel.gameObject.activeSelf, "settings closed after the reset");
                Assert.IsFalse(save.TutorialSeen(TutorialIds.Prologue), "prologue seen erased");
                Assert.AreEqual("", save.Progress.lastStage, "last stage erased");
                Assert.AreEqual(lang == "en" ? Language.En : Language.Ko, Services.Get<Localizer>().Current, "language kept");
                var title = Object.FindAnyObjectByType<SevenSteps.Game.UI.TitleScreen>();
                yield return Capture($"reset-title-{lang}", px, 0.8f);
                title.U5Title.Play.Press();
                var c = title.Cutscene;
                Assert.IsTrue(c.IsOpen, lang + ": New Game after the reset plays the prologue");
                Assert.AreEqual(0, c.Page, "prologue page 1");
                yield return Capture($"reset-prologue-p1-{lang}", px, 0.5f);
                AssertVaried($"reset-prologue-p1-{lang}", px.Value);
            }
            // In-game: stage 4 settings -> reset -> the title.
            PlayEnv.Reset();
            yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-level", "4", "-settings");
            var gamePanel = Object.FindAnyObjectByType<SevenSteps.Game.UI.SettingsPanel>();
            Assert.IsNotNull(gamePanel, "in-game settings open");
            gamePanel.ResetButton.onClick.Invoke();
            var shot = new Pixels();
            yield return Capture("reset-confirm-game-ko", shot, 0.8f);
            AssertVaried("reset-confirm-game-ko", shot.Value);
            gamePanel.ResetYes.onClick.Invoke();
            yield return PlayEnv.WaitForState(AppState.Title, AppScenes.Title, 20f);
        }

        /// No legal move (stage 44 "진흙과 숫자", core.js BFS: UULLD = 6 steps, mud and walls all round): the HUD line says Undo /
        /// Restart (msg.stuck), KO and EN. The last step is sent live so the message presenter sees it. `unity.sh playcapture Stuck`.
        [UnityTest]
        public IEnumerator Stuck()
        {
            foreach (string lang in new[] { "ko", "en" })
            {
                PlayEnv.Reset();
                yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", lang, "-level", "44", "-replay", "UULL");
                var game = Object.FindAnyObjectByType<GameController>();
                float tw = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - tw < 0.8f) yield return null;
                yield return SendLetters(game, "D");
                Assert.IsTrue(Core.Rules.IsStuck(game.Board.State), "UULLD on stage 44 leaves no legal move");
                string want = lang == "ko" ? "더 디딜 곳이 없다. 되돌리기(Z)나 다시 하기(R)로." : "Nowhere left to step. Undo (Z) or restart (R).";
                Assert.AreEqual(want, game.Board.MessageLine, lang + " stuck line");
                var px = new Pixels();
                yield return Capture($"stuck-44-{lang}", px, 1f);
                AssertVaried($"stuck-44-{lang}", px.Value);
                yield return SendLetters(game, "R"); // a bump keeps the line
                Assert.AreEqual(want, game.Board.MessageLine, lang + " stuck line after a bump");
            }
        }

        /// Locked planets (2026-11-14), KO and EN: a fresh save (only moss open; a click on hollow shows map.locked and does
        /// not dive), the middle (moss, hollow, carved completed: frost open, joinery on locked) and everything done.
        /// `unity.sh playcapture MapLock` -> map-lock-{fresh,middle,done}-{ko,en}.png.
        [UnityTest]
        public IEnumerator MapLock()
        {
            foreach (string lang in new[] { "ko", "en" })
                foreach (string state in new[] { "fresh", "middle", "done" })
                {
                    PlayEnv.Reset();
                    yield return Start(AppState.Title, AppScenes.Title, "-ui5", "-lang", lang);
                    var save = Services.Get<SaveSystem>();
                    var title = Object.FindAnyObjectByType<SevenSteps.Game.UI.TitleScreen>();
                    var levels = LevelCatalog.Load(title.assets);
                    var planets = SevenSteps.Game.UI.PlanetEntry.FromCatalog(levels.Length);
                    int completed = state == "fresh" ? 0 : state == "middle" ? 3 : planets.Count;
                    for (int q = 0; q < completed; q++)
                        foreach (int s in planets[q].Stages) save.Progress.MarkCleared(levels[s], levels[s].Bodies, 3);
                    title.U5Title.Map.Press();
                    yield return PlayEnv.WaitForState(AppState.StageSelect, AppScenes.Title);
                    var model = title.StageSelect.Model;
                    for (int p = 0; p < planets.Count; p++)
                        Assert.AreEqual(p > 0 && p > completed, model.Locked(p), $"{state}: planet {p} locked");
                    int locked = completed + 1;
                    if (locked < planets.Count)
                    {
                        GameObject.Find($"TitleUI/PlanetMap3D/Planet {locked + 1}").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                        yield return null;
                        var map = title.StageViews.Map;
                        Assert.AreEqual(locked, map.LabelShown, "a locked planet's click shows its label");
                        Assert.AreEqual(AppState.StageSelect, Services.Get<AppFlow>().State, "no dive into a locked planet");
                    }
                    var px = new Pixels();
                    yield return Capture($"map-lock-{state}-{lang}", px, 1.2f);
                    AssertVaried($"map-lock-{state}-{lang}", px.Value);
                }
        }

        /// Planet interlude (2026-11-14): moss's finale (stage 7) won live on a fresh save, the clear's Next plays
        /// interlude.moss once (page 1 captured KO / EN), then the journey goes on; the seen flag is saved and a reset clears
        /// it. `unity.sh playcapture Interlude` -> interlude-moss-p1-{ko,en}.png.
        [UnityTest]
        public IEnumerator Interlude()
        {
            foreach (string lang in new[] { "ko", "en" })
            {
                PlayEnv.Reset();
                yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", lang, "-level", "7");
                var game = Object.FindAnyObjectByType<GameController>();
                var flow = Services.Get<AppFlow>();
                var save = Services.Get<SaveSystem>();
                string seen = CutsceneCatalog.InterludeSeenId("moss");
                Assert.AreEqual(0, save.Story.Count(seen), "fresh save");
                float tw = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - tw < 1.5f) yield return null; // chapter card
                while (Object.FindAnyObjectByType<GameController>().Board.Session.IsLocked) yield return null;
                var sol = Core.Solver.Solve(game.Board.State.Level);
                Assert.IsNotNull(sol, "stage 7 solvable");
                yield return SendLetters(game, sol.Letters());
                float t0 = Time.realtimeSinceStartup;
                while (flow.State != AppState.Result) { Assert.Less(Time.realtimeSinceStartup - t0, 30f, "the clear"); yield return null; }
                t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 2.5f) yield return null; // the star trail finale
                game.Flow.Next();
                var c = game.Cutscene;
                Assert.IsNotNull(c, "a cutscene player in the Game scene");
                Assert.IsTrue(c.IsOpen, lang + ": Next after moss's finale plays its interlude");
                Assert.AreEqual("interlude.moss", c.Scene.id);
                Assert.AreEqual(1, save.Story.Count(seen), "marked seen");
                Assert.AreEqual(AppState.Result, flow.State, "the journey waits for the interlude");
                var px = new Pixels();
                while (c.Shown < c.Frame.Lines.Count) game.CutsceneView.Next.Press(); // page 1 with its origin caption
                yield return Capture($"interlude-moss-p1-{lang}", px, 0.5f);
                AssertVaried($"interlude-moss-p1-{lang}", px.Value);
                Assert.IsTrue(game.CutsceneView.HasPicture, "moss page 1 (picture moss.2) has its Codex picture");
                while (c.Page < 1 || c.Shown < c.Frame.Lines.Count) game.CutsceneView.Next.Press(); // page 2 (picture moss.1)
                Assert.IsTrue(game.CutsceneView.HasPicture, "moss page 2 (picture moss.1)");
                yield return Capture($"interlude-moss-p2-{lang}", px, 0.5f);
                AssertVaried($"interlude-moss-p2-{lang}", px.Value);
                game.CutsceneView.Skip.Press();
                yield return null;
                Assert.IsFalse(c.IsOpen, "skipped");
                Assert.IsTrue(game.Flow.Leaving || flow.State != AppState.Result, "the journey goes on after the interlude");
                Assert.IsFalse(game.Flow.Interlude(6, () => { }), "once only");
                save.ResetProgress();
                Assert.AreEqual(0, save.Story.Count(seen), "a reset brings the interlude back");
                yield return new WaitForSecondsRealtime(3f);
            }
        }

        /// Interlude pages played on the title's viewer (same view as the Game scene): the first page of each interlude with
        /// its picture, KO / EN; since art batch 2 every page of every scene. `unity.sh playcapture InterludePages` -> cutscene-<scene>-<page>-{ko,en}.png.
        [UnityTest]
        public IEnumerator InterludePages()
        {
            foreach (string lang in new[] { "ko", "en" })
            {
                PlayEnv.Reset();
                yield return Start(AppState.Title, AppScenes.Title, "-ui5", "-lang", lang);
                var title = Object.FindAnyObjectByType<SevenSteps.Game.UI.TitleScreen>();
                var catalog = SevenSteps.Game.CutsceneLoad.Catalog(title.assets);
                var c = title.Cutscene;
                var v = title.CutsceneView;
                foreach (var scene in catalog.Scenes) // every page of every scene (prologue, 7 interludes, epilogue): 30
                {
                    c.Begin(scene, _ => { });
                    for (int i = 0; i < scene.pages.Length; i++)
                    {
                        while (c.Page < i || c.Shown < c.Frame.Lines.Count) v.Next.Press();
                        string name = $"cutscene-{scene.id}-{scene.pages[i].id}-{lang}";
                        Assert.IsTrue(v.HasPicture, name + ": its picture");
                        var px = new Pixels();
                        yield return Capture(name, px, 0.3f);
                        AssertVaried(name, px.Value);
                    }
                    c.Skip();
                    yield return null;
                }
            }
        }

        /// Planet arrivals (2026-11-14): entering a planet's first stage in a run plays arrival.<planet> once, instead of the
        /// chapter card (a direct -level launch skips its own first board). Moss (stage 1) and frost (stage 22), KO / EN.
        /// `unity.sh playcapture Arrival` -> arrival-{moss,frost}-{ko,en}.png.
        [UnityTest]
        public IEnumerator Arrival()
        {
            foreach (string lang in new[] { "ko", "en" })
            {
                PlayEnv.Reset();
                yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", lang, "-level", "2");
                var game = Object.FindAnyObjectByType<GameController>();
                var flow = Services.Get<AppFlow>();
                var save = Services.Get<SaveSystem>();
                Assert.IsFalse(game.Cutscene != null && game.Cutscene.IsOpen, "a direct launch starts on its board");
                foreach (var (planet, stage) in new[] { ("moss", 0), ("frost", 21) })
                {
                    while (game.Overlays.Count > 0) game.Overlays.Pop();
                    while (flow.IsLoading || game.Board.Session.IsLocked) yield return null;
                    flow.GoToGame(stage);
                    float t0 = Time.realtimeSinceStartup;
                    while (!(game.Cutscene != null && game.Cutscene.IsOpen)) { Assert.Less(Time.realtimeSinceStartup - t0, 10f, planet + ": the arrival"); yield return null; }
                    Assert.AreEqual("arrival." + planet, game.Cutscene.Scene.id);
                    Assert.AreEqual(1, save.Story.Count(CutsceneCatalog.ArrivalSeenId(planet)), "marked seen");
                    Assert.AreEqual(1, game.Overlays.Count, planet + ": no chapter card under the arrival");
                    while (game.Cutscene.Shown < game.Cutscene.Frame.Lines.Count) game.CutsceneView.Next.Press();
                    Assert.IsTrue(game.CutsceneView.HasPicture, planet + ": the reused interlude picture");
                    var px = new Pixels();
                    yield return Capture($"arrival-{planet}-{lang}", px, 0.6f);
                    AssertVaried($"arrival-{planet}-{lang}", px.Value);
                    game.CutsceneView.Next.Press(); // the end
                    yield return null;
                    Assert.IsFalse(game.Cutscene.IsOpen, "one page");
                }
                // Once: the same first stage again plays nothing.
                flow.GoToGame(0);
                for (int f = 0; f < 30; f++) yield return null;
                Assert.IsFalse(game.Cutscene.IsOpen, "the moss arrival plays once");
            }
        }

        /// Hints by tries (2026-11-14), stage 4, KO / EN: hidden on a fresh try (H says how many more), a restart from a
        /// played state counts, the popup before the first hint, tier 1 (the nudge), tier 3 (the route) and the hinted
        /// clear's result line. `unity.sh playcapture Hints` -> hint-{locked,popup,tier1,tier3,result}-{ko,en}.png.
        [UnityTest]
        public IEnumerator Hints()
        {
            foreach (string lang in new[] { "ko", "en" })
            {
                PlayEnv.Reset();
                yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", lang, "-level", "4");
                var game = Object.FindAnyObjectByType<GameController>();
                var board = game.Board;
                var save = Services.Get<SaveSystem>();
                var level = board.State.Level;
                while (game.Overlays.Count > 0) game.Overlays.Pop();
                Assert.AreEqual(0, board.HintsUnlocked);
                Assert.IsFalse(game.U5Hud.Hint.gameObject.activeSelf, "hidden before 3 tries");
                board.ShowHint();
                StringAssert.Contains(lang == "ko" ? "3번 더" : "3 more", board.MessageLine);
                var px = new Pixels();
                yield return Capture($"hint-locked-{lang}", px, 0.6f);
                // A restart from a played state counts one try.
                yield return SendLetters(game, "D");
                board.Restart();
                yield return null;
                Assert.AreEqual(1, save.Progress.Tries(level), "a restart counts");
                save.Progress.CountTry(level); save.Progress.CountTry(level); // to 3
                yield return SendLetters(game, "D");
                board.Restart(); // the 4th try: tier 1 is open (and the line says so)
                yield return null;
                Assert.AreEqual(1, board.HintsUnlocked);
                Assert.IsTrue(game.U5Hud.Hint.gameObject.activeSelf, "the hint button shows from tier 1");
                board.ShowHint();
                Assert.IsTrue(game.HintConfirm.Visible, "the popup before the first hint of the try");
                Assert.AreEqual(lang == "ko" ? "힌트 1/3" : "Hint 1/3", game.HintConfirm.TitleText);
                Assert.AreEqual(0, board.Attempt.Hints, "nothing opened before the confirm");
                yield return Capture($"hint-popup-{lang}", px, 0.6f);
                AssertVaried($"hint-popup-{lang}", px.Value);
                AssertInside(game.HintConfirm.Box, "hint popup " + lang);
                game.HintConfirm.CancelButton.onClick.Invoke();
                Assert.IsFalse(game.HintConfirm.Visible);
                Assert.AreEqual(0, board.Attempt.Hints, "Cancel opens nothing");
                board.ShowHint();
                game.HintConfirm.ShowButton.onClick.Invoke();
                Assert.AreEqual(1, board.Attempt.Hints);
                StringAssert.Contains(lang == "ko" ? "힌트 1/3" : "Hint 1/3", board.MessageLine);
                yield return Capture($"hint-tier1-{lang}", px, 0.8f);
                AssertVaried($"hint-tier1-{lang}", px.Value);
                for (int k = 0; k < 6; k++) save.Progress.CountTry(level); // 9 tries: every tier
                board.ShowHint(); // tier 2, no popup on the same try
                Assert.IsFalse(game.HintConfirm.Visible, "later tiers of the same try open at once");
                board.ShowHint(); // tier 3
                StringAssert.Contains(lang == "ko" ? "힌트 3/3" : "Hint 3/3", board.MessageLine);
                yield return Capture($"hint-tier3-{lang}", px, 0.8f);
                AssertVaried($"hint-tier3-{lang}", px.Value);
                // The hinted clear: 1 star and the "without a hint" line.
                board.Restart();
                yield return null;
                yield return SendLetters(game, LevelCatalog.PathOf(3));
                float t0 = Time.realtimeSinceStartup;
                var clear = Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ClearView>();
                while (clear == null || !clear.IsShown) { Assert.Less(Time.realtimeSinceStartup - t0, 30f, "the clear"); yield return null; clear = Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ClearView>(); }
                StringAssert.Contains(lang == "ko" ? "힌트 없이" : "without a hint", clear.NudgeLine);
                yield return Capture($"hint-result-{lang}", px, 2.4f);
                AssertVaried($"hint-result-{lang}", px.Value);
            }
        }

        /// Final QA 2026-11-14 (H1): stages 1 and 43 after 9 tries, tiers 1/2/3, KO / EN: three brothers named, never "0",
        /// no exception (an error log fails the test). `unity.sh playcapture QaHintStages` -> qa-hint-s{1,43}-t{1,2,3}-{ko,en}.png.
        [UnityTest]
        public IEnumerator QaHintStages()
        {
            foreach (string lang in new[] { "ko", "en" })
                foreach (int stage in new[] { 1, 43 })
                {
                    PlayEnv.Reset();
                    yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", lang, "-level", stage.ToString());
                    var game = Object.FindAnyObjectByType<GameController>();
                    var board = game.Board;
                    var save = Services.Get<SaveSystem>();
                    while (game.Overlays.Count > 0) game.Overlays.Pop();
                    for (int k = 0; k < 9; k++) save.Progress.CountTry(board.State.Level);
                    board.Restart();
                    yield return null;
                    Assert.AreEqual(3, board.HintsUnlocked, $"stage {stage}: every tier after 9 tries");
                    var px = new Pixels();
                    for (int tier = 1; tier <= 3; tier++)
                    {
                        board.ShowHint();
                        if (game.HintConfirm.Visible) game.HintConfirm.ShowButton.onClick.Invoke();
                        string line = board.MessageLine;
                        StringAssert.Contains(lang == "ko" ? $"힌트 {tier}/3" : $"Hint {tier}/3", line, $"stage {stage} tier {tier}");
                        Assert.IsFalse(line.Contains("0명") || line.Contains("walk: 0") || line.Contains(". ."), $"stage {stage} tier {tier}: {line}");
                        if (tier == 1) StringAssert.Contains(lang == "ko" ? "형제 3명" : "walk: 3", line, $"stage {stage}: three brothers");
                        yield return Capture($"qa-hint-s{stage}-t{tier}-{lang}", px, 0.6f);
                        AssertVaried($"qa-hint-s{stage}-t{tier}-{lang}", px.Value);
                    }
                }
        }

        /// Stage story arc (2026-11-14): stage 2's start line (the steps given away), KO / EN, on the HUD line after the card.
        /// `unity.sh playcapture StoryArc` -> arc-stage2-{ko,en}.png.
        [UnityTest]
        public IEnumerator StoryArc()
        {
            foreach (string lang in new[] { "ko", "en" })
            {
                PlayEnv.Reset();
                yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", lang, "-level", "2");
                var game = Object.FindAnyObjectByType<GameController>();
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 1f) yield return null;
                while (game.Overlays.Count > 0) game.Overlays.Pop(); // the chapter card: the start line follows it
                string want = lang == "ko" ? "내 걸음을 내주어야" : "We give our steps";
                t0 = Time.realtimeSinceStartup;
                while (!(game.U5StoryLine != null && game.U5StoryLine.Text.Contains(want))) // the U5 story line plate (on screen)
                {
                    Assert.Less(Time.realtimeSinceStartup - t0, 8f, lang + ": stage 2's start line on screen");
                    yield return null;
                }
                var px = new Pixels();
                yield return Capture($"arc-stage2-{lang}", px, 0.3f);
                AssertVaried($"arc-stage2-{lang}", px.Value);
            }
        }

        /// Every story screen in play order (user request 2026-11-14), KO or EN per run: title line, prologue, T1-T3 start
        /// cues, then per planet its arrival, each stage's start line, the finale line and the interlude pages, and at the end
        /// the ending cards and the epilogue. Files story-{lang}-NNN-<id>.png; one "[SevenSteps] STORYIDX" log line each.
        /// `unity.sh playcapture StoryScreens` (both languages; ~10 min).
        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator StoryScreens()
        {
            foreach (string lang in new[] { "ko", "en" })
            {
                int n = 0;
                var px = new Pixels();
                IEnumerator Shot(string id, string text)
                {
                    string name = $"story-{lang}-{++n:000}-{id}";
                    yield return Capture(name, px, 0.4f);
                    Debug.Log($"[SevenSteps] STORYIDX|{lang}|{name}.png|{id}|{(text ?? "").Replace("\n", " / ").Replace("|", "/")}");
                }
                var language = lang == "ko" ? Language.Ko : Language.En;
                PlayEnv.Reset();
                yield return Start(AppState.Title, AppScenes.Title, "-ui5", "-lang", lang);
                var title = Object.FindAnyObjectByType<SevenSteps.Game.UI.TitleScreen>();
                var catalog = SevenSteps.Game.CutsceneLoad.Catalog(title.assets);
                yield return Shot("title", Services.TryGet<SevenSteps.Game.Flow.StoryBook>(out var book) ? book.TitleLine(language) : "");
                IEnumerator Pages(SevenSteps.Game.Flow.CutsceneController c, SevenSteps.Game.UI.U5.U5CutsceneView v, CutsceneSceneDto scene)
                {
                    if (!c.IsOpen) c.Begin(scene, _ => { });
                    for (int i = 0; i < scene.pages.Length; i++)
                    {
                        while (c.Page < i || c.Shown < c.Frame.Lines.Count) v.Next.Press();
                        var p = scene.pages[i];
                        string rule = CutsceneCatalog.Rule(p, language);
                        yield return Shot($"{scene.id}-p{i + 1}", string.Join(" / ", CutsceneCatalog.Lines(p, language)) + (rule.Length > 0 ? " [caption] " + rule : ""));
                    }
                    try { if (c.IsOpen) c.Skip(); } catch (System.Exception) { } // the last scene may already be leaving
                    yield return null;
                }
                yield return Pages(title.Cutscene, title.CutsceneView, catalog.Scene("prologue"));
                // Tutorial boards: their start cues.
                var flow = Services.Get<AppFlow>();
                int boards = LevelCatalog.Tutorials.Count;
                flow.GoToTutorial(0, boards);
                yield return PlayEnv.WaitForState(AppState.Game, AppScenes.Game);
                var game = Object.FindAnyObjectByType<GameController>();
                for (int t = 0; t < boards; t++)
                {
                    if (t > 0) flow.GoToTutorial(t, boards);
                    float tw = Time.realtimeSinceStartup;
                    while (flow.IsLoading || !game.TutorialCue.Visible) { if (Time.realtimeSinceStartup - tw > 6f) break; yield return null; }
                    yield return Shot($"tutorial-t{t + 1}", game.TutorialCue.Visible ? game.TutorialCue.Line : "(no cue)");
                }
                // Stages, planet by planet: each planet starts fresh (a direct launch on its first stage; its arrival opened
                // here), the stages inside it advance in place, the finale is won for its line, its interlude follows.
                int count = flow.StageCount;
                var keys = Enumerable.Range(0, count).Select(LevelCatalog.ChapterKeyOf).ToArray();
                for (int s = 0; s < count; s++)
                {
                    string chapter = keys[s];
                    bool first = s == 0 || keys[s - 1] != chapter || s == count - 1;
                    if (first)
                    {
                        PlayEnv.Reset();
                        yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", lang, "-level", (s + 1).ToString());
                        game = Object.FindAnyObjectByType<GameController>();
                        flow = Services.Get<AppFlow>();
                        float tw = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - tw < 0.5f) yield return null;
                        while (game.Overlays.Count > 0) game.Overlays.Pop(); // the chapter card
                        var arrival = s == count - 1 ? null : catalog.Arrival(chapter);
                        if (arrival != null && (s == 0 || keys[s - 1] != chapter))
                        {
                            game.Cutscene.Begin(arrival, _ => { });
                            yield return Pages(game.Cutscene, game.CutsceneView, arrival);
                            // Covered and uncovered again: the start line shows anew.
                        }
                    }
                    else
                    {
                        while (flow.IsLoading) yield return null;
                        flow.GoToGame(s);
                        float t0b = Time.realtimeSinceStartup;
                        while (flow.IsLoading || game.LevelIndex != s || game.Board.Session.IsLocked) { Assert.Less(Time.realtimeSinceStartup - t0b, 20f, $"stage {s + 1}"); yield return null; }
                        while (game.Overlays.Count > 0) game.Overlays.Pop();
                    }
                    float t0 = Time.realtimeSinceStartup;
                    while (!game.U5StoryLine.LineShown && Time.realtimeSinceStartup - t0 < 3f) yield return null;
                    yield return Shot($"stage{s + 1:00}", game.U5StoryLine.LineShown ? game.U5StoryLine.Text : "(no start line)");
                    bool finale = SevenSteps.Contracts.StoryTruths.IsFinaleStage(keys, s);
                    bool last = s == count - 1;
                    if (!finale && !last) continue;
                    string startText = game.U5StoryLine.Text;
                    string path = LevelCatalog.PathOf(s);
                    if (path.Length > 1) game.Board.Replay(path.Substring(0, path.Length - 1));
                    Assert.IsTrue(Core.Rules.TryParseLetter(path[path.Length - 1], out var act));
                    game.Board.Send(Core.Command.FromAct(act));
                    if (finale)
                    {
                        t0 = Time.realtimeSinceStartup;
                        while (!(game.U5StoryLine.LineShown && game.U5StoryLine.Text != startText) && Time.realtimeSinceStartup - t0 < 12f) yield return null;
                        yield return Shot($"finale-{chapter}", game.U5StoryLine.LineShown ? game.U5StoryLine.Text : "(finale line not caught)");
                    }
                    var clear = Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ClearView>(FindObjectsInactive.Include);
                    t0 = Time.realtimeSinceStartup;
                    while (flow.State != AppState.Result || !clear.IsShown) { Assert.Less(Time.realtimeSinceStartup - t0, 30f, $"clear {s + 1}"); yield return null; }
                    if (finale)
                    {
                        if (game.Cutscene.IsOpen) game.Cutscene.Skip();
                        yield return null;
                        var inter = catalog.Interlude(chapter);
                        game.Cutscene.Begin(inter, _ => { });
                        yield return Pages(game.Cutscene, game.CutsceneView, inter);
                        continue;
                    }
                    clear.Next.Press();
                    yield return null;
                    Assert.IsTrue(game.Ending.IsOpen, "the ending cards");
                    for (int card = 1; card <= 6 && game.Ending.IsOpen && !game.U5Ending.Map.Shown; card++)
                    {
                        yield return Shot($"ending-card{card}", "(see story.json ending.*)");
                        game.U5Ending.Next.Press();
                        yield return null;
                    }
                    if (game.Ending.IsOpen) yield return Shot("ending-last", "(see story.json ending.4)");
                    while (game.Overlays.Count > 0) game.Overlays.Pop();
                    yield return null;
                    var epi = catalog.Scene("epilogue");
                    if (game.Cutscene.IsOpen) game.Cutscene.Skip();
                    game.Cutscene.Begin(epi, _ => { });
                    yield return Pages(game.Cutscene, game.CutsceneView, epi);
                }
            }
        }

        /// The three strands (2026-11-14), KO / EN: a 3-star clear mid-braid and braided, a 2-star clear with the missing
        /// strand dim, and the HUD's brother dots mid-stage. `unity.sh playcapture Strands` -> strands-*.png.
        [UnityTest]
        public IEnumerator Strands()
        {
            yield return Start(AppState.Title, AppScenes.Title, "-ui5"); // GameAssets loaded
            int count = LevelCatalog.Load(FindAssets()).Length;
            int full = -1, partial = -1;
            for (int i = 3; i < count && (full < 0 || partial < 0); i++)
            {
                var rs = Walk(i);
                var last = rs.Count > 0 ? rs[rs.Count - 1].State : null;
                if (last == null || last.Status != Core.Status.Win || last.Level.Bodies < 2) continue;
                if (full < 0 && Core.Stars.BodiesUsed(last) == last.Level.Bodies) full = i;
                if (partial < 0 && Core.Stars.BodiesUsed(last) < last.Level.Bodies) partial = i;
            }
            Assert.GreaterOrEqual(full, 0, "a stage whose route uses every brother");
            Assert.GreaterOrEqual(partial, 0, "a stage whose route leaves a brother");
            foreach (string lang in new[] { "ko", "en" })
                foreach (var (stage, tag) in new[] { (full, "3star"), (partial, "2star") })
                {
                    PlayEnv.Reset();
                    yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", lang, "-level", (stage + 1).ToString());
                    var game = Object.FindAnyObjectByType<GameController>();
                    while (game.Overlays.Count > 0) game.Overlays.Pop();
                    while (game.Board.Session.IsLocked) yield return null;
                    string path = LevelCatalog.PathOf(stage);
                    var px = new Pixels();
                    if (tag == "3star" && lang == "ko")
                    {
                        // The HUD's three brother dots mid-stage: the first brother done, the second walking.
                        int k = path.IndexOfAny(new[] { 'K', 'X' });
                        if (k > 0) { yield return SendLetters(game, path.Substring(0, k + 1)); yield return Capture("strands-hud-mid", px, 0.6f); }
                        game.Board.Restart();
                        yield return null;
                    }
                    if (path.Length > 1) game.Board.Replay(path.Substring(0, path.Length - 1));
                    yield return SendLetters(game, path.Substring(path.Length - 1));
                    var clear = Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ClearView>(FindObjectsInactive.Include);
                    float t0 = Time.realtimeSinceStartup;
                    while (clear == null || !clear.IsShown) { Assert.Less(Time.realtimeSinceStartup - t0, 30f, "the clear"); yield return null; clear = Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ClearView>(FindObjectsInactive.Include); }
                    if (tag == "3star")
                    {
                        Assert.IsFalse(clear.Strands.gameObject.activeSelf, "no strand ribbon (removed 2026-11-14)");
                        t0 = Time.realtimeSinceStartup;
                        while (clear.LitStones < 3 && Time.realtimeSinceStartup - t0 < 5f) yield return null;
                        yield return Capture($"strands-3star-{lang}", px, 0.3f);
                        AssertVaried($"strands-3star-{lang}", px.Value);
                    }
                    else
                    {
                        Assert.IsFalse(clear.Strands.gameObject.activeSelf, "no strand ribbon (removed 2026-11-14)");
                        yield return Capture($"strands-2star-{lang}", px, 2f);
                        // Concept A: the same clear as a hinted one (1 star, the hint_again line, the ? still on the third stone).
                        int used = Core.Stars.BodiesUsed(game.Board.State);
                        clear.Show(SevenSteps.Game.UI.ResultModel.Clear(stage + 1, game.Board.State.Level.Bodies, used, used, false, stars: 1, bestStars: 1));
                        yield return Capture($"strands-hinted-{lang}", px, 1.5f);
                        AssertVaried($"strands-hinted-{lang}", px.Value);

                        AssertVaried($"strands-2star-{lang}", px.Value);
                    }
                }
        }

        /// The ending on its storybook pictures (2026-11-14), KO / EN: stage 50 won by its route, every ending card, then
        /// both epilogue pages. `unity.sh playcapture EndingPages` -> ending-{lang}-cardN.png, ending-{lang}-epilogue-pN.png.
        [UnityTest]
        public IEnumerator EndingPages()
        {
            foreach (string lang in new[] { "ko", "en" })
            {
                PlayEnv.Reset();
                yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", lang, "-level", "50");
                var game = Object.FindAnyObjectByType<GameController>();
                var flow = Services.Get<AppFlow>();
                while (game.Overlays.Count > 0) game.Overlays.Pop();
                while (game.Board.Session.IsLocked) yield return null;
                int last = flow.StageCount - 1;
                string path = LevelCatalog.PathOf(last);
                if (path.Length > 1) game.Board.Replay(path.Substring(0, path.Length - 1));
                yield return SendLetters(game, path.Substring(path.Length - 1));
                var clear = Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ClearView>(FindObjectsInactive.Include);
                float t0 = Time.realtimeSinceStartup;
                while (flow.State != AppState.Result || !clear.IsShown) { Assert.Less(Time.realtimeSinceStartup - t0, 30f, "the last clear"); yield return null; }
                clear.Next.Press();
                yield return null;
                Assert.IsTrue(game.Ending.IsOpen, "the ending cards");
                var px = new Pixels();
                for (int card = 1; card <= 4 && game.Ending.IsOpen; card++)
                {
                    yield return Capture($"ending-{lang}-card{card}", px, 0.5f);
                    AssertVaried($"ending-{lang}-card{card}", px.Value);
                    if (!game.U5Ending.Map.Shown) { game.U5Ending.Next.Press(); yield return null; }
                }
                // The last card's Map ends the ending (read through): the game plays the epilogue itself.
                game.U5Ending.Map.Press();
                t0 = Time.realtimeSinceStartup;
                while (!(game.Cutscene.IsOpen && game.Cutscene.Scene.id == "epilogue")) { Assert.Less(Time.realtimeSinceStartup - t0, 10f, "the epilogue after the ending"); yield return null; }
                var epi = game.Cutscene.Scene;
                for (int i = 0; i < epi.pages.Length; i++)
                {
                    for (int guard = 0; guard < 20 && game.Cutscene.IsOpen && (game.Cutscene.Page < i || game.Cutscene.Shown < game.Cutscene.Frame.Lines.Count); guard++)
                    {
                        game.CutsceneView.Next.Press();
                        yield return null;
                    }
                    Assert.IsTrue(game.Cutscene.IsOpen && game.Cutscene.Page == i, "epilogue page " + (i + 1));
                    Assert.IsTrue(game.CutsceneView.HasPicture, "epilogue page " + (i + 1));
                    yield return Capture($"ending-{lang}-epilogue-p{i + 1}", px, 0.5f);
                    AssertVaried($"ending-{lang}-epilogue-p{i + 1}", px.Value);
                }
            }
        }

        /// itch shot-11 (2026-11-14): stage 43 with the new altar look, EN, its start line on screen. `unity.sh playcapture ItchAltar`.
        [UnityTest]
        public IEnumerator ItchAltar()
        {
            yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", "en", "-level", "43");
            var game = Object.FindAnyObjectByType<GameController>();
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1f) yield return null;
            while (game.Overlays.Count > 0) game.Overlays.Pop();
            t0 = Time.realtimeSinceStartup;
            while (!game.U5StoryLine.LineShown && Time.realtimeSinceStartup - t0 < 3f) yield return null;
            var px = new Pixels();
            yield return Capture("shot-11-altar", px, 0.6f);
            AssertVaried("shot-11-altar", px.Value);
        }

        /// Stage background check (user report 2026-11-14 "stage 2 background is a mess"): stages 1-3 on load, KO, the chapter
        /// card closed. `unity.sh playcapture StageCheck` -> stage2-check-{1,2,3}.png.
        [UnityTest]
        public IEnumerator StageCheck()
        {
            foreach (int stage in new[] { 43, 44, 50 })
            {
                PlayEnv.Reset();
                yield return Start(AppState.Game, AppScenes.Game, "-ui5", "-lang", "ko", "-level", stage.ToString());
                var game = Object.FindAnyObjectByType<GameController>();
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 1f) yield return null;
                while (game.Overlays.Count > 0) game.Overlays.Pop(); // the chapter card
                var px = new Pixels();
                yield return Capture($"stage2-check-{stage}", px, 1.2f);
                AssertVaried($"stage2-check-{stage}", px.Value);
            }
        }

        /// The rect lies inside the 1280x720 capture (no clipping at the WebGL / desktop reference size).
        static void AssertInside(RectTransform r, string what)
        {
            Assert.IsNotNull(r, what);
            var corners = new Vector3[4];
            r.GetWorldCorners(corners);
            var canvas = r.GetComponentInParent<Canvas>().rootCanvas;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var w in corners)
            {
                var p = RectTransformUtility.WorldToScreenPoint(cam, w);
                Assert.IsTrue(p.x >= -0.5f && p.x <= Width + 0.5f && p.y >= -0.5f && p.y <= Height + 0.5f, $"{what}: corner {p} outside {Width}x{Height}");
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
            /// Draws the converted canvases: still, far from the scene (y 10000), last. A canvas on a moving
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
                ui.cullingMask = ~0; // every layer: an overlay canvas draws whatever its layer (the SceneFader is on Default); nothing else is near y 10000
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
            var clear = Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ClearView>();
            if (clear != null && clear.IsShown) clear.Layout();
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
