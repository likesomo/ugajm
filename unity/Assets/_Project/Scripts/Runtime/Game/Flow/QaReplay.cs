using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;

namespace SevenSteps.Game.Flow
{
    /// One line of reports/qa/attempts.jsonl (docs/qa-attempts.md v1), written by the JS QA harness unity/Tools/qa-play.mjs.
    /// JSON null (stars, failReason) and fields a line leaves out keep the defaults below: -1 / "".
    [Serializable]
    public sealed class QaAttemptRecord
    {
        public int v = -1;
        public string tester = "";
        /// Stage number, 1-based (levels.json order); a tutorial board line ("level": "T2", docs/qa-attempts.md 10) reads as -2.
        public int level;
        public string stageName = "";
        public string mapHash = "";
        public int attempt = -1;
        /// clear / fell / out_of_bodies / restart / gave_up
        public string outcome = "";
        /// fall / bridge / seal / vanish / stone when fell / out_of_bodies.
        public string failReason = "";
        /// Every token from the stage session start (start --level) to the end of this attempt.
        public string inputs = "";
        /// Index in inputs of this attempt's first token.
        public int from = -1;
        public int steps = -1, keeps = -1, switches = -1, undos = -1, hints = -1;
        public int levelUndos = -1, levelHints = -1, levelRestarts = -1;
        public int bodiesUsed = -1;
        /// Stars.Of on a clear; null (-1) otherwise.
        public int stars = -1;
        public string stateKey = "";
        public string stateHash = "";
    }

    /// What the C# rules make of one line's inputs (docs/qa-attempts.md 7).
    [Serializable]
    public sealed class QaReplayResult
    {
        /// clear, fell, out_of_bodies, or open (play / choose: a restart or give-up line ends there).
        public string outcome = "";
        /// fall / bridge / seal / vanish / stone on a loss: the last body-losing event of the last command; else "".
        public string failReason = "";
        /// Counted in inputs[from:]: moves, KEEP, SWITCH, undos, hints.
        public int steps, keeps, switches, undos, hints;
        /// Counted over all inputs (the stage session): Z, H and ! .
        public int levelUndos, levelHints, levelRestarts;
        public int bodiesUsed;
        /// Stars.Of with the game's own attempt counts (AttemptCounts) on a clear; -1 otherwise.
        public int stars = -1;
        public string stateKey = "";
        public string stateHash = "";
        /// The first token that had no effect (docs 3: never recorded by the harness), or "". Replay stops there.
        public string error = "";
    }

    /// One line in reports/qa/unity-replay.json: who, the C# result and what differs.
    [Serializable]
    public sealed class QaReplayEntry
    {
        public int line;
        public string tester;
        public int level;
        public string stageName;
        public int attempt;
        public string outcome;
        public QaReplayResult unity;
        public string[] diffs;
    }

    [Serializable]
    public sealed class QaReplayReport
    {
        public int attempts;
        public int mismatches;
        /// "line N (tester stage L #attempt): field js=.. unity=.." per difference, the shape of qa-play.mjs verify.
        public string[] mismatchList;
        public QaReplayEntry[] entries;
        public bool Ok => mismatches == 0;
    }

    /// <summary>
    /// QA cross-check (production/qa-play-plan.md 3, docs/qa-attempts.md 7): replays each harness line from the stage start
    /// through GameSession (headless) and the Core rules, and compares it field by field. Reference: qa-play.mjs replayLine().
    /// Stars use <see cref="AttemptCounts.After"/>, the game's own counting, so a harness/game disagreement shows up as a star diff.
    /// Pure C#; the batchmode entry point is BuildTools.QaReplay (unity.sh qa-replay).
    /// </summary>
    public static class QaReplay
    {
        public const int FormatVersion = 1;
        /// Input tokens besides the replay letters U D L R K X (docs/qa-attempts.md 3).
        public const char Undo = 'Z', Hint = 'H', Restart = '!';

        /// Replays <paramref name="inputs"/> from Rules.NewState.
        /// <paramref name="from"/>: this attempt's first token (counts of steps..hints start there).
        public static QaReplayResult Run(Level level, string inputs, int from = 0)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            inputs ??= "";
            var r = new QaReplayResult();
            var session = new GameSession();
            var counts = new AttemptCounts(0, 0, 0);
            StepResult last = null;
            session.Updated += u =>
            {
                counts = counts.After(u);
                if (u.Result != null) last = u.Result;
            };
            session.Load(level);

