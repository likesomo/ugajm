using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;

namespace SevenSteps.Tests
{
    /// docs/telemetry.md v1 wire format and levelKey (values from server/fixtures/telemetry.golden.json). Owned by the architect.
    public sealed class TelemetryContractTests
    {
        [TestCase(3, new[] { "##########", "#S......E#", "##########" }, "a1070f9a")]
        [TestCase(3, new[] { "#######", "#.....#", "#S..E.#", "#.....#", "#######" }, "98ce5a3e")]
        [TestCase(3, new[] { "#########", "#S....a.#", "#.#######", "#...A.E.#", "#########" }, "64a4ee50")]
        [TestCase(2, new[] { "#########", "#S......#", "#######.#", "#E......#", "#########" }, "13bb7641")]
        public void LevelKeyMatchesTelemetryJs(int bodies, string[] map, string key)
        {
            Assert.AreEqual(key, TelemetryJson.LevelKey(Level.Parse("t", "", bodies, map)));
        }

        [Test]
        public void EventJsonKeepsFieldOrderAndCells()
        {
            var e = new TelemetryEvent(TelemetryNames.Types.Move)
                .With("level", 1).With("body", 1).With("step", 1).With("dir", TelemetryNames.Dir.Right)
                .WithCell("from", 11, 10).WithCell("to", -1, 10).With("result", TelemetryNames.MoveResult.Blocked)
                .With("crumbled", true);
            e.Stamp(2, 0);
            var sb = new StringBuilder();
            TelemetryJson.WriteEvent(sb, e, includeTime: false);
            Assert.AreEqual("{\"seq\":2,\"type\":\"move\",\"level\":1,\"body\":1,\"step\":1,\"dir\":\"right\",\"from\":[1,1],\"to\":null,\"result\":\"blocked\",\"crumbled\":true}", sb.ToString());
        }

        [Test]
        public void BatchHeaderAndEscaping()
        {
            var ctx = new TelemetryContext { PlayerId = "p", SessionId = "s", Client = TelemetryContext.ClientWebGL, Build = "0.1.0+abc1234" };
            var e = new TelemetryEvent(TelemetryNames.Types.LevelStart).With("levelName", "일곱 \"걸음\"");
            e.Stamp(0, 1762500000000);
            string json = TelemetryJson.Batch(ctx, new List<TelemetryEvent> { e });
            Assert.AreEqual("{\"v\":1,\"playerId\":\"p\",\"sessionId\":\"s\",\"client\":\"unity-webgl\",\"build\":\"0.1.0+abc1234\",\"events\":[{\"seq\":0,\"t\":1762500000000,\"type\":\"level_start\",\"levelName\":\"일곱 \\\"걸음\\\"\"}]}", json);
            Assert.AreEqual("http://x/v1/events", new TelemetryContext { Endpoint = "http://x/" }.EventsUrl);
        }

        [Test]
        public void MemoryTelemetryStampsSeqAndRespectsOptOut()
        {
            var t = new MemoryTelemetry();
            t.Track(new TelemetryEvent("a"));
            t.SetEnabled(false);
            t.Track(new TelemetryEvent("b"));
            Assert.AreEqual(1, t.Events.Count);
            Assert.AreEqual(0, t.Events[0].Seq);
        }

        [Test]
        public void LaunchTelemetryUrlAndOff()
        {
            var u = LaunchOptions.Parse(null, "https://g/index.html?telemetry=" + System.Uri.EscapeDataString("http://localhost:8787"));
            Assert.AreEqual("http://localhost:8787", u.TelemetryUrl);
            Assert.IsTrue(LaunchOptions.Parse(new[] { "x", "-telemetry", "off" }, "").TelemetryOff);
        }

        [Test]
        public void AttemptCountsPerStage()
        {
            var p = new ProgressData();
            Assert.AreEqual(1, p.CountStart(4));
            Assert.AreEqual(2, p.CountStart(4));
            Assert.AreEqual(1, p.CountStart(0));
        }
    }
}
