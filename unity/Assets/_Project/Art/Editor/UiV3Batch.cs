using System;
using System.IO;
using System.Collections.Generic;
using SevenSteps.Core;
using SevenSteps.Art.Review;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace SevenSteps.Art
{
    public static class UiV3Batch
    {
        [Serializable]
        public sealed class PreviewRow
        {
            public string language, planet; public int current, start, keepGhost, keepActual, keepStone, switchGhost, switchActual; public bool matches;
        }
        [Serializable]
        public sealed class PreviewProof
        {
            public int level = 4, width = 1280, height = 720, footStarMinPixels = 16; public float presentationSeconds = .38f; public List<PreviewRow> cases = new List<PreviewRow>();
        }
        static PreviewProof proof = new PreviewProof();
        static Keyboard keyboard;
        static string Output => Path.GetFullPath("../concepts/ui/minimal-2026-11-10/v3"); static ReviewChoiceController v; static Camera cam; static List<string> checks = new List<string>();
        static void Assert(bool ok, string name)
        {
            checks.Add(name + ": " + (ok ? "PASS" : "FAIL"));
            if (!ok)
                throw new Exception(name);
        }
        public static void Run()
        {
            try
            {
                Directory.CreateDirectory(Output);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                cam = new GameObject("Review camera", typeof(Camera)).GetComponent<Camera>();
                cam.tag = "MainCamera";
                cam.orthographic = true;
                cam.orthographicSize = 360;
                cam.transform.position = new Vector3(640, 360, -10);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.nearClipPlane = .1f;
                cam.farClipPlane = 100;
                var go = new GameObject("GameSession KEEP SWITCH review", typeof(RectTransform));
                v = go.AddComponent<ReviewChoiceController>();
                v.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Shared/Fonts/Jua-Regular.ttf");
                v.chapters = Array.ConvertAll(new[] { "Chapter_moss", "Chapter_hollow", "Chapter_carved", "Chapter_frost", "Chapter_joinery", "Chapter_crumble", "Chapter_altar" }, n => AssetDatabase.LoadAssetAtPath<SevenSteps.Contracts.ChapterPreset>("Assets/_Project/Art/RuinsHenge/" + n + ".asset"));
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/_Project/Art/Test/UiV3/ChoiceReview.unity");
                cam.targetTexture = new RenderTexture(1280, 720, 24);
                var dto = JsonUtility.FromJson<LevelFileDto>(File.ReadAllText("Assets/_Project/Data/levels.json"));
                v.Initialize(dto.levels[3].ToLevel());
                keyboard = InputSystem.AddDevice<Keyboard>();
                v.PollKeyboard();
                foreach (var lang in new[] { "ko", "en" })
                    foreach (var planet in new[] { "moss", "frost", "carved" })
                    {
                        v.language = lang;
                        v.planet = planet;
                        v.encounters = 0;
                        v.Session.Load(dto.levels[3].ToLevel());
                        Capture("normal");
                        string path = dto.levels[3].expected.path;
                        int k = path.IndexOf('K');
                        for (int i = 0; i < k; i++)
                        {
                            if (Rules.TryParseLetter(path[i], out var a))
                            {
                                v.Send(Command.FromAct(a));
                                v.Finish();
                            }
                            if (v.Session.State.Used == 6)
                                Capture("six-steps");
                        }
                        Assert(v.Session.State.Status == Status.Choose, "level4 choice event " + lang + planet);
                        v.focusKeep = true;
                        Capture("seventh-keep-preview");
                        var s = v.Session.State;
                        Assert(!v.boardRect.Overlaps(v.panel.Bounds), "UiLayoutCheck board-panel " + lang + planet);
                        var expectedKeep = Rules.Dispatch(s, Command.Keep).State;
                        var expectedSwitch = Rules.Dispatch(s, Command.Switch).State;
                        proof.cases.Add(new PreviewRow { language = lang, planet = planet, current = s.Pos, start = s.Start, keepGhost = v.ghosts.NextPosition(s, true), keepActual = expectedKeep.Pos, keepStone = Array.Find(expectedKeep.Statues, c => c == s.Pos), switchGhost = v.ghosts.NextPosition(s, false), switchActual = expectedSwitch.Pos, matches = v.ghosts.NextPosition(s, true) == expectedKeep.Pos && v.ghosts.NextPosition(s, false) == expectedSwitch.Pos && Array.IndexOf(expectedKeep.Statues, s.Pos) >= 0 });
                        Assert(v.ghosts.NextPosition(s, true) == expectedKeep.Pos, "KEEP ghost = result position");
                        Assert(v.ghosts.NextPosition(s, false) == expectedSwitch.Pos, "SWITCH ghost = result position");
                        Assert(Array.IndexOf(expectedKeep.Statues, s.Pos) >= 0, "KEEP ghost stone = result statue");
                        v.focusKeep = false;
                        Capture("switch-preview");
                        Press(Key.X);
                        Assert(v.Session.IsLocked, "presentation input lock");
                        Press(Key.K);
                        Assert(v.Session.QueuedCount == 0, "other input suppressed during presentation");
                        Capture("switch-seam");
                        v.Finish();
                        Assert(v.Session.State.Pos == expectedSwitch.Pos && v.Session.State.Start == s.Pos, "SWITCH input and state");
                        Capture("after-switch");
                        Press(Key.Z);
                        Assert(v.Session.State.Status == Status.Choose, "Undo restores choice");
                        Press(Key.Tab);
                        Assert(v.focusKeep, "keyboard focus changes preview");
                        v.focusKeep = true;
                        v.Draw();
                        var touchTarget = GameObject.Find("Switch choice");
                        var touch = new PointerEventData(EventSystem.current) { pointerId = 0, button = PointerEventData.InputButton.Left };
                        ExecuteEvents.Execute(touchTarget, touch, ExecuteEvents.pointerEnterHandler);
                        Assert(touchTarget == GameObject.Find("Switch choice"), "touch target survives focus redraw");
                        ExecuteEvents.Execute(touchTarget, touch, ExecuteEvents.pointerDownHandler);
                        ExecuteEvents.Execute(touchTarget, touch, ExecuteEvents.pointerUpHandler);
                        ExecuteEvents.Execute(touchTarget, touch, ExecuteEvents.pointerClickHandler);
                        v.Finish();
                        Assert(v.Session.State.Pos == expectedSwitch.Pos, "touch pointer SWITCH");
                        Press(Key.Z);
                        v.focusKeep = true;
                        v.Draw();
                        v.focusKeep = false;
                        v.Draw();
                        ExecuteEvents.Execute(GameObject.Find("Keep choice"), new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                        Assert(v.focusKeep, "mouse hover changes preview");
                        var target = GameObject.Find("Keep choice");
                        ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                        Assert(v.Session.State.Pos == expectedKeep.Pos, "mouse pointer KEEP");
                        Capture("keep-fixing");
                        v.Finish();
                        Capture("after-keep");
                        Press(Key.Z);
                        Capture("experienced-focus-keep");
                        v.Focus(false);
                        Capture("experienced-focus-switch");
                        v.Focus(true);
                        Press(Key.K);
                        v.Finish();
                        Assert(v.Session.State.Pos == expectedKeep.Pos, "K keyboard KEEP");
                        v.Send(Command.Undo);
                        v.Send(Command.Undo);
                        Assert(v.Session.State.Used == 6 && v.Session.State.Status == Status.Play, "Undo returns before seventh step");
                        var moves = GameObject.FindObjectsByType<Button>(FindObjectsSortMode.None);
                        var rightMove = Array.Find(moves, b => b.name == "Touch move" && ((RectTransform)b.transform).anchoredPosition.x > 1080);
                        ExecuteEvents.Execute(rightMove.gameObject, new PointerEventData(EventSystem.current) { pointerId = 0 }, ExecuteEvents.pointerDownHandler);
                        v.Finish();
                        Assert(v.Session.State.Status == Status.Choose, "touch seventh move from pulsing sixth");
                        v.Session.Load(dto.levels[3].ToLevel());
                        foreach (char c in path)
                        {
                            if (Rules.TryParseLetter(c, out var a))
                            {
                                v.Send(Command.FromAct(a));
                                v.Finish();
                            }
                        }
                        Assert(v.Session.State.Status == Status.Win, "actual session result");
                        Capture("result");
                    }
                foreach (var planet in new[] { "hollow", "joinery", "crumble", "altar" })
                {
                    v.planet = planet;
                    v.language = "ko";
                    v.Session.Load(dto.levels[3].ToLevel());
                    foreach (char c in "DURRRR")
                    {
                        Rules.TryParseLetter(c, out var a);
                        v.Send(Command.FromAct(a));
                        v.Finish();
                    }
                    Capture("six-steps");
                }
                var cmap = SevenSteps.EditorTools.GlyphCheck.ReadCmap(File.ReadAllBytes("Assets/Shared/Fonts/Jua-Regular.ttf"));
                foreach (string file in Directory.GetFiles("Assets/_Project/Art/Test/UiV3", "*.cs"))
                {
                    foreach (char c in File.ReadAllText(file))
                    {
                        if (c >= 0xAC00 && c <= 0xD7A3)
                            Assert(cmap.Contains(c), "Art GlyphCheck " + c);
                    }
                }
                var assets = AssetDatabase.LoadAssetAtPath<SevenSteps.Game.GameAssets>("Assets/_Project/Data/GameAssets.asset");
                var issues = new List<string>();
                Assert(SevenSteps.EditorTools.GlyphCheck.Check(assets.uiTheme, assets, issues) == 0, "production GlyphCheck");
                File.WriteAllLines(Path.Combine(Output, "glyph-check.txt"), issues);
                v.planet = "moss";
                v.language = "ko";
                v.Session.Load(dto.levels[0].ToLevel());
                foreach (char c in dto.levels[0].expected.path)
                {
                    if (Rules.TryParseLetter(c, out var a))
                    {
                        v.Send(Command.FromAct(a));
                        v.Finish();
                    }
                }
                Assert(v.Session.State.Status == Status.Win && !GameObject.Find("Keep choice"), "exit does not show choice");
                v.Session.Load(Level.Parse("pit fixture", "", 3, new[] { "###########", "#S......o.#", "###########" }));
                for (int i = 0; i < 7; i++)
                {
                    v.Send(Command.Move(Dir.Right));
                    v.Finish();
                }
                Assert(v.Session.State.Status != Status.Choose && !GameObject.Find("Keep choice"), "pit does not show choice");
                MakeSheet("choice-state-sheet.png", new[] { "ko-moss-normal.png", "ko-moss-six-steps.png", "ko-moss-seventh-keep-preview.png", "ko-moss-switch-preview.png", "ko-moss-after-keep.png", "ko-moss-after-switch.png", "ko-moss-result.png", "en-frost-seventh-keep-preview.png" }, 2);
                MakeSheet("seven-planet-foot-stars.png", Array.ConvertAll(new[] { "moss", "hollow", "carved", "frost", "joinery", "crumble", "altar" }, id => "ko-" + id + "-six-steps.png"), 2);
                File.Copy("C:/unityjam/shared-assets/seven-steps/concepts/ui/minimal-2026-11-10/v2/unity/moss-choice.png", Path.Combine(Output, "v2-unity-moss-choice.png"), true);
                MakeSheet("before-after.png", new[] { "v2-unity-moss-choice.png", "ko-moss-seventh-keep-preview.png" }, 2);
                File.WriteAllLines(Path.Combine(Output, "assertions.txt"), checks);
                File.WriteAllText(Path.Combine(Output, "preview-assertions.json"), JsonUtility.ToJson(proof, true));
                Debug.Log("UI_V3_CAPTURE_PASS " + checks.Count);
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); File.WriteAllLines(Path.Combine(Output, "assertions.txt"), checks); EditorApplication.Exit(1); }
        }
        public static void BuildWebGL()
        {
            var r = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions { scenes = new[] { "Assets/_Project/Art/Test/UiV3/ChoiceReview.unity" }, locationPathName = "../Builds/UiV3WebGL", target = UnityEditor.BuildTarget.WebGL, options = UnityEditor.BuildOptions.None, extraScriptingDefines = new[] { "SEVENSTEPS_ART_PREVIEW" } });
            File.WriteAllText(Path.Combine(Output, "webgl-summary.json"), "{\"result\":\"" + r.summary.result + "\",\"errors\":" + r.summary.totalErrors + ",\"bytes\":" + r.summary.totalSize + "}");
            EditorApplication.Exit(r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
        static void MakeSheet(string filename, string[] sources, int columns)
        {
            int rows = (sources.Length + columns - 1) / columns;
            var dst = new Texture2D(640 * columns, 360 * rows, TextureFormat.RGB24, false);
            for (int i = 0; i < sources.Length; i++)
            {
                var src = new Texture2D(2, 2);
                src.LoadImage(File.ReadAllBytes(Path.Combine(Output, sources[i])));
                for (int y = 0; y < 360; y++)
                    for (int x = 0; x < 640; x++)
                        dst.SetPixel((i % columns) * 640 + x, (rows - 1 - i / columns) * 360 + y, src.GetPixelBilinear(x / 640f, y / 360f));
                UnityEngine.Object.DestroyImmediate(src);
            }
            dst.Apply();
            File.WriteAllBytes(Path.Combine(Output, filename), dst.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(dst);
        }
        static void Press(Key key)
        {
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            v.PollKeyboard();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            v.PollKeyboard();
        }
        static void Capture(string name)
        {
            v.Draw();
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var old = RenderTexture.active;
            RenderTexture.active = cam.targetTexture;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Output, v.language + "-" + v.planet + "-" + name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            RenderTexture.active = old;
        }
    }
}
