using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Core;

namespace SevenSteps.Tests
{
    /// Stars.Of against core.js stars() (stars.golden.json), and each stage's fewest-bodies solution in one go gets 3.
    public sealed class StarsTests
    {
        static IEnumerable<GoldenStarCase> Cases() => Golden.Stars.cases;
        static IEnumerable<GoldenStarStage> Stages() => Golden.Stars.stages;

        [TestCaseSource(nameof(Cases))]
        public void StarsMatchCoreJs(GoldenStarCase c)
        {
            Assert.AreEqual(c.stars, Stars.Of(c.bodiesUsed, c.minBodies, c.hints, c.undos, c.restarts));
        }

        [TestCaseSource(nameof(Stages))]
        public void FewestBodiesSolutionInOneGoGetsThree(GoldenStarStage g)
        {
            var golden = Golden.Levels.levels.First(l => l.name == g.name);
            var s = Rules.NewState(Golden.ToLevel(new GoldenLevelDef { name = golden.name, hint = golden.hint, bodies = golden.bodies, statueCount = golden.statueCount, map = golden.map }));
            foreach (char c in g.path) s = Rules.Dispatch(s, Golden.ToCommand(c)).State;
            Assert.AreEqual(Status.Win, s.Status);
            Assert.AreEqual(g.bodiesUsed, Stars.BodiesUsed(s), "bodiesUsed");
            Assert.AreEqual(g.stars, Stars.Of(Stars.BodiesUsed(s), g.minBodies, 0, 0, 0));
        }

        [Test]
        public void GoldenCoversEveryStarValue()
        {
            var seen = new HashSet<int>(Golden.Stars.cases.Select(c => c.stars));
            CollectionAssert.IsSubsetOf(new[] { 0, 1, 2, 3 }, seen);
            Assert.AreEqual(Golden.Levels.levels.Length, Golden.Stars.stages.Length, Golden.Regenerate);
        }

        [Test]
        public void Definition()
        {
            Assert.AreEqual(0, Stars.Of(2, 0, 0, 0, 0), "minBodies unknown");
            Assert.AreEqual(1, Stars.Of(2, 2, 1, 0, 0), "hint");
            Assert.AreEqual(1, Stars.Of(3, 2, 0, 0, 0), "more bodies than the fewest");
            Assert.AreEqual(2, Stars.Of(2, 2, 0, 1, 0), "undo");
            Assert.AreEqual(2, Stars.Of(2, 2, 0, 0, 1), "restart");
            Assert.AreEqual(3, Stars.Of(2, 2, 0, 0, 0), "one go");
            Assert.AreEqual(Stars.Max, 3);
        }

        [Test]
        public void BadInputsThrow()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Stars.Of(0, 2, 0, 0, 0), "bodiesUsed 0");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Stars.Of(1, 2, -1, 0, 0), "hints");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Stars.Of(1, 2, 0, -1, 0), "undos");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Stars.Of(1, 2, 0, 0, -1), "restarts");
            Assert.AreEqual(0, Stars.Of(1, -1, 0, 0, 0), "minBodies <= 0 is 'unknown', not an error");
            Assert.AreEqual(3, Stars.Of(1, 1, 0, 0, 0), "smallest valid input");
        }
    }
}
