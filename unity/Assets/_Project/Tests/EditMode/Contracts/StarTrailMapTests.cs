using System;
using NUnit.Framework;
using SevenSteps.Contracts;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// docs/architecture.md 15: the star trail texture layout the view writes and the tile shader reads.
    public sealed class StarTrailMapTests
    {
        [Test]
        public void StepsStatuesAndNewestGoToLayerZero()
        {
            var m = new StarTrailMap(4, 3);
            Assert.AreEqual(8, m.TextureWidth);
            Assert.AreEqual(3, m.TextureHeight);
            Assert.AreEqual(8 * 3 * 4, m.Bytes.Length);
            int cell = 2 * 4 + 1; // x 1, y 2
            m.AddStep(1, cell, 7);
            m.AddStep(1, cell, 3);
            m.AddStep(0, cell, 0); // start footprint: nothing
            m.AddStatue(2, cell);
            m.SetNewest(cell);
            int texel = (2 * 8 + 2 * 1) * 4; // row 2, texel 2x+0
            Assert.AreEqual(0, m.Bytes[texel + 0]);
            Assert.AreEqual((1 << 6) | (1 << 2), m.Bytes[texel + 1]);
            Assert.AreEqual(StarTrailMap.StatueBit, m.Bytes[texel + 2]);
            Assert.AreEqual(StarTrailMap.NewestBit, m.Bytes[texel + 3]);
            Assert.Throws<ArgumentOutOfRangeException>(() => m.AddStep(3, cell, 1), "three bodies at most");
            Assert.Throws<ArgumentOutOfRangeException>(() => m.AddStep(0, cell, 8));
            m.Clear();
            Assert.AreEqual(0, m.Bytes[texel + 1]);
        }

        [Test]
        public void PathsMarkFacingEdgesAndSlides()
        {
            var m = new StarTrailMap(5, 2);
            byte E(int x, int y, int body) => m.Bytes[m.Index(y * 5 + x, 1, body)];
            m.AddPath(0, 0, 3); // slide right across row 0
            Assert.AreEqual(StarTrailMap.EdgeRight, E(0, 0, 0));
            Assert.AreEqual(StarTrailMap.EdgeLeft | StarTrailMap.EdgeRight, E(1, 0, 0));
            Assert.AreEqual(StarTrailMap.EdgeLeft | StarTrailMap.EdgeRight, E(2, 0, 0));
            Assert.AreEqual(StarTrailMap.EdgeLeft, E(3, 0, 0));
            Assert.AreEqual(0, E(4, 0, 0));
            m.AddPath(1, 5 + 4, 4); // up from (4,1) to (4,0)
            Assert.AreEqual(StarTrailMap.EdgeUp, E(4, 1, 1));
            Assert.AreEqual(StarTrailMap.EdgeDown, E(4, 0, 1));
            m.AddPath(2, 7, 7); // mud: same cell
            Assert.AreEqual(0, E(2, 1, 2));
            Assert.Throws<ArgumentException>(() => m.AddPath(0, 0, 6), "not one row or column");
        }

        [Test]
        public void WorldToCellInvertsCellToWorld()
        {
            var grid = new BoardGrid(7, 4, 1.5f, new Vector3(2f, 0f, -3f));
            var m = StarTrailMap.WorldToCell(grid);
            for (int i = 0; i < grid.Count; i++)
            {
                var c = m.MultiplyPoint3x4(grid.CellToWorld(i));
                Assert.AreEqual(i % 7, c.x, 1e-4f);
                Assert.AreEqual(i / 7, c.y, 1e-4f);
            }
            var corner = m.MultiplyPoint3x4(grid.CellToWorld(0) + new Vector3(0.75f, 0.3f, -0.75f));
            Assert.AreEqual(0.5f, corner.x, 1e-4f, "half a cell right");
            Assert.AreEqual(0.5f, corner.y, 1e-4f, "half a cell down the map; height ignored");
        }
        [Test]
        public void EndFlagsAndStateGoToLayerOneAlpha()
        {
            var m = new StarTrailMap(3, 3);
            m.SetEnd(4, StarTrailMap.EndSwitch);
            m.SetEnd(4, StarTrailMap.EndBridge);
            Assert.AreEqual(StarTrailMap.EndSwitch | StarTrailMap.EndBridge, m.Bytes[m.Index(4, 1, 3)]);
            Assert.AreEqual(0, m.Bytes[m.Index(4, 0, 3)], "layer 0 alpha untouched");
            Assert.AreEqual(new Vector4(2, (int)SevenSteps.Core.Status.Win, 1, 0), StarTrailMap.StateVector(2, (int)SevenSteps.Core.Status.Win, 3f));
            Assert.AreEqual(new Vector4(0, 0, 0, 6), StarTrailMap.StateVector(0, 0, 0f, 6), "w = steps used");
            m.SetPlayer(4);
            m.SetNewest(4);
            Assert.AreEqual(StarTrailMap.NewestBit | StarTrailMap.PlayerBit, m.Bytes[m.Index(4, 0, 3)]);
        }

        [Test]
        public void PresetDefaultsAreValidAndRangesCaught()
        {
            Assert.IsEmpty(StarTrailPreset.Default.Validate(), "built-in values pass");
            var p = ScriptableObject.CreateInstance<StarTrailPreset>();
            try
            {
                Assert.IsEmpty(p.Validate());
                p.dotSizeMax = 0.02f; // below dotSizeMin
                p.awakenEmission = 0.5f; // below emission
                p.particleCount = 40;
                p.stoneTint = new Color(1, 0, 0, 0.9f);
                var problems = p.Validate();
                Assert.AreEqual(4, problems.Count, string.Join("; ", problems));
            }
            finally { UnityEngine.Object.DestroyImmediate(p); }
        }
    }
}
