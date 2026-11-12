using NUnit.Framework;
using SevenSteps.Contracts;

namespace SevenSteps.Tests
{
    /// U5 UI toggle (docs/ui-u5-kit.md 2): -ui5 / -ui4, ?ui5=1|0, default off.
    public sealed class UiStyleToggleTests
    {
        [Test]
        public void DefaultIsTheBuildDefault()
        {
            var o = LaunchOptions.Parse(new[] { "x.exe" }, "");
            Assert.IsNull(o.Ui5);
            Assert.AreEqual(UiStyleToggle.DefaultOn, UiStyleToggle.U5(o));
            Assert.IsTrue(UiStyleToggle.DefaultOn, "U5 on for the 11/13 playtest build (user decision 2026-11-12-b); -ui4 / ?ui5=0 keep the old UI");
        }

        [Test]
        public void CommandLineAndUrlTurnItOnOrOff()
        {
            Assert.IsTrue(UiStyleToggle.U5(LaunchOptions.Parse(new[] { "x.exe", "-ui5" }, "")));
            Assert.IsFalse(UiStyleToggle.U5(LaunchOptions.Parse(new[] { "x.exe", "-ui4" }, "")));
            Assert.IsTrue(UiStyleToggle.U5(LaunchOptions.Parse(null, "http://h/?ui5=1")));
            Assert.IsTrue(UiStyleToggle.U5(LaunchOptions.Parse(null, "http://h/?ui5=true")));
            Assert.AreEqual(false, LaunchOptions.Parse(null, "http://h/?ui5=0").Ui5);
        }

        [Test]
        public void BadUrlValueWarnsAndKeepsTheDefault()
        {
            var o = LaunchOptions.Parse(null, "http://h/?ui5=maybe");
            Assert.IsNull(o.Ui5);
            Assert.IsNotEmpty(o.Warnings);
        }

        [Test]
        public void NullLaunchThrows() =>
            Assert.Throws<System.ArgumentNullException>(() => UiStyleToggle.U5(null));
    }
}
