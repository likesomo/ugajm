using System;
using System.Collections.Generic;
using System.IO;
using SevenSteps.Contracts;
using SevenSteps.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Play Mode capture (docs/scene-flow.md 9.2): screenshots of the real Game view in the Editor Play Mode, no player build.
    /// Run by `unity.sh playcapture` in a non-batchmode Editor (batchmode has no Game view; a camera RenderTexture misses
    /// Screen Space - Overlay UI). Each shot enters Play Mode on an empty scene, injects its launch options
    /// (Bootstrap.UseLaunchOptionsForNextRun: telemetry off, Korean, save key "playcapture"), loads Boot, waits for the flow
    /// state, waits a moment, captures at a fixed 1280x720 Game view (PlayModeWindow) and exits Play Mode.
    /// Checks: every shot is 1280x720 and not one flat colour; the game board shot (SmokeView.Board, magenta clear, UI hidden)
    /// has at least <see cref="BoardMin"/> non-magenta pixels in the board area, and its control (SmokeView.Empty) fewer.
    /// Logs "[SevenSteps] Play capture: OK|FAIL ..." and exits the Editor (0 / 1).
    /// </summary>
    public static class PlayCapture
    {
        const int Width = 1280, Height = 720;
        const int BoardMin = 500;
        const float SettleSeconds = 1.5f, Timeout = 30f;
        const string SaveKey = "playcapture";

        sealed class Shot
        {
            public string Name;
            public string[] Args;
            public AppState State;
            public string Scene;
        }

        static readonly Shot[] Shots =
        {
            new Shot { Name = "title", Args = new string[0], State = AppState.Title, Scene = AppScenes.Title },
            new Shot { Name = "game", Args = new[] { "-level", "1" }, State = AppState.Game, Scene = AppScenes.Game },
            new Shot { Name = "game-board", Args = new[] { "-level", "1", "-smoke", "board" }, State = AppState.Game, Scene = AppScenes.Game },
            new Shot { Name = "game-empty", Args = new[] { "-level", "1", "-smoke", "empty" }, State = AppState.Game, Scene = AppScenes.Game },
        };

        enum Step { Enter, Load, Wait, Settle, Capture, Written, Exit }

        static string outDir;
        static int index;
        static Step step;
        static double t0;
        static readonly List<string> fails = new List<string>();
        static readonly Dictionary<string, string> results = new Dictionary<string, string>();

        /// Batch entry: Unity.exe -projectPath unity -executeMethod SevenSteps.EditorTools.PlayCapture.Run -playcaptureOut &lt;dir&gt;
        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int k = Array.IndexOf(args, "-playcaptureOut");
            outDir = k >= 0 && k + 1 < args.Length ? args[k + 1] : Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/playcapture"));
            Directory.CreateDirectory(outDir);
            fails.Clear();
            results.Clear();
            index = 0;
            WipeSave();
            PlayModeWindow.SetCustomRenderingResolution(Width, Height, "playcapture");
            step = Step.Enter;
            EditorApplication.update += Tick;
        }

        [MenuItem("Seven Steps/Scene Flow/Play Mode Capture")]
        static void RunFromMenu() => Run();

        static void Tick()
        {
            if (index >= Shots.Length) { Finish(); return; }
            var shot = Shots[index];
            string file = Path.Combine(outDir, shot.Name + ".png");
            switch (step)
            {
                case Step.Enter:
                    if (EditorApplication.isPlaying) return;
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    if (File.Exists(file)) File.Delete(file);
                    EditorApplication.EnterPlaymode();
                    step = Step.Load;
                    return;
                case Step.Load:
                    if (!EditorApplication.isPlaying) return;
                    var list = new List<string> { "SevenSteps", "-telemetry", "off", "-lang", "ko", "-savekey", SaveKey };
                    list.AddRange(shot.Args);
                    Bootstrap.UseLaunchOptionsForNextRun(LaunchOptions.Parse(list.ToArray(), ""));
                    SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
                    t0 = EditorApplication.timeSinceStartup;
                    step = Step.Wait;
                    return;
                case Step.Wait:
                    if (Services.TryGet<AppFlow>(out var flow) && flow.State == shot.State && !flow.IsLoading && SceneManager.GetActiveScene().name == shot.Scene)
                    {
                        t0 = EditorApplication.timeSinceStartup;
                        step = Step.Settle;
                    }
                    else if (EditorApplication.timeSinceStartup - t0 > Timeout) Fail(shot, "timed out waiting for " + shot.State);
                    return;
                case Step.Settle:
                    if (EditorApplication.timeSinceStartup - t0 < SettleSeconds) return;
                    ScreenCapture.CaptureScreenshot(file); // written at the end of a rendered frame (Game view)
                    t0 = EditorApplication.timeSinceStartup;
                    step = Step.Written;
                    return;
                case Step.Written:
                    if (!File.Exists(file) || new FileInfo(file).Length == 0)
                    {
                        if (EditorApplication.timeSinceStartup - t0 > 10) Fail(shot, "no screenshot written");
                        return;
                    }
                    EditorApplication.ExitPlaymode();
                    step = Step.Exit;
                    return;
                case Step.Exit:
                    if (EditorApplication.isPlaying) return;
                    index++;
                    step = Step.Enter;
                    return;
            }
        }

        static void Fail(Shot shot, string why)
        {
            fails.Add(shot.Name + ": " + why);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            step = Step.Exit;
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            foreach (var shot in Shots) Check(shot);
            if (results.TryGetValue("game-board", out var b) && results.TryGetValue("game-empty", out var e))
            {
                int board = int.Parse(b), empty = int.Parse(e);
                if (board < BoardMin) fails.Add($"game-board: {board} board pixels (< {BoardMin})");
                if (empty >= BoardMin) fails.Add($"game-empty: control {empty} board pixels (>= {BoardMin}): the measurement is broken");
            }
            WipeSave();
            var parts = new List<string>();
            foreach (var kv in results) parts.Add(kv.Key + " " + kv.Value);
            Debug.Log(fails.Count == 0
                ? $"[SevenSteps] Play capture: OK ({string.Join(", ", parts)}) -> {outDir}"
                : $"[SevenSteps] Play capture: FAIL: {string.Join("; ", fails)} ({string.Join(", ", parts)})");
            if (Application.isBatchMode || Array.IndexOf(Environment.GetCommandLineArgs(), "-executeMethod") >= 0)
                EditorApplication.Exit(fails.Count == 0 ? 0 : 1);
        }

        /// Size, not one flat colour; for the smoke shots the non-magenta count in the board area (results).
        static void Check(Shot shot)
        {
            string file = Path.Combine(outDir, shot.Name + ".png");
            if (!File.Exists(file)) { if (!fails.Exists(f => f.StartsWith(shot.Name + ":"))) fails.Add(shot.Name + ": missing"); return; }
            var tex = new Texture2D(2, 2);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(file))) { fails.Add(shot.Name + ": unreadable png"); return; }
                if (tex.width != Width || tex.height != Height) fails.Add($"{shot.Name}: {tex.width}x{tex.height}, want {Width}x{Height}");
                var px = tex.GetPixels32();
                if (shot.Name.EndsWith("-board") || shot.Name.EndsWith("-empty"))
                    results[shot.Name] = BoardPixels(px, tex.width, tex.height).ToString();
                else
                {
                    int distinct = NotFirstColour(px);
                    results[shot.Name] = "varied " + distinct;
                    if (distinct < BoardMin) fails.Add($"{shot.Name}: almost one flat colour ({distinct} pixels differ)");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        /// Pixels in the board area (x 30-70 %, y 33-66 %) that are not the smoke clear #FF00FF (board-pixels.ps1 rule).
        static int BoardPixels(Color32[] px, int w, int h)
        {
            int n = 0;
            for (int y = h * 33 / 100; y < h * 66 / 100; y++)
                for (int x = w * 30 / 100; x < w * 70 / 100; x++)
                {
                    var c = px[y * w + x];
                    if (Math.Abs(c.r - 255) + Math.Abs(c.g - 0) + Math.Abs(c.b - 255) > 24) n++;
                }
            return n;
        }

        static int NotFirstColour(Color32[] px)
        {
            var c0 = px[0];
            int n = 0;
            foreach (var c in px) if (Math.Abs(c.r - c0.r) + Math.Abs(c.g - c0.g) + Math.Abs(c.b - c0.b) > 24) n++;
            return n;
        }

        static void WipeSave()
        {
            var store = new SuffixedStore(new PlayerPrefsStore(), SaveKey);
            foreach (var key in new[] { SaveSystem.ProgressKey, SaveSystem.SettingsKey, SaveSystem.ProgressBackupKey, SaveSystem.TutorialKey, SaveSystem.StoryKey })
                store.Delete(key);
            store.Flush();
        }
    }
}
