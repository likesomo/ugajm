using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;

namespace SevenSteps.Tests
{
    /// Hints unlock by tries (user request 2026-11-14): tier 1 at 3, tier 2 at 6, tier 3 at 9; counts per stage, saved,
    /// cleared by the progress reset.
    public sealed class HintGateTests
    {
        [Test]
        public void TiersUnlockEveryThreeTries()
        {
            int[] want = { 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 3 };
            for (int t = 0; t < want.Length; t++) Assert.AreEqual(want[t], HintGate.Unlocked(t), $"{t} tries");
            Assert.AreEqual(0, HintGate.Unlocked(-1));
            Assert.AreEqual(3, HintGate.TriesToNext(0));
            Assert.AreEqual(1, HintGate.TriesToNext(2));
            Assert.AreEqual(3, HintGate.TriesToNext(3));
            Assert.AreEqual(1, HintGate.TriesToNext(8));
            Assert.AreEqual(0, HintGate.TriesToNext(9));
        }

        static Level L(string name) => Level.Parse(name, "", 1, new[] { "S.E" }, 0);

        [Test]
        public void TriesArePerStageSavedAndClearedByTheReset()
        {
            var store = new MemoryStore();
            var save = new SaveSystem(store);
            var a = L("a");
            var b = L("b");
            for (int i = 0; i < 4; i++) save.Progress.CountTry(a);
            save.Progress.CountTry(b);
            save.SaveProgress();
            var again = new SaveSystem(store);
            Assert.AreEqual(4, again.Progress.Tries(a));
            Assert.AreEqual(1, again.Progress.Tries(b));
            Assert.AreEqual(1, HintGate.Unlocked(again.Progress.Tries(a)));
            again.ResetProgress();
            Assert.AreEqual(0, again.Progress.Tries(a));
            Assert.AreEqual(0, new SaveSystem(store).Progress.Tries(a), "reset saved");
        }
    }
}
