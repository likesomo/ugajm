using SevenSteps.Core;
using UnityEngine;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Cell index &lt;-&gt; world position. Same convention as v0: cell (x, y) at world (x, 0, -y) around the board center,
    /// so map-up is +Z. Top-down and quarter view share this grid; only the camera turns.
    /// Allocation-free, safe to call every frame.
    /// </summary>
    public readonly struct BoardGrid
    {
        public readonly int W;
        public readonly int H;
        public readonly float CellSize;
        public readonly Vector3 Origin;

        public BoardGrid(int w, int h, float cellSize = 1f, Vector3 origin = default)
        {
            W = w; H = h; CellSize = cellSize; Origin = origin;
        }

        public BoardGrid(Level level, float cellSize = 1f) : this(level.W, level.H, cellSize) { }

        public int Count => W * H;
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;

        /// Center of the top surface of cell i (the cell anchor).
        public Vector3 CellToWorld(int i) => CellToWorld(i % W, i / W);

        public Vector3 CellToWorld(int x, int y) =>
            Origin + new Vector3((x - (W - 1) / 2f) * CellSize, 0f, ((H - 1) / 2f - y) * CellSize);

        /// Cell under a point on the board plane, or -1.
        public int WorldToCell(Vector3 world)
        {
            var p = world - Origin;
            int x = Mathf.RoundToInt(p.x / CellSize + (W - 1) / 2f);
            int y = Mathf.RoundToInt((H - 1) / 2f - p.z / CellSize);
            return InBounds(x, y) ? y * W + x : -1;
        }

        /// Cell hit by a screen ray (mouse/touch), or -1. Works for any camera angle.
        public int RayToCell(Ray ray)
        {
            var plane = new Plane(Vector3.up, Origin);
            return plane.Raycast(ray, out float d) ? WorldToCell(ray.GetPoint(d)) : -1;
        }

        /// Board top surface in world space (for camera framing).
        public Bounds Bounds => new Bounds(Origin, new Vector3(W * CellSize, 0f, H * CellSize));
    }
}
