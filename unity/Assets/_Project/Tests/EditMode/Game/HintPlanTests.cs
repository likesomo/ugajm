using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Core;
using SevenSteps.Game;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// The staged hint (QA-1 (A), contract review 2026-11-11-staged-hint-contract.md (3)): HintPlan from the stored
    /// solution of every shipped stage, and the tiers HintSteps opens.
    public sealed class HintPlanTests
    {
        static LevelFileDto Shipped() =>
            JsonUtility.FromJson<LevelFileDto>(File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Data", "levels.json")));

        static int Seventh(Level level, string path, int goal)
        {
            // The cell where the run of body `goal` ends, walking the route again with Dispatch and counting body ends.
            var s = Rules.NewState(level);
            int ended = 0;
            foreach (char c in path)
            {
                Rules.TryParseLetter(c, out var act);
                var r = Rules.Dispatch(s, Command.FromAct(act));
                foreach (var e in r.Events)
                    if (e.Type == GameEventType.Choose || e.Type == GameEventType.Bridge || e.Type == GameEventType.Seal
                        || e.Type == GameEventType.Vanish || e.Type == GameEventType.Fall
                        || (e.Type == GameEventType.Stone && Command.FromAct(act).Type == CommandType.Move))
                    {
                        if (ended == goal) return e.At;
                        ended++;
                    }
                s = r.State;
            }
            return -1;
        }

        [Test]
        public void EveryShippedStageHasOnePlanTiersOnePlusGoalsPlusOne()
        {
            var file = Shipped();
            Assert.AreEqual(50, file.levels.Length);
            int keep = 0, sw = 0, other = 0;
            foreach (var dto in file.levels)
            {
                var level = dto.ToLevel();
                var plan = HintPlan.From(level, dto.expected.path);
                Assert.AreEqual(plan.Goals.Count + 2, plan.Tiers, dto.name);
                // One goal per body that ends before the exit: the winning body is the last one walked.
                Assert.LessOrEqual(plan.Goals.Count, level.Bodies - 1, dto.name);
                for (int g = 0; g < plan.Goals.Count; g++)
                {
                    var goal = plan.Goals[g];
                    Assert.AreEqual(g, goal.Body, dto.name);
                    Assert.AreEqual(Seventh(level, dto.expected.path, g), goal.Cell, $"{dto.name} goal {g}: on the route");
                    if (goal.Kind == HintGoalKind.Keep) keep++;
                    else if (goal.Kind == HintGoalKind.Switch) sw++;
                    else other++;
                }
                Assert.IsTrue(plan.TryOnRoute(Rules.NewState(level), out int atStart) && atStart == 0, dto.name);
            }
            // level-design's replay of all 50 (2026-11-11): KEEP 26, SWITCH 26, bridge 10, seal 3.
            TestContext.WriteLine($"KEEP {keep}, SWITCH {sw}, other {other}");
            Assert.Greater(keep, 0);
            Assert.Greater(sw, 0);
        }

        static Level Two() => Level.Parse("t", "", 2, new[] { "##########", "#S......E#", "#........#", "##########" });

        [Test]
        public void BadPathsThrow()
        {
            var level = Two();
            Assert.Throws<ArgumentException>(() => HintPlan.From(level, ""));
            Assert.Throws<ArgumentException>(() => HintPlan.From(level, "RRR"), "does not reach the exit");
            Assert.Throws<ArgumentException>(() => HintPlan.From(level, "LLL"), "a letter that does nothing");
            Assert.Throws<ArgumentException>(() => HintPlan.From(level, "RRRRRRRQ"));
        }

        [Test]
        public void StepsOpenPrincipleGoalsThenRouteAndResumeOnTheRoute()
        {
            var file = Shipped();
            var dto = file.levels.First(d => HintPlan.From(d.ToLevel(), d.expected.path).Goals.Count >= 2);
            var level = dto.ToLevel();
            var plan = HintPlan.From(level, dto.expected.path);
            var steps = new HintSteps(plan, principle: true);
            var start = Rules.NewState(level);

            var t1 = steps.Open(start);
            Assert.AreEqual(HintTier.Shows.Principle, t1.What);
            Assert.AreEqual(1, t1.Tier);
            Assert.AreEqual(HintRules.Costs(1), t1.Costs);

            var t2 = steps.Open(start);
            Assert.AreEqual(HintTier.Shows.Goal, t2.What);
            Assert.AreEqual(2, t2.Tier);
            Assert.AreEqual(plan.Goals[0].Cell, t2.Goal.Cell);
            Assert.IsTrue(t2.Costs);
            Assert.IsFalse(t2.FromStart);

            // Walk the route through the first body's end: the next H gives the body walking now (goal 1).
            var s = start;
            foreach (char c in plan.Path)
            {
                Rules.TryParseLetter(c, out var act);
                s = Rules.Dispatch(s, Command.FromAct(act)).State;
                if (plan.TryOnRoute(s, out int reached) && reached == 1) break;
            }
            Assert.AreEqual(HintPlan.GoalTier(1), steps.NextTier(s));
            var t3 = steps.Open(s);
            Assert.AreEqual(plan.Goals[1].Cell, t3.Goal.Cell);

            for (int k = 2; k < plan.Goals.Count; k++) steps.Open(s);
            var route = steps.Open(s);
            Assert.AreEqual(HintTier.Shows.Route, route.What);
            Assert.AreEqual(plan.Tiers, route.Tier);
            Assert.AreEqual(plan.Tiers, steps.Open(s).Tier, "after the route, the route again");
        }

        [Test]
        public void NoPrincipleStartsAtTierTwoAndOffTheRouteIsFromTheStart()
        {
            var file = Shipped();
            var dto = file.levels.First(d => HintPlan.From(d.ToLevel(), d.expected.path).Goals.Count >= 1);
            var level = dto.ToLevel();
            var plan = HintPlan.From(level, dto.expected.path);
            var steps = new HintSteps(plan, principle: false); // startLine "hint": tier 1 was the start line
            var start = Rules.NewState(level);
            Assert.AreEqual(2, steps.NextTier(start));
            Assert.IsTrue(steps.NextCosts(start), "the first H of a hint-start stage already costs (the HUD marks it)");

            // Off the route: a move the route does not start with.
            State off = null;
            foreach (Dir d in Enum.GetValues(typeof(Dir)))
            {
                var r = Rules.Dispatch(start, Command.Move(d));
                if (!ReferenceEquals(r.State, start) && !plan.TryOnRoute(r.State, out _)) { off = r.State; break; }
            }
            Assume.That(off, Is.Not.Null, "a stage with a move off the route");
            var t = steps.Open(off);
            Assert.AreEqual(2, t.Tier);
            Assert.IsTrue(t.FromStart);
        }

        [Test]
        public void OneBodyStagesGoPrincipleThenRoute()
        {
            var plan = HintPlan.From(Level.Parse("t", "", 1, new[] { "##########", "#S......E#", "##########" }), "RRRRRRR");
            Assert.AreEqual(0, plan.Goals.Count);
            Assert.AreEqual(2, plan.Tiers);
            var steps = new HintSteps(plan, principle: true);
            Assert.AreEqual(HintTier.Shows.Principle, steps.Open(null).What);
            Assert.AreEqual(HintTier.Shows.Route, steps.Open(null).What);
        }
    }
}
