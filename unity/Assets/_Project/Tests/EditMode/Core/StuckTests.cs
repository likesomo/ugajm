using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Core;

namespace SevenSteps.Tests
{
    /// Rules.IsStuck (user report 2026-11-14): stage 44 "진흙과 숫자" has play states where every direction bumps (mud with one
    /// step left, walls, gates); the game then says Undo / Restart (msg.stuck). The rules themselves do not change.
    public sealed class StuckTests
    {
        static Level Stage44()
        {
            var g = Golden.Levels.levels.First(l => l.name == "진흙과 숫자");
            return Level.Parse(g.name, g.hint, g.bodies, g.map, g.statueCount);
        }

        /// Every reachable state (BFS over Rules.Actions, as the solver walks), until <paramref name="limit"/>.
        static IEnumerable<State> Reachable(Level level, int limit = 200000)
        {
            var start = Rules.NewState(level);
            var seen = new HashSet<string> { Rules.Key(start) };
            var queue = new Queue<State>();
            queue.Enqueue(start);
            while (queue.Count > 0 && seen.Count < limit)
            {
                var s = queue.Dequeue();
                yield return s;
                foreach (var a in Rules.Actions(s))
                {
                    var next = Rules.Apply(s, a).State;
                    if (ReferenceEquals(next, s) || next.Status != Status.Play && next.Status != Status.Choose) continue;
                    if (seen.Add(Rules.Key(next))) queue.Enqueue(next);
                }
            }
        }

        [Test]
        public void Stage44HasStuckStatesAndEveryMoveBumpsThere()
        {
            var stuck = Reachable(Stage44()).Where(Rules.IsStuck).Take(20).ToList();
            Assert.IsNotEmpty(stuck, "stage 44 has no-legal-move states (core.js BFS found 57)");
            foreach (var s in stuck)
            {
                Assert.AreEqual(Status.Play, s.Status);
                for (var d = Dir.Up; d <= Dir.Right; d++)
                {
                    var r = Rules.Step(s, d);
                    Assert.AreSame(s, r.State, $"{d} must not move from a stuck state");
                    Assert.IsTrue(r.Has(GameEventType.Bump), $"{d} bumps");
                }
            }
        }

        [Test]
        public void IsStuckAgreesWithStepOnEveryStage44State()
        {
            foreach (var s in Reachable(Stage44(), 20000))
            {
                bool anyMove = s.Status == Status.Play
                    && Enumerable.Range(0, 4).Any(d => !ReferenceEquals(Rules.Step(s, (Dir)d).State, s));
                Assert.AreEqual(s.Status == Status.Play && !anyMove, Rules.IsStuck(s), Rules.Key(s));
            }
        }

        [Test]
        public void StartAndNonPlayStatesAreNotStuck()
        {
            var level = Stage44();
            Assert.IsFalse(Rules.IsStuck(Rules.NewState(level)));
            Assert.Throws<System.ArgumentNullException>(() => Rules.IsStuck(null));
        }
    }
}
