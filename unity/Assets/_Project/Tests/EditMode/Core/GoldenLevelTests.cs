using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SevenSteps.Core;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// Every stage: Level.Parse matches core.js parseLevel, the BFS solver gives the same answers as core.js solve()
    /// for every body count with and without SWITCH, and the shipped levels.json agrees with both.
    public sealed class GoldenLevelTests
    {
        static IEnumerable<GoldenLevel> Levels() => Golden.Levels.levels;

        static LevelFileDto Shipped() =>
            JsonUtility.FromJson<LevelFileDto>(File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Data", "levels.json")));

        [TestCaseSource(nameof(Levels))]
        public void ParseMatchesCoreJs(GoldenLevel g)
        {
            var l = Level.Parse(g.name, g.hint, g.bodies, g.map);
            Assert.AreEqual(g.w, l.W, "w");
            Assert.AreEqual(g.h, l.H, "h");
            Assert.AreEqual(g.start, l.Start, "start");
            Assert.AreEqual(g.bodies, l.Bodies, "bodies");
            var tiles = new StringBuilder(l.Count);
            for (int i = 0; i < l.Count; i++) tiles.Append(l.Tile(i));
            Assert.AreEqual(g.tiles, tiles.ToString(), "tiles");
            foreach (char plate in "abc")
            {
                var want = g.plates.FirstOrDefault(p => p.plate[0] == plate)?.cells ?? new int[0];
                CollectionAssert.AreEqual(want, l.PlatesFor(plate), $"plate {plate}");
            }
        }

        [TestCaseSource(nameof(Levels))]
        public void SolverMatchesCoreJs(GoldenLevel g)
        {
            var l = Level.Parse(g.name, g.hint, g.bodies, g.map);
            foreach (var want in g.solves)
            {
                string where = $"{g.name} bodies={want.bodies} noSwitch={want.noSwitch}";
                var got = Solver.Solve(l, want.bodies, want.noSwitch);
                Assert.AreEqual(want.solvable, got != null, $"{where}: solvable");
                if (got == null) continue;
                Assert.AreEqual(want.path, got.Letters(), $"{where}: path");
                Assert.AreEqual(want.bodiesUsed, got.BodiesUsed, $"{where}: bodies used");
            }
        }

        [TestCaseSource(nameof(Levels))]
        public void MinPathReplaysToWin(GoldenLevel g)
        {
            var s = Rules.NewState(Level.Parse(g.name, g.hint, g.bodies, g.map));
            foreach (char c in g.minPath) s = Rules.Dispatch(s, Golden.ToCommand(c)).State;
            Assert.AreEqual(Status.Win, s.Status, $"{g.name}: {g.minPath}");
            Assert.AreEqual(g.minBodies, g.bodies - s.Bodies + 1, "bodies used");
        }

        /// levels.json is the level designer's export; the golden data is the core's. If a stage changed, both must be regenerated.
        [Test]
        public void ShippedLevelsMatchGolden()
        {
            var shipped = Shipped();
            var golden = Golden.Levels.levels;
            Assert.AreEqual(golden.Length, shipped.levels.Length, $"stage count differs. {Golden.Regenerate}");
            for (int k = 0; k < golden.Length; k++)
            {
                var s = shipped.levels[k];
                var g = golden[k];
                string where = $"stage {k + 1} '{s.name}'";
                Assert.AreEqual(g.name, s.name, $"{where}: name. {Golden.Regenerate}");
                Assert.AreEqual(g.bodies, s.bodies, $"{where}: bodies. {Golden.Regenerate}");
                CollectionAssert.AreEqual(g.map, s.map, $"{where}: map. {Golden.Regenerate}");
                Assert.AreEqual(g.minBodies, s.expected.minBodies, $"{where}: expected.minBodies");
                Assert.AreEqual(g.minMoves, s.expected.moves, $"{where}: expected.moves");
                Assert.AreEqual(g.minPath, s.expected.path, $"{where}: expected.path");
                Assert.AreEqual(g.needsSwitch, s.expected.needsSwitch, $"{where}: expected.needsSwitch");
            }
        }
    }
}
