using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Core;

namespace SevenSteps.Tests
{
    /// Replays every golden trace (stage solutions, random walks with undo/restart, hand-made edge cases) through
    /// Rules.Dispatch and checks the state and events after each command are exactly what core.js produced.
    public sealed class GoldenTraceTests
    {
        static IEnumerable<GoldenTrace> Traces() => Golden.Traces.traces;

        [Test]
        public void GoldenTracesExist()
        {
            Assert.AreEqual(Rules.Steps, Golden.Traces.steps, "core.js and C# disagree on the step count");
            Assert.Greater(Golden.Traces.traces.Length, 0, Golden.Regenerate);
        }

        [TestCaseSource(nameof(Traces))]
        public void ReplayMatchesCoreJs(GoldenTrace t)
        {
            var s = Rules.NewState(Golden.ToLevel(t.level));
            Assert.AreEqual(Golden.Describe(t.initial), Golden.Describe(s), $"{t.id}: initial state");
            Assert.AreEqual(t.commands.Length, t.steps.Length, $"{t.id}: malformed trace");

            for (int k = 0; k < t.steps.Length; k++)
            {
                var g = t.steps[k];
                string where = $"{t.id} command #{k} '{g.cmd}' (commands so far: {t.commands.Substring(0, k + 1)})";
                var r = Rules.Dispatch(s, Golden.ToCommand(g.cmd[0]));
                Assert.AreEqual(Golden.Describe(g.events), Golden.Describe(r.Events), $"{where}: events");
                Assert.AreEqual(g.changed, !ReferenceEquals(r.State, s), $"{where}: state replaced");
                Assert.AreEqual(Golden.Describe(g.state), Golden.Describe(r.State), $"{where}: state");
                s = r.State;
            }
        }
    }
}
