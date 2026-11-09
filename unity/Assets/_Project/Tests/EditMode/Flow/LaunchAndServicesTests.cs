using System;
using NUnit.Framework;
using SevenSteps.Contracts;

namespace SevenSteps.Tests
{
    /// S1 assertive checks (docs/scene-flow.md 5 S1): launch options parse bad input into Warnings instead of
    /// guessing, are read-only after parsing, and the service map refuses a second registration.
    public sealed class LaunchAndServicesTests
    {
        [SetUp] public void SetUp() => Services.Clear();
        [TearDown] public void TearDown() => Services.Clear();

        [Test]
        public void ServicesRejectDuplicateRegistration()
        {
            Services.Register<ITelemetry>(new NullTelemetry());
            Assert.Throws<InvalidOperationException>(() => Services.Register<ITelemetry>(new NullTelemetry()));
            Assert.Throws<ArgumentNullException>(() => Services.Register<ITelemetry>(null));
        }

        [Test]
        public void GoodLaunchHasNoWarnings()
        {
            var o = LaunchOptions.Parse(new[] { "game.exe", "-level", "8", "-replay", "LRRK", "-telemetry", "off" }, "");
            Assert.AreEqual(8, o.Level);
            Assert.AreEqual("LRRK", o.Replay);
            Assert.IsTrue(o.TelemetryOff);
            CollectionAssert.IsEmpty(o.Warnings);
        }

        [Test]
        public void BadValuesAreWarningsNotGuesses()
        {
            var o = LaunchOptions.Parse(new[] { "game.exe", "-level", "abc", "-replay", "-screenshot", "s.png", "-telemetry", "ftp://x" }, "");
            Assert.AreEqual(0, o.Level);
            Assert.IsFalse(o.DirectToGame);
            Assert.AreEqual("", o.Replay);          // "-replay" followed by another flag has no value
            Assert.AreEqual("s.png", o.Screenshot); // the flag after it still parses
            Assert.IsFalse(o.TelemetryOff);
            Assert.AreEqual("", o.TelemetryUrl);
            Assert.AreEqual(3, o.Warnings.Count, string.Join("\n", o.Warnings));

            var u = LaunchOptions.Parse(null, "https://x/index.html?level=-2&telemetry=maybe");
            Assert.AreEqual(0, u.Level);
            Assert.AreEqual(2, u.Warnings.Count, string.Join("\n", u.Warnings));
        }

        [Test]
        public void SmokeFlagsAreParsedWithoutValues()
        {
            var o = LaunchOptions.Parse(new[] { "game.exe", "-screenshot", "t.png", "-stageselect", "-credits" }, "");
            Assert.IsTrue(o.OpenStageSelect);
            Assert.IsTrue(o.OpenCredits);
            Assert.AreEqual("t.png", o.Screenshot);
            CollectionAssert.IsEmpty(o.Warnings);
            Assert.IsFalse(LaunchOptions.Parse(new[] { "game.exe" }, "").OpenCredits);
        }

        [Test]
        public void ReplayIsTakenOnce()
        {
            var o = LaunchOptions.Parse(null, "https://x/?level=3&replay=DUR");
            Assert.AreEqual("DUR", o.TakeReplay());
            Assert.AreEqual("", o.TakeReplay());
            Assert.IsTrue(o.DirectToGame);
        }

        [Test]
        public void EditorStartFillsOnlyWhatTheLaunchLeftOut()
        {
            var none = LaunchOptions.Parse(new[] { "Unity.exe" }, "");
            Assert.IsTrue(none.UseEditorStart(5, "RRK"));
            Assert.AreEqual(5, none.Level);
            Assert.AreEqual("RRK", none.Replay);

            var given = LaunchOptions.Parse(new[] { "x", "-level", "8", "-replay", "L" }, "");
            Assert.IsFalse(given.UseEditorStart(5, "RRK"));
            Assert.AreEqual(8, given.Level);
            Assert.AreEqual("L", given.Replay);

            Assert.Throws<ArgumentOutOfRangeException>(() => none.UseEditorStart(0, ""));
        }
    }
}
