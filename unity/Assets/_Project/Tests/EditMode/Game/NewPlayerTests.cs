using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Telemetry;

namespace SevenSteps.Tests
{
    /// <summary>
    /// Playtest facilitator link (-newplayer / ?newplayer=1): parsing, what the save keeps and erases, and the telemetry
    /// queue keeping each event's player so the last tester's events never go out under the new id. Owner: gameplay/UI.
    /// </summary>
    public sealed class NewPlayerTests
    {
        static readonly Level Stage = Level.Parse("a", "", 2, new[] { "#S.E#" });

        [Test]
        public void ParsesFromCommandLineAndUrl()
        {
            Assert.IsTrue(LaunchOptions.Parse(new[] { "x.exe", "-newplayer" }, "").NewPlayer);
            Assert.IsTrue(LaunchOptions.Parse(new string[0], "http://h/?newplayer=1").NewPlayer);
            Assert.IsTrue(LaunchOptions.Parse(new string[0], "http://h/?level=3&newplayer=true").NewPlayer);
            var off = LaunchOptions.Parse(new string[0], "http://h/?newplayer=0");
            Assert.IsFalse(off.NewPlayer);
            Assert.AreEqual(0, off.Warnings.Count);
            var bad = LaunchOptions.Parse(new string[0], "http://h/?newplayer=yes");
            Assert.IsFalse(bad.NewPlayer);
            Assert.AreEqual(1, bad.Warnings.Count);
            Assert.IsFalse(LaunchOptions.Parse(new[] { "x.exe" }, "").NewPlayer);
        }

        [Test]
        public void ErasesProgressAndPlayerIdKeepsSettingsAndLanguage()
        {
            var store = new MemoryStore();
            var save = new SaveSystem(store);
            save.Progress.MarkCleared(Stage, 1, 3);
            save.Progress.CountStart(Stage);
            save.SaveProgress();
            save.Settings.playerId = "old-player";
            save.SetSfxVolume(0.3f);
            save.SetLanguage(Language.En);
            save.SetTelemetryChoice(true);

            Assert.IsTrue(NewPlayerLink.Apply(LaunchOptions.Parse(new[] { "x.exe", "-newplayer" }, ""), save));

            var reread = new SaveSystem(store); // what the next run reads from disk
            Assert.IsFalse(reread.Progress.IsCleared(Stage));
            Assert.AreEqual(0, reread.Progress.BestStars(Stage));
            Assert.AreEqual(0, reread.Progress.ClearedCount);
            Assert.AreEqual("", reread.Settings.playerId, "telemetry makes a new id");
            Assert.AreEqual(0.3f, reread.Settings.sfxVolume, 1e-4f);
            Assert.AreEqual(Language.En, reread.Language);
            Assert.IsTrue(reread.TelemetryOn(false), "consent kept");

            Assert.IsFalse(NewPlayerLink.Apply(LaunchOptions.Parse(new[] { "x.exe" }, ""), reread), "no link, nothing erased");
        }

        [Test]
        public void QueuedEventsKeepTheirPlayer()
        {
            var store = new MemoryStore();
            var ctx = new TelemetryContext { PlayerId = "old-player", SessionId = "s1" };
            var t = new HttpTelemetry(ctx, store, _ => 1);
            t.SetEnabled(true);
            t.Track(new TelemetryEvent(TelemetryNames.Types.LevelStart));
            ctx.PlayerId = "new-player";
            ctx.SessionId = "s2";
            t.Track(new TelemetryEvent(TelemetryNames.Types.LevelStart));
            t.SaveQueue(true);

            string[] lines = store.Get(HttpTelemetry.QueueKey).TrimEnd('\n').Split('\n');
            Assert.AreEqual(2, lines.Length);
            StringAssert.StartsWith("old-player\ts1\t{", lines[0]);
            StringAssert.StartsWith("new-player\ts2\t{", lines[1]);

            // Read back by the next run (which already has the new id): the old line keeps the old player.
            var next = new HttpTelemetry(new TelemetryContext { PlayerId = "new-player", SessionId = "s3" }, store, _ => 1);
            next.SetEnabled(true);
            next.SaveQueue(true);
            StringAssert.StartsWith("old-player\ts1\t{", store.Get(HttpTelemetry.QueueKey));
        }

        [Test]
        public void OlderQueueLinesReadAsTheCurrentPlayer()
        {
            var store = new MemoryStore();
            store.Set(HttpTelemetry.QueueKey, "s0\t{\"seq\":0}\n");
            var t = new HttpTelemetry(new TelemetryContext { PlayerId = "p", SessionId = "s1" }, store, _ => 1);
            t.SetEnabled(true);
            Assert.AreEqual(1, t.PendingEvents);
            t.SaveQueue(true);
            Assert.AreEqual("p\ts0\t{\"seq\":0}\n", store.Get(HttpTelemetry.QueueKey));
        }
    }
}
