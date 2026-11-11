using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// Star trail before/after captures in the Editor Play Mode, headless (batchmode, no Editor window, no player build):
    /// each case boots the Game scene with -level / -replay, lets the replay play, renders the game camera into a
    /// 1280x720 texture and writes a PNG. The screen-space HUD (overlay canvas, the foot stars) is not in these frames;
    /// the trail is world geometry and is. Explicit: run on purpose only, e.g.
    /// Unity -batchmode -projectPath unity -runTests -testPlatform PlayMode -testFilter SevenSteps.Tests.PlayMode.TrailCaptureTests
    /// with TRAIL_CAPTURE_OUT=&lt;dir&gt; (default reports/trail-capture).
    /// </summary>
    [Explicit("capture tool, run on purpose")]
    public sealed class TrailCaptureTests
    {
        const string SaveKey = "trailcapture";
        const int W = 1280, H = 720;

        static readonly string[] SaveKeys =
        {
            SaveSystem.ProgressKey, SaveSystem.SettingsKey, SaveSystem.ProgressBackupKey, SaveSystem.TutorialKey, SaveSystem.StoryKey,
        };

        /// name, stage, replay, seconds to let the replay play. Mid-play: the third brother walking after a SWITCH and a
        /// KEEP; won: every trail at the end (the state of the user's screenshot, board-readability.png).
        static readonly (string Name, int Level, string Replay, float Seconds)[] Cases =
        {
            ("trail-35-mid", 35, "UDUDULRXUDLDDRRKRRU", 8f),
            ("trail-35-won", 35, "UDUDULRXUDLDDRRKRRUDDDR", 12f),
            ("trail-50-mid", 50, "UUUDURUKDRRRUUKDR", 8f),
            ("trail-50-won", 50, "UUUDURUKDRRRUUKDRRDLUU", 12f),
            ("trail-41-mid", 41, "LUURRRDLUURRRDXDRR", 8f),
        };

        static string OutDir()
        {
            string env = Environment.GetEnvironmentVariable("TRAIL_CAPTURE_OUT");
            return string.IsNullOrEmpty(env) ? Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/trail-capture")) : env;
        }

        static void WipeSave()
        {
            var store = new SuffixedStore(new PlayerPrefsStore(), SaveKey);
            foreach (var k in SaveKeys) store.Delete(k);
            store.Flush();
        }

        static void DestroyDdolRoots()
        {
            var probe = new GameObject("DDOL probe");
            Object.DontDestroyOnLoad(probe);
            foreach (var g in probe.scene.GetRootGameObjects())
                if (g != probe && !g.name.StartsWith("Code-based tests runner", StringComparison.Ordinal)) Object.DestroyImmediate(g);
            Object.DestroyImmediate(probe);
        }

        [SetUp]
        public void SetUp()
        {
            WipeSave();
            Bootstrap.ResetForTest();
            DestroyDdolRoots();
        }

        [TearDown]
        public void TearDown()
        {
            Bootstrap.ResetForTest();
            DestroyDdolRoots();
            WipeSave();
        }

        [UnityTest]
        public IEnumerator CaptureTrails()
        {
            string dir = OutDir();
            Directory.CreateDirectory(dir);
            var written = new List<string>();
            foreach (var c in Cases)
            {
                Bootstrap.ResetForTest();
                DestroyDdolRoots();
                var args = LaunchOptions.Parse(new[] { "SevenSteps", "-telemetry", "off", "-lang", "ko", "-savekey", SaveKey, "-level", c.Level.ToString(), "-replay", c.Replay }, "");
                Assert.IsEmpty(args.Warnings, c.Name);
                Bootstrap.UseLaunchOptionsForNextRun(args);
                SceneManager.LoadScene(AppScenes.Boot, LoadSceneMode.Single);
                float t0 = Time.realtimeSinceStartup;
                while (!(Services.TryGet<AppFlow>(out var flow) && flow.State == AppState.Game && !flow.IsLoading && SceneManager.GetActiveScene().name == AppScenes.Game))
                {
                    if (Time.realtimeSinceStartup - t0 > 20f) Assert.Fail($"{c.Name}: the Game scene did not open");
                    yield return null;
                }
                var cam = Camera.main;
                Assert.IsNotNull(cam, c.Name + ": no main camera");
                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt; // the board refits to 16:9 on the next frames
                t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < c.Seconds) yield return null;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                string file = Path.Combine(dir, c.Name + ".png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                written.Add(file);
                Object.Destroy(tex);
                cam.targetTexture = null;
                rt.Release();
                Object.Destroy(rt);
                yield return null;
            }
            Debug.Log("[SevenSteps] Trail capture: " + string.Join(", ", written));
            Assert.AreEqual(Cases.Length, written.Count);
        }
    }
}
