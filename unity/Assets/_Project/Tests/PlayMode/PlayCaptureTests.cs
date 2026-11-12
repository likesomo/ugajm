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
        /// Codex pictures (style A, watercolour; shared-assets). Pages 1, 4 and 6 with every line out, in KO and EN, then the
        /// epilogue played on the same viewer. `unity.sh playcapture U5Cutscene` -> u5-cutscene-*.png.
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
                foreach (int page in new[] { 1, 4, 6 })
                {
                    while (c.Page < page - 1 || c.Shown < c.Frame.Lines.Count) v.Next.Press();
                    Assert.IsTrue(v.HasPicture, $"{lang} p{page}: its picture");
                    string name = $"u5-cutscene-p{page}-{lang}";
                    yield return Capture(name, px, 0.5f);
                    AssertVaried(name, px.Value);
                }
                v.Skip.Press();
                yield return PlayEnv.WaitForState(AppState.Game, AppScenes.Game, 20f);
            }
        }

        /// Tutorial cues (docs/tutorial.md 2.3; gameplay-ui): T1's start line, T1 at its 6th step (the line and the exit's
        /// ring) and T2's start (its ring on the plate), KO and EN. `unity.sh playcapture U5Tutorial` -> u5-tutorial-*.png.
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
