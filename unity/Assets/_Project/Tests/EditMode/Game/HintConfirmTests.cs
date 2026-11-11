using NUnit.Framework;
using SevenSteps.Game;

namespace SevenSteps.Tests
{
    /// Blind QA-2: the first hint that costs in an attempt asks first (HintRules.AsksFirst, BoardController.ShowHint).
    public sealed class HintConfirmTests
    {
        [Test]
        public void TheFirstCostingHintOfAnAttemptOnlyWarns()
        {
            Assert.IsTrue(HintRules.AsksFirst(costs: true, hintsSoFar: 0, warned: false), "first press: the warning");
            Assert.IsFalse(HintRules.AsksFirst(costs: true, hintsSoFar: 0, warned: true), "second press: the hint opens");
            Assert.IsFalse(HintRules.AsksFirst(costs: true, hintsSoFar: 1, warned: false), "already capped: no more warnings");
            Assert.IsFalse(HintRules.AsksFirst(costs: false, hintsSoFar: 0, warned: false), "a free tier opens at once");
        }
    }
}
