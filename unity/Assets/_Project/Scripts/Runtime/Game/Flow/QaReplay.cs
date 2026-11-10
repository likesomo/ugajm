using System;
using System.Collections.Generic;
using System.Text;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;

namespace SevenSteps.Game.Flow
{
    /// One line of reports/qa/attempts.jsonl, written by the JS QA harness (unity/Tools/qa-play.mjs, production/qa-play-plan.md 1).
    /// Field meanings: docs/qa-attempts.md (core). Numbers the record leaves out stay at -1 and are not compared.
    [Serializable]
    public sealed class QaAttemptRecord
    {
        public string tester = "";
        /// Stage number, 1-based (levels.json order).
        public int level = -1;
        public string stageName = "";
        public int attempt = -1;
        /// clear / restart / fell / out_of_bodies / gave_up
        public string outcome = "";
        public string inputs = "";
        public int steps = -1;
        public int bodiesUsed = -1;
        public int undos = -1;
        public int hints = -1;
        public int stars = -1;
        public string failReason = "";
        /// Final state hash, see <see cref="QaReplay.StateHash"/>. Empty: not compared.
        public string stateHash = "";
    }

    /// What the C# rules make of one attempt's inputs.
    [Serializable]
    public sealed class QaReplayResult
    {
        /// clear, fell, out_of_bodies, or open (the inputs end before a win or a loss: a restart or a give-up ends it).
        public string outcome = "";
        /// Moves the rules applied (bumps and moves outside Play not counted; undone moves still counted).
        public int steps;
        /// Bodies spent, counting the current one; a loss counts all of the stage's bodies.
        public int bodiesUsed;
        /// Stars.Of on a clear, else 0.
        public int stars;
        public int undos;
        public int hints;
        public int restarts;
        /// Commands the rules rejected (a bump, or a move while choosing).
        public int rejected;
        public string stateKey = "";
        public string stateHash = "";
    }

    /// One attempt in reports/qa/unity-replay.json: the harness record, the C# result and what differs.
    [Serializable]
    public sealed class QaReplayEntry
    {
        public string tester;
        public int level;
        public string stageName;
        public int attempt;
        public QaReplayResult unity;
        public string[] diffs;
    }

    [Serializable]
    public sealed class QaReplayReport
    {
        public int attempts;
        public int mismatches;
        public QaReplayEntry[] entries;
        /// "tester level#attempt: field js=.. unity=.." lines, one per difference.
        public string[] mismatchList;
        public bool Ok => mismatches == 0;
    }

    /// <summary>
    /// QA cross-check (production/qa-play-plan.md 3): replays the blind testers' attempts through GameSession (headless) and
    /// the Core rules, and compares outcome, steps, bodies used, stars and the final state with the JS harness record.
    /// Hints, undos and restarts are counted with <see cref="AttemptCounts.After"/>, the game's own rule, so stars match the game.
    /// Pure C#; the batchmode entry point is BuildTools.QaReplay.
    /// </summary>
    public static class QaReplay
    {
        /// Input letters: U D L R moves, K KEEP, X SWITCH (the replay letters), Z undo, H hint, ! restart.
        /// The restart letter is provisional until docs/qa-attempts.md fixes the token set.
        public const char Undo = 'Z', Hint = 'H', Restart = '!';

        /// Replays <paramref name="inputs"/> from the stage start. <paramref name="minBodies"/>: levels.json expected.minBodies.
        /// Spaces are ignored; any other unknown letter throws FormatException (with its index).
        public static QaReplayResult Run(Level level, int minBodies, string inputs)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            var session = new GameSession();
            var counts = new AttemptCounts(0, 0, 0);
            var r = new QaReplayResult();
            StepResult lastLoss = null;
            session.Updated += u =>
            {
                counts = counts.After(u);
                if (u.Kind == SessionUpdateKind.Present && u.Command.Type == CommandType.Move) r.steps++;
                if (u.Kind == SessionUpdateKind.Feedback) r.rejected++;
                if (u.Result != null && u.Result.Has(GameEventType.Lose)) lastLoss = u.Result;
            };
            session.Load(level);
            string text = inputs ?? "";
            for (int k = 0; k < text.Length; k++)
            {
                char c = char.ToUpperInvariant(text[k]);
                if (c == ' ') continue;
                if (c == Hint) counts = counts.WithHint();
                else if (c == Undo) session.Send(Command.Undo);
                else if (c == Restart) session.Send(Command.Restart);
                else if (Rules.TryParseLetter(c, out var act)) session.Send(Command.FromAct(act));
                else throw new FormatException($"input '{text[k]}' at {k} is not one of UDLRKX{Undo}{Hint}{Restart}");
            }

