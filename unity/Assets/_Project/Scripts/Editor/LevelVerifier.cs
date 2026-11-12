using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using SevenSteps.Core;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Checks the C# rules against the web prototype: for every stage in levels.json the C# solver must find the same
    /// fewest bodies, move count, route and SWITCH requirement as the JS solver (exported by unity/Tools/export-levels.mjs),
    /// the JS route must replay to a win through Rules.Dispatch, and undoing it must return to the start state.
    /// Menu: Seven Steps > Verify Levels. Batch: -executeMethod SevenSteps.EditorTools.LevelVerifier.BatchVerify
    /// Report: reports/seven-steps-unity-verify.json (repo root, not committed).
    /// </summary>
    public static class LevelVerifier
    {
        const int BodyCap = 3;

        [Serializable]
        sealed class Row
        {
            public int idx;
            public string name;
            public bool ok;
            public int minBodies;
            public int moves;
            public bool needsSwitch;
            public string path;
            public bool replayWin;
            public bool undoBack;
            public long ms;
            public string error;
        }

        [Serializable]
        sealed class Report
        {
            public int total;
            public int failed;
            public List<Row> rows = new List<Row>();
        }

        [MenuItem("Seven Steps/Verify Levels (C# vs JS)", priority = 10)]
        public static void VerifyMenu()
        {
            bool ok = Run();
            EditorUtility.DisplayDialog("Seven Steps", ok ? "모든 스테이지가 JS 솔버와 같은 결과입니다." : "불일치가 있습니다. Console을 확인하세요.", "OK");
        }

        public static void BatchVerify()
        {
            bool ok;
            try { ok = Run(); }
            catch (Exception e) { Debug.LogException(e); ok = false; }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Run()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(ProjectSetup.LevelsPath);
            if (json == null) throw new Exception($"{ProjectSetup.LevelsPath} not found. Run: node unity/Tools/export-levels.mjs");
            var file = JsonUtility.FromJson<LevelFileDto>(json.text);
            var report = new Report();
            var log = new StringBuilder("[SevenSteps] Verify levels (C# vs JS)\n");

            for (int k = 0; k < file.levels.Length; k++)
                Check(file.levels[k], new Row { idx = k + 1, name = file.levels[k].name }, report, log, tutorial: false);
            // Tutorial boards (docs/tutorial.md 2.1): same checks, plus each needs every body it gives. idx = -board number.
            var tutorials = file.tutorials ?? new TutorialDto[0];
            for (int k = 0; k < tutorials.Length; k++)
                Check(tutorials[k].level, new Row { idx = -(k + 1), name = $"{tutorials[k].id} {tutorials[k].level?.name}" }, report, log, tutorial: true);

            report.total = report.rows.Count;
            report.failed = report.rows.FindAll(r => !r.ok).Count;
            log.AppendLine(report.failed == 0 ? $"[SevenSteps] Verify OK: {report.total}/{report.total} boards ({file.levels.Length} stages + {tutorials.Length} tutorials) match the JS solver."
                                               : $"[SevenSteps] Verify FAILED: {report.failed}/{report.total} boards differ.");
            if (report.failed == 0) Debug.Log(log.ToString());
            else Debug.LogError(log.ToString());

            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "reports", "seven-steps-unity-verify.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, JsonUtility.ToJson(report, true));
            return report.failed == 0;
        }

        static void Check(LevelDto dto, Row row, Report report, StringBuilder log, bool tutorial)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var level = dto.ToLevel();
                var best = Solver.MinBodies(level);
                row.needsSwitch = Solver.Solve(level, level.Bodies, noSwitch: true) == null;
                if (best != null)
                {
                    row.minBodies = best.BodiesUsed;
                    row.moves = best.Path.Count;
                    row.path = best.Letters();
                }
                CheckReplay(level, dto.expected.path, out row.replayWin, out row.undoBack);

                var exp = dto.expected;
                var diffs = new List<string>();
                if (best == null) diffs.Add("no solution");
                // The triplets: at most 3 bodies (data rule, docs/architecture.md 6.4).
                if (dto.bodies > BodyCap) diffs.Add($"bodies {dto.bodies} > {BodyCap}");
                if (best != null && row.minBodies > BodyCap) diffs.Add($"needs {row.minBodies} bodies > {BodyCap}");
                if (tutorial && best != null && row.minBodies != dto.bodies) diffs.Add($"tutorial clears with {row.minBodies} of {dto.bodies} bodies");
                if (row.minBodies != exp.minBodies) diffs.Add($"minBodies {row.minBodies} != {exp.minBodies}");
                if (row.moves != exp.moves) diffs.Add($"moves {row.moves} != {exp.moves}");
                if (row.path != exp.path) diffs.Add($"path {row.path} != {exp.path}");
                if (row.needsSwitch != exp.needsSwitch) diffs.Add($"needsSwitch {row.needsSwitch} != {exp.needsSwitch}");
                if (!row.replayWin) diffs.Add("JS route does not win in C#");
                if (!row.undoBack) diffs.Add("undo does not return to start");
                row.ok = diffs.Count == 0;
                row.error = string.Join("; ", diffs);
            }
            catch (Exception e)
            {
                row.ok = false;
                row.error = e.Message;
            }
            row.ms = sw.ElapsedMilliseconds;
            report.rows.Add(row);
            string tag = row.idx > 0 ? $"{row.idx,2}." : $"T{-row.idx}";
            log.AppendLine($"{(row.ok ? "OK  " : "FAIL")} {tag} {row.name}  bodies {row.minBodies}/{dto.bodies}  {(row.needsSwitch ? "SWITCH " : "")}{row.path}  ({row.ms} ms){(row.ok ? "" : "  <- " + row.error)}");
        }

        static void CheckReplay(Level level, string letters, out bool win, out bool undoBack)
        {
            var start = Rules.NewState(level);
            var s = start;
            int applied = 0;
            foreach (char c in letters ?? "")
            {
                if (!Rules.TryParseLetter(c, out var act)) continue;
                var next = Rules.Dispatch(s, Command.FromAct(act)).State;
                if (!ReferenceEquals(next, s)) applied++;
                s = next;
            }
            win = s.Status == Status.Win;
            for (int k = 0; k < applied; k++) s = Rules.Dispatch(s, Command.Undo).State;
            undoBack = Rules.Key(s) == Rules.Key(start) && s.Prev == null;
        }
    }
}
