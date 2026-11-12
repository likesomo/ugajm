using System;
using UnityEngine;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Star trail board data (docs/architecture.md 15): the bytes of the global texture <see cref="MapId"/> that the shared
    /// tile shader (art) reads to draw the brothers' star footprints with no extra draw call. The view (gameplay-ui) fills it
    /// from State on every session update and uploads it; this class only fixes the layout so writer, shader and tests agree.
    ///
    /// Texture: RGBA32, linear (not sRGB), point filter, clamp, no mips, size (2W) x H. Row y = board row y (levels.json row,
    /// 0 = top of the map). Two texels per cell: layer 0 at (2x, y), layer 1 at (2x+1, y).
    /// - Layer 0: R, G, B = body 0, 1, 2 (<see cref="BodyIndex"/>). Bit k-1 (k = 1..7): that body's step k landed on this cell.
    ///   Bit 7: that body's KEEP statue stands here. A: bit 0 = newest step cell (the pulse), bit 1 = the walking body stands here (centre of the ring of seven
    ///   stars, 15.6), other bits 0.
    /// - Layer 1: R, G, B = body 0, 1, 2 path edges: bit 0 toward x+1, bit 1 toward x-1, bit 2 toward y+1 (down the map),
    ///   bit 3 toward y-1. A path between two footprints marks both cells' facing edges, and every cell between them with
    ///   both edges (an ice slide). Other bits 0. A: how a brother ended on this cell (15.5): bit 0 SWITCH baton seam (the
    ///   new start), bit 1 bridge (a pit or crack the brother sealed, State.Bridges: the sunken star), bit 2 fell (the pit),
    ///   bit 3 vanished (a seventh step on the start). Whose: the brother with step 7 (or the last step) on the cell.
    /// </summary>
    public sealed class StarTrailMap
    {
        /// Global shader properties (Shader.SetGlobal*). Unset globals read 0, so the shader draws nothing without the view.
        public const string MapName = "_StarTrailMap";
        /// float4x4: world position → (cx, cy, 0, 1), cell (x, y) centre at integer (x, y), cy grows down the map.
        public const string WorldToCellName = "_StarTrailWorldToCell";
        /// float4 (W, H, enabled 0/1, fade 0..1): fade multiplies every trail (Restart afterglow, 1 = full).
        public const string ParamsName = "_StarTrailParams";
        /// float4 (newest-cell pulse 0..1, seventh-step awaken 0..1, reserved 0 (was the last-step ring pulse, unused since the
        /// 2026-11-12 step sky C), 0), animated by the view.
        public const string PulseName = "_StarTrailPulse";
        /// float4 (current body 0..2, phase 0 play / 1 choose / 2 win / 3 lose, finale 0..1, reserved 0 (was steps used, unused since
        /// the 2026-11-12 step sky C)): the trail state machine.
        public const string StateName = "_StarTrailState";
        /// float4[3] linear colours of body 0, 1, 2 (the HUD brother colours).
        public const string ColorsName = "_StarTrailColors";

        public static readonly int MapId = Shader.PropertyToID(MapName);
        public static readonly int WorldToCellId = Shader.PropertyToID(WorldToCellName);
        public static readonly int ParamsId = Shader.PropertyToID(ParamsName);
        public static readonly int PulseId = Shader.PropertyToID(PulseName);
        public static readonly int ColorsId = Shader.PropertyToID(ColorsName);
        public static readonly int StateId = Shader.PropertyToID(StateName);

        public const int MaxBodies = 3, Steps = 7;
        /// Layer 0 A: NewestBit = newest step cell. ReservedBit (was the walking body's cell for the ring at the feet) stays 0
        /// and is not reused (2026-11-12 step sky C, architect review bf5dfac).
        public const byte StatueBit = 1 << 7, NewestBit = 1, ReservedBit = 2;
        public const byte EdgeRight = 1, EdgeLeft = 2, EdgeDown = 4, EdgeUp = 8;
        public const byte EndSwitch = 1, EndBridge = 2, EndFell = 4, EndVanish = 8;

        public readonly int W, H;
        /// RGBA32 pixel data, row-major from row 0, 4 bytes per texel: what Texture2D.SetPixelData takes.
        public readonly byte[] Bytes;

        public StarTrailMap(int w, int h)
        {
            if (w <= 0 || h <= 0) throw new ArgumentOutOfRangeException(w <= 0 ? nameof(w) : nameof(h));
            W = w; H = h;
            Bytes = new byte[2 * w * h * 4];
        }

        public int TextureWidth => 2 * W;
        public int TextureHeight => H;

        public void Clear() => Array.Clear(Bytes, 0, Bytes.Length);

        /// Byte index of channel c (0 R .. 3 A) of a cell's layer.
        public int Index(int cell, int layer, int c)
        {
            if (cell < 0 || cell >= W * H) throw new ArgumentOutOfRangeException(nameof(cell));
            int x = cell % W, y = cell / W;
            return ((y * 2 * W) + 2 * x + layer) * 4 + c;
        }

        /// Body <paramref name="body"/>'s step <paramref name="step"/> (1..7) on <paramref name="cell"/>. Step 0 (the start
        /// footprint) is ignored.
        public void AddStep(int body, int cell, int step)
        {
            CheckBody(body);
            if (step < 0 || step > Steps) throw new ArgumentOutOfRangeException(nameof(step));
            if (step == 0) return;
            Bytes[Index(cell, 0, body)] |= (byte)(1 << (step - 1));
        }

        public void AddStatue(int body, int cell)
        {
            CheckBody(body);
            Bytes[Index(cell, 0, body)] |= StatueBit;
        }

        public void SetNewest(int cell) => Bytes[Index(cell, 0, 3)] |= NewestBit;

        /// How a brother ended on <paramref name="cell"/>: <see cref="EndSwitch"/>, <see cref="EndBridge"/>, <see cref="EndFell"/>, <see cref="EndVanish"/>.
        public void SetEnd(int cell, byte end) => Bytes[Index(cell, 1, 3)] |= end;

        /// Value for <see cref="StateName"/>. Phase: 0 play, 1 choose, 2 win, 3 lose (Core Status order).
        public static Vector4 StateVector(int currentBody, int phase, float finale) =>
            new Vector4(currentBody, phase, Mathf.Clamp01(finale), 0f);

        /// The path of body <paramref name="body"/> from one footprint to the next (same row or column).
        public void AddPath(int body, int fromCell, int toCell)
        {
            CheckBody(body);
            if (fromCell == toCell) return; // mud: a second step on the same cell
            int fx = fromCell % W, fy = fromCell / W, tx = toCell % W, ty = toCell / W;
            if (fx != tx && fy != ty) throw new ArgumentException($"path {fromCell}→{toCell} is not on one row or column");
            int dx = Math.Sign(tx - fx), dy = Math.Sign(ty - fy);
            byte forward = dx > 0 ? EdgeRight : dx < 0 ? EdgeLeft : dy > 0 ? EdgeDown : EdgeUp;
            byte back = dx > 0 ? EdgeLeft : dx < 0 ? EdgeRight : dy > 0 ? EdgeUp : EdgeDown;
            int x = fx, y = fy;
            Bytes[Index(y * W + x, 1, body)] |= forward;
            while (true)
            {
                x += dx; y += dy;
                int i = Index(y * W + x, 1, body);
                Bytes[i] |= back;
                if (x == tx && y == ty) break;
                Bytes[i] |= forward;
            }
        }

        /// Value for <see cref="WorldToCellName"/>: inverse of BoardGrid.CellToWorld on the board plane.
        public static Matrix4x4 WorldToCell(BoardGrid grid)
        {
            float k = 1f / grid.CellSize;
            var m = Matrix4x4.zero;
            m.m00 = k; m.m03 = -grid.Origin.x * k + (grid.W - 1) / 2f;
            m.m12 = -k; m.m13 = grid.Origin.z * k + (grid.H - 1) / 2f;
            m.m33 = 1f;
            return m;
        }

        static void CheckBody(int body)
        {
            if (body < 0 || body >= MaxBodies) throw new ArgumentOutOfRangeException(nameof(body), $"0..{MaxBodies - 1}");
        }
    }
}