            var s = session.State;
            r.bodiesUsed = Math.Min(Stars.BodiesUsed(s), level.Bodies); // after a loss no body is left: all of them were used
            r.hints = counts.Hints;
            r.undos = counts.Undos;
            r.restarts = counts.Restarts;
            r.outcome = s.Status == Status.Win ? "clear"
                : s.Status == Status.Lose ? (lastLoss != null && lastLoss.Has(GameEventType.Fall) ? "fell" : "out_of_bodies")
                : "open";
            r.stars = s.Status == Status.Win ? Stars.Of(r.bodiesUsed, minBodies, counts.Hints, counts.Undos, counts.Restarts) : 0;
            r.stateKey = Rules.Key(s);
            r.stateHash = StateHash(s);
            return r;
        }

        /// FNV-1a 32 of the UTF-8 Rules.Key (core.js keyOf fields), 8 lowercase hex digits.
        /// Provisional until docs/qa-attempts.md fixes the hash.
        public static string StateHash(State s)
        {
            uint h = 2166136261;
            foreach (byte b in Encoding.UTF8.GetBytes(Rules.Key(s))) { h ^= b; h *= 16777619; }
            return h.ToString("x8");
        }

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
                QaAttemptRecord rec;
                try { rec = JsonUtility.FromJson<QaAttemptRecord>(line); }
                catch (Exception e) { throw new FormatException($"attempts.jsonl line {n + 1}: {e.Message}", e); }
                list.Add(rec);
            }
            return list;
        }

        /// Replays every record and lists the differences. <paramref name="minBodies"/>: 0-based stage → expected.minBodies.
        /// A record whose stage number or inputs are invalid becomes a mismatch (not an exception), so one bad line does not hide the rest.
        public static QaReplayReport Check(IReadOnlyList<Level> levels, Func<int, int> minBodies, IReadOnlyList<QaAttemptRecord> records)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            if (minBodies == null) throw new ArgumentNullException(nameof(minBodies));
            if (records == null) throw new ArgumentNullException(nameof(records));
            var entries = new List<QaReplayEntry>();
            var all = new List<string>();
            foreach (var rec in records)
            {
                var e = new QaReplayEntry { tester = rec.tester, level = rec.level, stageName = rec.stageName, attempt = rec.attempt };
                var diffs = new List<string>();
                int stage = rec.level - 1;
                if (stage < 0 || stage >= levels.Count) diffs.Add($"level {rec.level} is not 1..{levels.Count}");
                else
                {
                    var level = levels[stage];
                    if (!string.IsNullOrEmpty(rec.stageName) && rec.stageName != level.Name)
                        diffs.Add($"stageName js={rec.stageName} unity={level.Name}");
                    try
                    {
                        e.unity = Run(level, minBodies(stage), rec.inputs);
                        Compare(rec, e.unity, diffs);
                    }
                    catch (FormatException ex) { diffs.Add("inputs: " + ex.Message); }
                }
                e.diffs = diffs.ToArray();
                foreach (var d in diffs) all.Add($"{rec.tester} {rec.level}#{rec.attempt}: {d}");
                entries.Add(e);
            }
            return new QaReplayReport
            {
                attempts = entries.Count,
                mismatches = entries.FindAll(x => x.diffs.Length > 0).Count,
                entries = entries.ToArray(),
                mismatchList = all.ToArray(),
            };
        }

        static void Compare(QaAttemptRecord js, QaReplayResult u, List<string> diffs)
        {
            // restart / gave_up end an attempt the player left: the rules see it unfinished.
            string expected = js.outcome == "restart" || js.outcome == "gave_up" ? "open" : js.outcome;
            if (expected != u.outcome) diffs.Add($"outcome js={js.outcome} unity={u.outcome}");
            Field("steps", js.steps, u.steps, diffs);
            Field("bodiesUsed", js.bodiesUsed, u.bodiesUsed, diffs);
            Field("undos", js.undos, u.undos, diffs);
            Field("hints", js.hints, u.hints, diffs);
            if (js.outcome == "clear") Field("stars", js.stars, u.stars, diffs);
            if (!string.IsNullOrEmpty(js.stateHash) && js.stateHash != u.stateHash)
                diffs.Add($"stateHash js={js.stateHash} unity={u.stateHash} ({u.stateKey})");
        }

        static void Field(string name, int js, int unity, List<string> diffs)
        {
            if (js >= 0 && js != unity) diffs.Add($"{name} js={js} unity={unity}");
        }
    }
}
