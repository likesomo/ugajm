using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Core;

namespace SevenSteps.Tests
{
    /// Stars.Of against core.js stars() (stars.golden.json): every input case, and each stage's fewest-bodies and
    /// every-brother solutions replayed to a win.
    public sealed class StarsTests
    {
        static IEnumerable<GoldenStarCase> Cases() => Golden.Stars.cases;
        static IEnumerable<GoldenStarStage> Stages() => Golden.Stars.stages;

        [TestCaseSource(nameof(Cases))]
        public void StarsMatchCoreJs(GoldenStarCase c)
        {
            Assert.AreEqual(c.stars, Stars.Of(c.bodiesUsed, c.bodies, c.hints));
        }

        [TestCaseSource(nameof(Stages))]
        public void StageSolutionStarsMatchCoreJs(GoldenStarStage g)
        {
            var golden = Golden.Levels.levels.First(l => l.name == g.name);
            var s = Rules.NewState(Golden.ToLevel(new GoldenLevelDef { name = golden.name, hint = golden.hint, bodies = golden.bodies, statueCount = golden.statueCount, map = golden.map }));
            foreach (char c in g.path) s = Rules.Dispatch(s, Golden.ToCommand(c)).State;
            Assert.AreEqual(Status.Win, s.Status);
            Assert.AreEqual(g.bodies, s.Level.Bodies, "bodies");
            Assert.AreEqual(g.bodiesUsed, Stars.BodiesUsed(s), "bodiesUsed");
            Assert.AreEqual(g.stars, Stars.Of(Stars.BodiesUsed(s), s.Level.Bodies, 0));
        }

        [Test]
        public void GoldenCoversEveryStarValueAndStage()
        {
            var seen = new HashSet<int>(Golden.Stars.cases.Select(c => c.stars));
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, seen);
            var names = new HashSet<string>(Golden.Stars.stages.Select(g => g.name));
            Assert.AreEqual(Golden.Levels.levels.Length, names.Count, Golden.Regenerate);
        }

        [Test]
        public void Definition()
        {
            Assert.AreEqual(1, Stars.Of(3, 3, 1), "hint");
            Assert.AreEqual(2, Stars.Of(2, 3, 0), "not every brother");
            Assert.AreEqual(3, Stars.Of(3, 3, 0), "every brother, no hint");
            Assert.AreEqual(3, Stars.Of(1, 1, 0), "smallest valid input");
            Assert.AreEqual(Stars.Max, 3);
        }

        [Test]
        public void BadInputsThrow()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Stars.Of(0, 2, 0), "bodiesUsed 0");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Stars.Of(3, 2, 0), "bodiesUsed > bodies");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Stars.Of(1, 0, 0), "bodies 0");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Stars.Of(1, 2, -1), "hints");
        }
    }
}
