using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SevenSteps.Core;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// Tutorial boards T1..T3 in the shipped levels.json and the cue matcher TutorialCues (docs/tutorial.md).
    public class TutorialTests
    {
        static LevelFileDto shipped;
        static LevelFileDto Shipped() => shipped ??=
            JsonUtility.FromJson<LevelFileDto>(File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Data", "levels.json")));

        static TutorialDto Board(string id)
        {
            foreach (var t in Shipped().tutorials)
                if (t.id == id) return t;
            Assert.Fail($"no tutorial \"{id}\" in levels.json");
            return null;
        }

        /// "on", "on/body" for a body-filtered cue, "-" for no cue.
        static string On(TutorialDto t, int cue) => cue < 0 ? "-" : t.cues[cue].on + (t.cues[cue].body != 0 ? "/" + t.cues[cue].body : "");

        /// Plays letters (U D L R K X, Z undo, * restart) from the start and returns the cue each command shows.
        static List<string> Play(TutorialDto t, string letters, out State end)
        {
            var s = Rules.NewState(t.level.ToLevel());
            var shown = new List<string>();
            foreach (char c in letters)
            {
                Command cmd;
                if (c == 'Z') cmd = Command.Undo;
                else if (c == '*') cmd = Command.Restart;
                else { Assert.IsTrue(Rules.TryParseLetter(c, out var act), $"letter {c}"); cmd = Command.FromAct(act); }
                var r = Rules.Dispatch(s, cmd);
                shown.Add(On(t, TutorialCues.Match(t, cmd, s, r.State, r.Events)));
                s = r.State;
            }
            end = s;
            return shown;
        }

        [Test]
        public void ShippedBoardsAreT1ToT3WithOneTwoThreeBodies()
        {
            var ts = Shipped().tutorials;
            Assert.AreEqual(3, ts.Length, "three tutorial boards");
            CollectionAssert.AreEqual(new[] { "t1", "t2", "t3" }, new[] { ts[0].id, ts[1].id, ts[2].id });
            var stageNames = new HashSet<string>();
            foreach (var l in Shipped().levels) stageNames.Add(l.name);
            for (int k = 0; k < ts.Length; k++)
            {
                var t = ts[k];
                Assert.AreEqual(k + 1, t.level.bodies, $"{t.id} bodies");
                Assert.GreaterOrEqual(t.version, 1, t.id);
                Assert.IsFalse(stageNames.Contains(t.level.name), $"{t.id}: name is a stage name");
                Assert.AreEqual("", t.level.StartLine, $"{t.id}: the cues own the start line");
                Assert.GreaterOrEqual(TutorialCues.Start(t), 0, $"{t.id}: no start cue");
                var level = t.level.ToLevel();
                foreach (var c in t.cues)
                {
                    Assert.IsTrue(TutorialCues.IsValidOn(c.on), $"{t.id}: on \"{c.on}\"");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(c.line), $"{t.id} {c.on}: ko line");
                    Assert.AreNotEqual(c.line, c.Line("en"), $"{t.id} {c.on}: en line missing");
                    StringAssert.DoesNotContain("·", c.line, "U+00B7 is not in Gaegu");
                    if (c.HasCell) Assert.IsTrue(c.X < level.W && c.Y < level.H, $"{t.id} {c.on}: cell off the board");
                }
            }
        }

        [Test]
        public void EachBoardWinsWithItsRouteAndNeedsAllBodies()
        {
            foreach (var t in Shipped().tutorials)
            {
                var level = t.level.ToLevel();
                Assert.AreEqual(level.Bodies, Solver.MinBodies(level).BodiesUsed, $"{t.id}: clears with fewer bodies");
                Play(t, t.level.expected.path, out var end);
                Assert.AreEqual(Status.Win, end.Status, t.id);
                Assert.AreEqual(Stars.Max, Stars.Of(Stars.BodiesUsed(end), level.Bodies, 0), $"{t.id}: a clear without hint is star 3");
            }
        }

        [Test]
        public void T1CuesFollowTheSteps()
        {
            var t = Board("t1");
            Assert.AreEqual("start", On(t, TutorialCues.Start(t)));
            CollectionAssert.AreEqual(new[] { "step", "-", "-", "-", "-", "step", "win" }, Play(t, "RRRDDLL", out _));
            // The 7th step ends off the exit: the only body turns to stone -> lose. Restart -> the restart cue.
            CollectionAssert.AreEqual(new[] { "step", "-", "-", "-", "-", "step", "lose", "restart" }, Play(t, "RRRDDLR*", out var s));
            Assert.AreEqual(Status.Play, s.Status);
        }

        [Test]
        public void T2ChooseThenKeepOpensTheDoor()
        {
            var t = Board("t2");
            Assert.AreEqual("DUDRRRRKDDRRRDD", t.level.expected.path, "route changed: update this test");
            var shown = Play(t, t.level.expected.path, out _);
            Assert.AreEqual("step/1", shown[4], "5th step of the first brother");
            Assert.AreEqual("choose", shown[6]);
            Assert.AreEqual("doorOpen", shown[7], "KEEP: Stone, DoorOpen, Respawn; no respawn cue");
            Assert.AreEqual("-", shown[12], "the second brother's 5th step: the body-1 cue does not show");
            Assert.AreEqual("win", shown[14]);
        }

        [Test]
        public void T2SwitchKeepsTheSwitchLine()
        {
            var t = Board("t2");
            var shown = Play(t, "DUDRRRR" + "X", out var s);
            Assert.AreEqual("switch", shown[7]);
            Assert.AreEqual(2, Stars.BodiesUsed(s), "the second brother woke on the plate");
        }

        [Test]
        public void T3RespawnCuesPerBrother()
        {
            var t = Board("t3");
            var shown = Play(t, t.level.expected.path, out var end);
            CollectionAssert.AreEqual(new[] { "respawn/2", "respawn/3" }, shown.FindAll(x => x.StartsWith("respawn")));
            Assert.AreEqual("win", shown[shown.Count - 1]);
            Assert.AreEqual(3, Stars.BodiesUsed(end), "all three brothers walked");
        }

        static TutorialDto Synthetic(params TutorialCueDto[] cues) => new TutorialDto
        {
            id = "x",
            level = new LevelDto { name = "x", hint = "", bodies = 2, map = new[] { "##########", "#S......E#", "##########" } },
            cues = cues,
        };

        static TutorialCueDto Cue(string on, int n = 0, int body = 0) => new TutorialCueDto { on = on, n = n, body = body, line = on };

        [Test]
        public void LastMatchInEventThenListOrderWins()
        {
            // The 7th step onto the exit is one Win event: both "win" and "seventh" match it; the later cue in the list shows.
            var a = Synthetic(Cue("start"), Cue("win"), Cue("seventh"));
            var b = Synthetic(Cue("start"), Cue("seventh"), Cue("win"));
            Assert.AreEqual("seventh", Play(a, "RRRRRRR", out _)[6]);
            Assert.AreEqual("win", Play(b, "RRRRRRR", out _)[6]);
        }

        [Test]
        public void UndoBumpAndRestartFallBack()
        {
            var a = Synthetic(Cue("start"), Cue("step", n: 1), Cue("seventh"));
            CollectionAssert.AreEqual(new[] { "-", "step", "-", "start" }, Play(a, "LRZ*", out _), "bump, step 1, undo, restart -> start");
            Assert.AreEqual(-1, TutorialCues.Start(Synthetic()), "no start cue");
            Assert.AreEqual(-1, TutorialCues.Restart(Synthetic()), "no start or restart cue");
            // A middle body's 7th step on floor: Choose (seventh); KEEP is not a move, so the Stone it makes is not "seventh".
            CollectionAssert.AreEqual(new[] { "seventh", "-" }, Play(a, "RRRRRRLK", out _).GetRange(6, 2));
        }

        [Test]
        public void BodyFilterUsesTheBrotherWhoWalks()
        {
            var a = Synthetic(Cue("step", n: 1, body: 2), Cue("respawn", body: 2));
            var shown = Play(a, "R" + "RRRRRL" + "K" + "R", out _);
            Assert.AreEqual("-", shown[0], "first brother");
            Assert.AreEqual("respawn/2", shown[7], "respawn counts the brother who wakes");
            Assert.AreEqual("step/2", shown[8]);
        }

        [Test]
        public void LineFallsBackToKo()
        {
            var c = new TutorialCueDto { on = "start", line = "가", text = new[] { new TutorialLineDto { lang = "en", line = "A" } } };
            Assert.AreEqual("A", c.Line("en"));
            Assert.AreEqual("가", c.Line("ko"));
            Assert.AreEqual("가", c.Line("ja"));
            Assert.IsFalse(c.HasCell);
            Assert.AreEqual(-1, c.X);
            Assert.IsTrue(new TutorialCueDto { cell = new[] { 2, 3 } }.HasCell);
        }
    }
}
