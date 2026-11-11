using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Core;
using SevenSteps.Game.Flow;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// QA cross-check replay (production/qa-play-plan.md 3, docs/qa-attempts.md v1): C# Core + GameSession against the JS harness.
    public sealed class QaReplayTests
    {
        // Stage 1 of levels.json: 7 steps right to the exit, minBodies 1.
        static Level Corridor() => Level.Parse("일곱 걸음", "", 3, new[] { "##########", "#S......E#", "##########" });

        /// Lines written by core's harness (unity/Tools/qa-play.mjs, feat/core ca616f5) on stages 1, 2, 10 and 29:
        /// restart, clear after a restart, fell, gave_up after an undo of the loss, out_of_bodies, gave_up after a loss, and the
        /// stage 29 key golden line. `qa-play.mjs verify` passes on it. Regenerate with the harness if the format changes.
        static string FixtureText() =>
            File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Tests", "EditMode", "Flow", "QaAttempts.fixture.jsonl"));

        static LevelDto[] stages;
        static LevelDto[] Stages() => stages ??= JsonUtility.FromJson<LevelFileDto>(
            File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Data", "levels.json"))).levels;

        static QaReplayReport CheckAgainstLevelsJson(System.Collections.Generic.List<QaAttemptRecord> records)
        {
            var dto = Stages();
            return QaReplay.Check(dto.Select(d => d.ToLevel()).ToArray(), i => dto[i].expected.minBodies, records, i => dto[i].map);
        }

        [Test]
        public void HarnessFixtureMatches()
        {
            var records = QaReplay.ParseJsonl(FixtureText());
            Assert.AreEqual(8, records.Count);
            var report = CheckAgainstLevelsJson(records);
            Assert.IsTrue(report.Ok, string.Join("\n", report.mismatchList));
            CollectionAssert.AreEqual(new[] { "open", "clear", "fell", "open", "open", "open", "out_of_bodies", "out_of_bodies" },
                report.entries.Select(e => e.unity.outcome).ToArray());
        }

        /// docs/qa-attempts.md 6 golden (core ca616f5, pinned in the node test too): restart, hint, undo then KEEP again.
        [Test]
        public void Stage29KeyGolden()
        {
            var dto = Stages()[28];
            var r = QaReplay.Run(dto.ToLevel(), dto.expected.minBodies, "R!HDURRRRRKZKDDR");
            Assert.AreEqual("", r.error);
            Assert.AreEqual("10|29|3|2|15|||0|1", r.stateKey);
            Assert.AreEqual("e57f027f", r.stateHash);
            Assert.AreEqual((1, 1, 1), (r.levelRestarts, r.levelHints, r.levelUndos));
        }

        /// Several statues: the list is ascending, comma-joined, no spaces (docs 6). Same line as the fixture's tester B stage 2.
        [Test]
        public void SeveralStatuesKeyGolden()
        {
            var dto = Stages()[1];
            var r = QaReplay.Run(dto.ToLevel(), dto.expected.minBodies, "UDUDUDUKDUDUDUDKRLRLRLR");
            Assert.AreEqual(("15|16|7|0|8,16,22|||3|0", "1d479b67", "out_of_bodies", "stone"), (r.stateKey, r.stateHash, r.outcome, r.failReason));
        }

        [Test]
        public void KeyHashReference() => Assert.AreEqual("e40c292c", QaReplay.KeyHash("a"));

        [Test]
        public void MapHashMatchesTheHarness() => Assert.AreEqual("6428e42a396ce53f", QaReplay.MapHash(Stages()[0].map));

        [Test]
        public void StarsCountTheWholeStageSession()
        {
            var clean = QaReplay.Run(Corridor(), 1, "RRRRRRR");
            Assert.AreEqual(("clear", 7, 1, 3), (clean.outcome, clean.steps, clean.bodiesUsed, clean.stars));

            var undo = QaReplay.Run(Corridor(), 1, "RRRZRRRRR");
            Assert.AreEqual((8, 1, 2), (undo.steps, undo.undos, undo.stars));

            var hint = QaReplay.Run(Corridor(), 1, "HRRRRRRR");
            Assert.AreEqual((1, 1), (hint.levelHints, hint.stars));

            // The earlier attempt's restart and hint still cost stars; steps / hints count only from `from`.
            var later = QaReplay.Run(Corridor(), 1, "RHR!RRRRRRR", 3);
            Assert.AreEqual((1, 1, 7, 0, 1), (later.levelRestarts, later.levelHints, later.steps, later.hints, later.stars));
        }

        [Test]
        public void TokensWithoutEffectAreErrors()
        {
            StringAssert.Contains("token 0 \"L\" had no effect", QaReplay.Run(Corridor(), 1, "L").error);   // bump
            StringAssert.Contains("token 1 \"K\" had no effect", QaReplay.Run(Corridor(), 1, "RK").error);  // not choosing
            StringAssert.Contains("token 0 \"Z\" had no effect", QaReplay.Run(Corridor(), 1, "Z").error);   // nothing to undo
            StringAssert.Contains("token 0 \"!\" on a fresh state", QaReplay.Run(Corridor(), 1, "!").error);
            StringAssert.Contains("token 2 \"!\" on a fresh state", QaReplay.Run(Corridor(), 1, "R!!").error);
            StringAssert.Contains("token 1 \"q\" is not", QaReplay.Run(Corridor(), 1, "Rq").error);
            Assert.AreEqual("", QaReplay.Run(Corridor(), 1, "RZ").error);
        }

        [Test]
        public void NullFieldsKeepTheirDefaults()
        {
            var rec = QaReplay.ParseJsonl("{\"v\":1,\"failReason\":null,\"stars\":null,\"level\":3}")[0];
            Assert.AreEqual((1, "", -1, 3), (rec.v, rec.failReason, rec.stars, rec.level));
            var last = QaReplay.ParseJsonl("{\"level\":4,\"stars\":null}")[0];
            Assert.AreEqual((4, -1), (last.level, last.stars));
        }

        [Test]
        public void DifferencesAreListed()
        {
            var lines = FixtureText().Split('\n').Where(l => l.Trim().Length > 0).ToArray();
            string Edit(int n, string from, string to)
            {
                StringAssert.Contains(from, lines[n]);
                return lines[n].Replace(from, to);
            }
            var tampered = string.Join("\n", new[]
            {
                Edit(1, "\"stars\":2", "\"stars\":3"),
                Edit(2, "\"failReason\":\"fall\"", "\"failReason\":\"stone\""),
                Edit(5, "\"stateHash\":\"e57f027f\"", "\"stateHash\":\"00000000\""),
                Edit(0, "\"stageName\":\"일곱 걸음\"", "\"stageName\":\"other\""),
                Edit(0, "\"v\":1", "\"v\":2"),
                Edit(0, "\"inputs\":\"RRR\"", "\"inputs\":\"LRR\""),
                Edit(0, "\"mapHash\":\"6428e42a396ce53f\"", "\"mapHash\":\"0000000000000000\""),
                Edit(6, "\"outcome\":\"out_of_bodies\"", "\"outcome\":\"fell\""),
            });
            var report = CheckAgainstLevelsJson(QaReplay.ParseJsonl(tampered));
            Assert.AreEqual(8, report.mismatches);
            string First(int n) => report.entries[n].diffs[0];
            Assert.AreEqual("stars js=3 unity=2", First(0));
            Assert.AreEqual("failReason js=stone unity=fall", First(1));
            Assert.AreEqual("stateHash js=00000000 unity=e57f027f", First(2));
            Assert.AreEqual("stageName js=other unity=일곱 걸음", First(3));
            Assert.AreEqual("v 2, this replay reads v1", First(4));
            StringAssert.Contains("token 0 \"L\" had no effect", First(5));
            Assert.AreEqual("skipped: map changed", First(6));
            CollectionAssert.Contains(report.entries[7].diffs, "outcome js=fell unity=out_of_bodies");
            StringAssert.StartsWith("line 1 (A stage 1 #2): stars", report.mismatchList[0]);
        }

        [Test]
        public void BadLineNamesItsNumber()
        {
            var e = Assert.Throws<FormatException>(() => QaReplay.ParseJsonl("{\"level\":1}\n\nnot json\n"));
            StringAssert.Contains("line 3", e.Message);
        }
    }
}
