using System;
using NUnit.Framework;
using SevenSteps.Core;
using SevenSteps.Game.Flow;

namespace SevenSteps.Tests
{
    /// QA cross-check replay (production/qa-play-plan.md 3): C# Core + GameSession results for harness attempt records.
    public sealed class QaReplayTests
    {
        // Stage 1 of levels.json: 7 steps right to the exit, minBodies 1.
        static Level Corridor() => Level.Parse("일곱 걸음", "", 3, new[] { "##########", "#S......E#", "##########" });
        // One body, exit out of reach: the 7th step turns it to stone and no body is left.
        static Level TooFar() => Level.Parse("far", "", 1, new[] { "############", "#S.........E#", "############" });
        // One body, a pit on the 2nd step: it falls (not the lucky 7th step) and no body is left.
        static Level Pit() => Level.Parse("pit", "", 1, new[] { "##########", "#S.o....E#", "##########" });

        static readonly Level[] Levels = { Corridor(), TooFar(), Pit() };
        static int MinBodies(int stage) => 1;

        const string Fixture =
            "{\"tester\":\"A\",\"level\":1,\"stageName\":\"일곱 걸음\",\"attempt\":1,\"outcome\":\"restart\",\"inputs\":\"RRR\",\"steps\":3,\"bodiesUsed\":1,\"undos\":0,\"hints\":0}\n" +
            "\n" +
            "{\"tester\":\"A\",\"level\":1,\"stageName\":\"일곱 걸음\",\"attempt\":2,\"outcome\":\"clear\",\"inputs\":\"RRRZRRRRR\",\"steps\":8,\"bodiesUsed\":1,\"undos\":1,\"hints\":0,\"stars\":2}\r\n" +
            "{\"tester\":\"B\",\"level\":2,\"attempt\":1,\"outcome\":\"out_of_bodies\",\"inputs\":\"RRRRRRR\",\"steps\":7,\"bodiesUsed\":1}\n" +
            "{\"tester\":\"B\",\"level\":3,\"attempt\":1,\"outcome\":\"fell\",\"inputs\":\"RR\",\"steps\":2}\n";

        [Test]
        public void CleanClearIsThreeStars()
        {
            var r = QaReplay.Run(Corridor(), 1, "RRRRRRR");
            Assert.AreEqual("clear", r.outcome);
            Assert.AreEqual(7, r.steps);
            Assert.AreEqual(1, r.bodiesUsed);
            Assert.AreEqual(3, r.stars);
            Assert.AreEqual(0, r.rejected);
            Assert.AreEqual(8, r.stateHash.Length);
        }

        [Test]
        public void UndoHintAndRestartCountLikeTheGame()
        {
            var undo = QaReplay.Run(Corridor(), 1, "RRRZRRRRR");
            Assert.AreEqual(("clear", 8, 1, 2), (undo.outcome, undo.steps, undo.undos, undo.stars));

            var hint = QaReplay.Run(Corridor(), 1, "HRRRRRRR");
            Assert.AreEqual((1, 1), (hint.hints, hint.stars));

            var restart = QaReplay.Run(Corridor(), 1, "RR!RRRRRRR");
            Assert.AreEqual((1, 2), (restart.restarts, restart.stars));

            // docs 6.5: a restart from the stage start and an undo with nothing to undo are not counted.
            var idle = QaReplay.Run(Corridor(), 1, "!ZRRRRRRR");
            Assert.AreEqual((0, 0, 3), (idle.restarts, idle.undos, idle.stars));
        }

        [Test]
        public void LossesAreFellOrOutOfBodies()
        {
            var far = QaReplay.Run(TooFar(), 1, "RRRRRRR");
            Assert.AreEqual(("out_of_bodies", 1), (far.outcome, far.bodiesUsed));
            var fell = QaReplay.Run(Pit(), 1, "RR");
            Assert.AreEqual(("fell", 0), (fell.outcome, fell.stars));
        }

        [Test]
        public void UnfinishedInputsAreOpenAndBumpsAreNotSteps()
        {
            var r = QaReplay.Run(Corridor(), 1, "LRR K");
            Assert.AreEqual("open", r.outcome);
            Assert.AreEqual(2, r.steps);
            Assert.AreEqual(2, r.rejected); // the wall on the left, KEEP while not choosing
        }

        [Test]
        public void UnknownInputThrows()
        {
            var e = Assert.Throws<FormatException>(() => QaReplay.Run(Corridor(), 1, "RRQ"));
            StringAssert.Contains("at 2", e.Message);
        }

        [Test]
        public void SameInputsGiveTheSameStateHash()
        {
            Assert.AreEqual(QaReplay.Run(Corridor(), 1, "RRZ").stateHash, QaReplay.Run(Corridor(), 1, "R").stateHash);
            Assert.AreNotEqual(QaReplay.Run(Corridor(), 1, "RR").stateHash, QaReplay.Run(Corridor(), 1, "R").stateHash);
        }

        [Test]
        public void FixtureMatches()
        {
            var records = QaReplay.ParseJsonl(Fixture);
            Assert.AreEqual(4, records.Count);
            Assert.AreEqual(-1, records[3].bodiesUsed, "a field the record leaves out is -1 (not compared)");
            var report = QaReplay.Check(Levels, MinBodies, records);
            Assert.AreEqual(4, report.attempts);
            Assert.IsTrue(report.Ok, string.Join("\n", report.mismatchList));
        }

        [Test]
        public void DifferencesAreListed()
        {
            var records = QaReplay.ParseJsonl(
                "{\"tester\":\"C\",\"level\":1,\"stageName\":\"일곱 걸음\",\"attempt\":4,\"outcome\":\"clear\",\"inputs\":\"RRRRRRR\",\"steps\":6,\"stars\":3,\"stateHash\":\"00000000\"}\n" +
                "{\"tester\":\"C\",\"level\":2,\"stageName\":\"other\",\"attempt\":1,\"outcome\":\"clear\",\"inputs\":\"RRRRRRR\"}\n" +
                "{\"tester\":\"C\",\"level\":9,\"attempt\":1,\"outcome\":\"clear\",\"inputs\":\"R\"}\n" +
                "{\"tester\":\"C\",\"level\":1,\"attempt\":5,\"outcome\":\"clear\",\"inputs\":\"R?\"}\n");
            var report = QaReplay.Check(Levels, MinBodies, records);
            Assert.AreEqual(4, report.mismatches);
            CollectionAssert.AreEquivalent(new[] { "steps js=6 unity=7", $"stateHash js=00000000 unity={report.entries[0].unity.stateHash} ({report.entries[0].unity.stateKey})" }, report.entries[0].diffs);
            CollectionAssert.AreEquivalent(new[] { "stageName js=other unity=far", "outcome js=clear unity=out_of_bodies" }, report.entries[1].diffs);
            StringAssert.StartsWith("level 9 is not 1..3", report.entries[2].diffs[0]);
            StringAssert.StartsWith("inputs: ", report.entries[3].diffs[0]);
            StringAssert.StartsWith("C 1#4: steps", report.mismatchList[0]);
        }

        [Test]
        public void BadLineNamesItsNumber()
        {
            var e = Assert.Throws<FormatException>(() => QaReplay.ParseJsonl("{\"level\":1}\n\nnot json\n"));
            StringAssert.Contains("line 3", e.Message);
        }
    }
}
