using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// BoardView.Fit (docs/architecture.md 14.6): the padded board with its actors fills HudView.BoardViewport on its
    /// binding axis and is centred in it, so no margin is lost above the board to the actor height.
    public sealed class BoardFitTests
    {
        static readonly Vector2Int[] Boards = { new Vector2Int(8, 6), new Vector2Int(12, 5), new Vector2Int(10, 3), new Vector2Int(7, 7) };

        static CameraRig Rig(float pitch, float padding) =>
            new CameraRig { orthographic = true, pitch = pitch, yaw = 0f, fieldOfView = 30f, distance = 30f, padding = padding };

        /// Viewport box of the padded board corners at floor and actor height.
        static Rect Projected(Camera cam, BoardGrid grid, float padding)
        {
            var b = grid.Bounds;
            float pad = padding * grid.CellSize, top = BoardView.ActorHeight * grid.CellSize;
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            for (int k = 0; k < 8; k++)
            {
                var p = b.center + new Vector3((k & 1) == 0 ? -b.extents.x - pad : b.extents.x + pad, (k & 2) == 0 ? 0 : top,
                    (k & 4) == 0 ? -b.extents.z - pad : b.extents.z + pad);
                Vector2 v = cam.WorldToViewportPoint(p);
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [Test]
        public void FillsAndCentresTheFrame([Values(50f, 58f)] float pitch, [Values(0.15f, 0.7f)] float padding,
            [Values(16f / 9f, 16f / 10f)] float aspect)
        {
            var go = new GameObject("BoardFitTests Camera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.aspect = aspect;
                var frame = HudView.BoardViewport(aspect);
                foreach (var size in Boards)
                {
                    var grid = new BoardGrid(size.x, size.y);
                    BoardView.Fit(cam, grid, Rig(pitch, padding));
                    var box = Projected(cam, grid, padding);
                    string at = $"{size.x}x{size.y}";
                    Assert.AreEqual(frame.center.x, box.center.x, 1e-3f, at + " centred across");
                    Assert.AreEqual(frame.center.y, box.center.y, 1e-3f, at + " centred up/down");
                    Assert.AreEqual(1f, Mathf.Max(box.width / frame.width, box.height / frame.height), 1e-3f, at + " fills the binding axis");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void EightBySixCellsGrowFromTheOldSymmetricFit()
        {
            var go = new GameObject("BoardFitTests Camera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.aspect = 16f / 9f;
                BoardView.Fit(cam, new BoardGrid(8, 6), Rig(50f, 0.7f));
                // Symmetric half-height (before): orthographic size 6.52, 55.2 px cells at 720. The min/max box: 5.82, 61.8 px.
                Assert.AreEqual(61.8f, 720f / (2f * cam.orthographicSize), 0.1f);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