            for (int k = 0; k < inputs.Length; k++)
            {
                char c = inputs[k];
                bool own = k >= from;
                if (c == Hint) { counts = counts.WithHint(); r.levelHints++; if (own) r.hints++; continue; }
                bool ok;
                if (c == Undo) { ok = session.Send(Command.Undo) == SendOutcome.Applied; r.levelUndos++; if (own) r.undos++; }
                else if (c == Restart)
                {
                    // Only a restart the game counts is recorded: the current state has a Prev (docs 3).
                    if (session.State.Prev == null) { r.error = $"token {k} \"!\" on a fresh state (such a restart is not counted and never recorded)"; break; }
                    ok = session.Send(Command.Restart) == SendOutcome.Applied;
                    r.levelRestarts++;
                }
                else if ("UDLRKX".IndexOf(c) >= 0 && Rules.TryParseLetter(c, out var act))
                {
                    ok = session.Send(Command.FromAct(act)) == SendOutcome.Applied;
                    if (own)
                    {
                        if (c == 'K') r.keeps++;
                        else if (c == 'X') r.switches++;
                        else r.steps++;
                    }
                }
                else { r.error = $"token {k} \"{c}\" is not U D L R K X Z H !"; break; }
                if (!ok) { r.error = $"token {k} \"{c}\" had no effect"; break; }
            }

            var s = session.State;
            r.bodiesUsed = Math.Min(Stars.BodiesUsed(s), level.Bodies); // a loss leaves no body: all of them were used
            r.outcome = s.Status == Status.Win ? "clear"
                : s.Status == Status.Lose ? (last != null && last.Has(GameEventType.Fall) ? "fell" : "out_of_bodies")
                : "open";
            if (s.Status == Status.Lose) r.failReason = LossOf(last);
            if (s.Status == Status.Win) r.stars = Stars.Of(r.bodiesUsed, level.Bodies, counts.Hints);
            r.stateKey = Rules.Key(s);
            r.stateHash = KeyHash(r.stateKey);
            return r;
        }

        /// The last event of a command that cost a body (core.js LOSS: the event type in lower case), or "".
        static string LossOf(StepResult last)
        {
            if (last == null) return "";
            for (int k = last.Events.Count - 1; k >= 0; k--)
                switch (last.Events[k].Type)
                {
                    case GameEventType.Fall: return "fall";
                    case GameEventType.Bridge: return "bridge";
                    case GameEventType.Seal: return "seal";
                    case GameEventType.Vanish: return "vanish";
                    case GameEventType.Stone: return "stone";
                }
            return "";
        }

        /// stateHash (docs 6): FNV-1a 32 (offset 2166136261, prime 16777619) of the UTF-8 key, 8 lowercase hex digits.
        public static string KeyHash(string key)
        {
            uint h = 2166136261;
            foreach (byte b in Encoding.UTF8.GetBytes(key ?? "")) { h ^= b; h *= 16777619; }
            return h.ToString("x8");
        }

        /// mapHash (docs 5): SHA-256 of the map rows joined by \n, first 16 lowercase hex digits.
        public static string MapHash(string[] map)
        {
            using var sha = SHA256.Create();
            var d = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", map ?? new string[0])));
            var sb = new StringBuilder(16);
            for (int k = 0; k < 8; k++) sb.Append(d[k].ToString("x2"));
            return sb.ToString();
        }

        static readonly Regex NullField = new Regex("\"[A-Za-z0-9_]+\"\\s*:\\s*null\\s*,?");
        static readonly Regex TrailingComma = new Regex(",\\s*}");
        static readonly Regex TutorialLevel = new Regex("\"level\"\\s*:\\s*\"T([1-9])\"");

        /// "T2" for a tutorial board record (level -2), else the stage number.
        public static string LevelLabel(int level) => level < 0 ? "T" + (-level) : level.ToString();

