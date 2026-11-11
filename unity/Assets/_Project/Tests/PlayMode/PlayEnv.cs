using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests.PlayMode
{
    /// <summary>
    /// The PlayMode test environment (docs/scene-flow.md 9.1, architect N5): injected launch options (telemetry off, Korean,
    /// save key "playtest"), a reset between tests as entering Play Mode would do (one Play session runs all tests; domain
    /// reload is off), and waits on the flow.
    /// </summary>
    public static class PlayEnv
    {
        public const string SaveKey = "playtest";
        public const float Timeout = 15f;

        static readonly string[] SaveKeys =
        {
            SaveSystem.ProgressKey, SaveSystem.SettingsKey, SaveSystem.ProgressBackupKey, SaveSystem.TutorialKey, SaveSystem.StoryKey,
        };

        public static LaunchOptions Launch(params string[] extra)
        {
            var args = new List<string> { "SevenSteps", "-telemetry", "off", "-lang", "ko", "-savekey", SaveKey };
            args.AddRange(extra);
            var o = LaunchOptions.Parse(args.ToArray(), "");
            Assert.IsEmpty(o.Warnings, "test launch options parse cleanly");
            return o;
        }

        /// Forget this Play's bootstrap and services, destroy the DontDestroyOnLoad roots and wipe the test save key.
        public static void Reset()
        {
            Bootstrap.ResetForTest();
            DestroyDdolRoots();
            var store = new SuffixedStore(new PlayerPrefsStore(), SaveKey);
            foreach (var k in SaveKeys) store.Delete(k);
            store.Flush();
        }

        /// Waits until the flow is in <paramref name="state"/>, no scene is loading and <paramref name="scene"/> is the active
        /// scene, then one more frame (the new scene's Start).
        public static IEnumerator WaitForState(AppState state, string scene, float timeout = Timeout)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < timeout)
            {
                if (Services.TryGet<AppFlow>(out var flow) && flow.State == state && !flow.IsLoading && SceneManager.GetActiveScene().name == scene)
                {
                    yield return null;
                    yield break;
                }
                yield return null;
            }
            string now = Services.TryGet<AppFlow>(out var f) ? $"{f.State}, loading {f.IsLoading}" : "no AppFlow";
            Assert.Fail($"timed out waiting for {state} in {scene} (flow {now}, active scene {SceneManager.GetActiveScene().name})");
        }

        /// Root objects of the DontDestroyOnLoad scene (found through a probe object moved there), the test runner's own excluded.
        public static List<string> DdolRootNames()
        {
            var probe = new GameObject("DDOL probe");
            Object.DontDestroyOnLoad(probe);
            var names = probe.scene.GetRootGameObjects().Where(g => g != probe && !IsTestRunner(g)).Select(g => g.name).ToList();
            Object.DestroyImmediate(probe);
            return names;
        }

        public static void DestroyDdolRoots()
        {
            var probe = new GameObject("DDOL probe");
            Object.DontDestroyOnLoad(probe);
            foreach (var g in probe.scene.GetRootGameObjects())
                if (g != probe && !IsTestRunner(g)) Object.DestroyImmediate(g);
            Object.DestroyImmediate(probe);
        }

        static bool IsTestRunner(GameObject g) => g.name.StartsWith("Code-based tests runner", StringComparison.Ordinal);
    }
}
