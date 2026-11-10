using System;
using System.IO;
using System.Linq;
using SevenSteps.Core;
using SevenSteps.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// QA cross-check in batchmode (production/qa-play-plan.md 3): reports/qa/attempts.jsonl → QaReplay (C# Core + GameSession)
    /// → reports/qa/unity-replay.json. Exit 1 when any attempt differs from the JS harness record, or the input is missing.
    /// Batch: -executeMethod BuildTools.QaReplay [-qaAttempts path] [-qaOut path] (paths relative to the repo root).
    /// </summary>
    public static class QaReplayTool
    {
        public const string LevelsPath = "Assets/_Project/Data/levels.json";
        public const string DefaultAttempts = "reports/qa/attempts.jsonl";
        public const string DefaultOut = "reports/qa/unity-replay.json";

        [MenuItem("Seven Steps/QA/Replay attempts.jsonl")]
        public static void Menu() => Run(DefaultAttempts, DefaultOut);

        public static void Batch()
        {
            int code;
            try { code = Run(Arg("-qaAttempts") ?? DefaultAttempts, Arg("-qaOut") ?? DefaultOut) ? 0 : 1; }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        /// Returns true when every attempt matches.
        public static bool Run(string attemptsPath, string outPath)
        {
            string root = Directory.GetParent(Application.dataPath).Parent.FullName; // unity/Assets → repo
            string input = Path.GetFullPath(Path.Combine(root, attemptsPath));
            string output = Path.GetFullPath(Path.Combine(root, outPath));
            if (!File.Exists(input)) throw new FileNotFoundException($"[QaReplay] no attempts file: {input}");

            var file = JsonUtility.FromJson<LevelFileDto>(File.ReadAllText(Path.Combine(root, "unity", LevelsPath)));
            var levels = file.levels.Select(d => d.ToLevel()).ToArray();
            var minBodies = file.levels.Select(d => d.expected != null ? d.expected.minBodies : 0).ToArray();
            var records = QaReplay.ParseJsonl(File.ReadAllText(input));
            var report = QaReplay.Check(levels, i => minBodies[i], records);

            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            var head = $"[QaReplay] {report.attempts} attempts, {report.mismatches} differ from the JS harness → {output}";
            if (report.Ok) Debug.Log(head);
            else Debug.LogError(head + "\n" + string.Join("\n", report.mismatchList));
            return report.Ok;
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int k = Array.IndexOf(args, name);
            return k >= 0 && k + 1 < args.Length ? args[k + 1] : null;
        }
    }
}