        /// Parses attempts.jsonl. Blank lines are skipped; a line that is not a JSON object throws FormatException with its number.
        public static List<QaAttemptRecord> ParseJsonl(string text)
        {
            var list = new List<QaAttemptRecord>();
            var lines = (text ?? "").Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n].Trim();
                if (line.Length == 0) continue;
                if (line[0] != '{') throw new FormatException($"attempts.jsonl line {n + 1}: not a JSON object");
                // JsonUtility has no null for int / string: drop "x": null so the field keeps its default (-1 / "").
                line = TrailingComma.Replace(NullField.Replace(line, ""), "}");
                // A tutorial board's "level": "T2" reads as -2 (JsonUtility has no int-or-string field; docs/qa-attempts.md 10).
                line = TutorialLevel.Replace(line, m => "\"level\":-" + m.Groups[1].Value);
                QaAttemptRecord rec;
                try { rec = JsonUtility.FromJson<QaAttemptRecord>(line); }
                catch (Exception e) { throw new FormatException($"attempts.jsonl line {n + 1}: {e.Message}", e); }
                list.Add(rec);
            }
            return list;
        }

        /// Replays every record and lists the differences (docs 7). <paramref name="mapRows"/>
        /// takes a 0-based stage; mapRows null = mapHash not compared. A bad line becomes a mismatch, not an exception.
        /// Tutorial board records (level -1..-3) replay on <paramref name="tutorials"/> (levels.json "tutorials" order;
        /// <paramref name="tutorialMapRows"/> 0-based); without them such a record is a mismatch.
        public static QaReplayReport Check(IReadOnlyList<Level> levels, IReadOnlyList<QaAttemptRecord> records,
            Func<int, string[]> mapRows = null, IReadOnlyList<Level> tutorials = null, Func<int, string[]> tutorialMapRows = null)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            if (records == null) throw new ArgumentNullException(nameof(records));
            var entries = new List<QaReplayEntry>();
            var all = new List<string>();
            for (int n = 0; n < records.Count; n++)
            {
                var rec = records[n];
                var e = new QaReplayEntry
                {
                    line = n + 1, tester = rec.tester, level = rec.level, stageName = rec.stageName, attempt = rec.attempt, outcome = rec.outcome,
                };
                var diffs = new List<string>();
                bool board = rec.level < 0;
                var list = board ? tutorials ?? Array.Empty<Level>() : levels;
                var rows = board ? tutorialMapRows : mapRows;
                int stage = board ? -rec.level - 1 : rec.level - 1;
                if (rec.v != FormatVersion) diffs.Add($"v {rec.v}, this replay reads v{FormatVersion}");
                else if (stage < 0 || stage >= list.Count)
                    diffs.Add(board ? $"level {LevelLabel(rec.level)} is not T1..T{list.Count}" : $"level {rec.level} is not 1..{levels.Count}");
                else if (rec.stageName != list[stage].Name) diffs.Add($"stageName js={rec.stageName} unity={list[stage].Name}");
                else if (rows != null && rec.mapHash != MapHash(rows(stage))) diffs.Add("skipped: map changed");
                else if (rec.from < 0 || rec.from > rec.inputs.Length) diffs.Add($"from {rec.from} is not 0..{rec.inputs.Length}");
                else
                {
                    e.unity = Run(list[stage], rec.inputs, rec.from);
                    if (e.unity.error.Length > 0) diffs.Add(e.unity.error);
                    else Compare(rec, e.unity, diffs);
                }
                e.diffs = diffs.ToArray();
                foreach (var d in diffs) all.Add($"line {n + 1} ({rec.tester} stage {LevelLabel(rec.level)} #{rec.attempt}): {d}");
                entries.Add(e);
            }
            return new QaReplayReport
            {
                attempts = entries.Count,
                mismatches = entries.FindAll(x => x.diffs.Length > 0).Count,
                mismatchList = all.ToArray(),
                entries = entries.ToArray(),
            };
        }

        /// Final outcomes a line's outcome allows (docs 4, last column).
        static string[] Allowed(string outcome) => outcome switch
        {
            "clear" => new[] { "clear" },
            "fell" => new[] { "fell" },
            "out_of_bodies" => new[] { "out_of_bodies" },
            "restart" => new[] { "open" },
            "gave_up" => new[] { "open", "fell", "out_of_bodies" },
            _ => null,
        };

        static void Compare(QaAttemptRecord js, QaReplayResult u, List<string> diffs)
        {
            var allowed = Allowed(js.outcome);
            if (allowed == null) diffs.Add($"outcome \"{js.outcome}\" unknown");
            else if (Array.IndexOf(allowed, u.outcome) < 0) diffs.Add($"outcome js={js.outcome} unity={u.outcome}");
            if ((js.outcome == "fell" || js.outcome == "out_of_bodies") && js.failReason != u.failReason)
                diffs.Add($"failReason js={js.failReason} unity={u.failReason}");
            Text("stateKey", js.stateKey, u.stateKey, diffs);
            Text("stateHash", js.stateHash, u.stateHash, diffs);
            Number("levelUndos", js.levelUndos, u.levelUndos, diffs);
            Number("levelHints", js.levelHints, u.levelHints, diffs);
            Number("levelRestarts", js.levelRestarts, u.levelRestarts, diffs);
            Number("bodiesUsed", js.bodiesUsed, u.bodiesUsed, diffs);
            Number("steps", js.steps, u.steps, diffs);
            Number("keeps", js.keeps, u.keeps, diffs);
            Number("switches", js.switches, u.switches, diffs);
            Number("undos", js.undos, u.undos, diffs);
            Number("hints", js.hints, u.hints, diffs);
            Number("stars", js.stars, u.stars, diffs); // both -1 (null) unless cleared
        }

        static void Number(string name, int js, int unity, List<string> diffs)
        {
            if (js != unity) diffs.Add($"{name} js={(js < 0 ? "null" : js.ToString())} unity={(unity < 0 ? "null" : unity.ToString())}");
        }

        static void Text(string name, string js, string unity, List<string> diffs)
        {
            if (js != unity) diffs.Add($"{name} js={js} unity={unity}");
        }
    }
}
