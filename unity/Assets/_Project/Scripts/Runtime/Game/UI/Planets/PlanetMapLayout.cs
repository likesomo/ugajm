using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenSteps.Game.UI.Planets
{
    /// <summary>
    /// Stage select v4 map layout (pure): where the seven bodies sit (the Big Dipper of the approved v4 map, ladle 1-4,
    /// handle 4-7), the camera framing for an aspect, and where a focus label goes so it stays clear of the trail lines
    /// (v4 fix "label / lit trail"). Reference pixels are the 1280x720 review frame, origin bottom left, y up.
    /// </summary>
    public static class PlanetMapLayout
    {
        public const float RefW = 1280, RefH = 720;
        /// Map camera: orthographic, pitch 50, yaw 0 (IMPLEMENT.md budgets), 13 units half height at 16:9, 30 units back.
        public const float OrthoSize = 13f, Pitch = 50f, Distance = 30f;
        /// Body scale on the map (v4 SpaceBodies: about 180 px across at 1280x720).
        public const float BodyScale = 0.9f;
        /// Centre of the map in world space.
        public static readonly Vector3 Target = new Vector3(0, 0.3f, 0);

        /// v4 map positions (StageSelectReview A): moss, hollow, carved, frost (ladle), joinery, crumble, altar (handle).
        public static readonly Vector2[] Dipper =
        {
            new Vector2(150, 550), new Vector2(150, 290), new Vector2(410, 240), new Vector2(430, 520),
            new Vector2(700, 490), new Vector2(920, 330), new Vector2(1120, 180),
        };

        public static Quaternion Rotation => Quaternion.Euler(Pitch, 0, 0);

        /// Orthographic half height for a camera aspect: 13 at 16:9 and wider, larger for narrower screens so the whole
        /// 1280-wide map stays on screen (4:3, 1280x800).
        public static float OrthoFor(float aspect)
        {
            if (aspect <= 0) throw new ArgumentOutOfRangeException(nameof(aspect), aspect, "aspect");
            float wide = RefW / RefH;
            return aspect >= wide ? OrthoSize : OrthoSize * wide / aspect;
        }

        /// Camera position for a framing target.
        public static Vector3 CameraPosition(Vector3 target) => target - Rotation * Vector3.forward * Distance;

        /// Reference pixel position of planet <paramref name="i"/> of <paramref name="count"/>: the Dipper for up to 7, else a row.
        public static Vector2 Reference(int i, int count)
        {
            if (count <= 0 || i < 0 || i >= count) throw new ArgumentOutOfRangeException(nameof(i), i, $"planet of {count}");
            if (count <= Dipper.Length) return Dipper[i];
            float step = (RefW - 300) / (count - 1);
            return new Vector2(150 + i * step, 360);
        }

        /// World position of a reference pixel on the map plane (through <see cref="Target"/>, facing the camera). Does not
        /// need the camera: the journey return can lay the map out while scene-flow drives the camera.
        public static Vector3 World(Vector2 reference)
        {
            float k = 2 * OrthoSize / RefH;
            var rot = Rotation;
            return Target + rot * Vector3.right * ((reference.x - RefW / 2) * k) + rot * Vector3.up * ((reference.y - RefH / 2) * k);
        }

        /// Where to centre a focus label of <paramref name="size"/> for a body at <paramref name="center"/> with radius
        /// <paramref name="radius"/> (canvas units, y up): under the body first, then beside it (right, left), then above;
        /// the first spot whose box (plus 6 units of air) crosses no trail segment and stays inside <paramref name="bounds"/>.
        /// <paramref name="below"/> is the extra room under the body (the star row). None fits: under the body.
        public static Vector2 LabelSpot(Vector2 center, float radius, Vector2 size, float below, IReadOnlyList<(Vector2 a, Vector2 b)> segments, Rect bounds)
        {
            if (segments == null) throw new ArgumentNullException(nameof(segments));
            var spots = new[]
            {
                center + new Vector2(0, -(radius + below + size.y / 2)),
                center + new Vector2(radius * 0.55f + size.x / 2 + 8, -(radius * 0.75f + size.y / 2)),
                center + new Vector2(-(radius * 0.55f + size.x / 2 + 8), -(radius * 0.75f + size.y / 2)),
                center + new Vector2(0, radius + size.y / 2 + 12),
            };
            foreach (var s in spots)
            {
                var box = new Rect(s - size / 2 - Vector2.one * 6, size + Vector2.one * 12);
                if (box.xMin < bounds.xMin || box.xMax > bounds.xMax || box.yMin < bounds.yMin || box.yMax > bounds.yMax) continue;
                bool hit = false;
                foreach (var (a, b) in segments) if (SegmentHitsRect(a, b, box)) { hit = true; break; }
                if (!hit) return s;
            }
            return spots[0];
        }

        /// The segment a-b touches the rectangle (inside or crossing an edge). Liang-Barsky clipping.
        public static bool SegmentHitsRect(Vector2 a, Vector2 b, Rect r)
        {
            float t0 = 0, t1 = 1;
            var d = b - a;
            bool Clip(float p, float q)
            {
                if (Mathf.Approximately(p, 0)) return q >= 0;
                float t = q / p;
                if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
                return true;
            }
            return Clip(-d.x, a.x - r.xMin) && Clip(d.x, r.xMax - a.x) && Clip(-d.y, a.y - r.yMin) && Clip(d.y, r.yMax - a.y);
        }

        /// The part of a trail line between two bodies that is outside both (canvas units): from a's rim + gap to b's rim
        /// - gap. False when the bodies are too close for a visible line.
        public static bool TrailSegment(Vector2 a, float ra, Vector2 b, float rb, float gap, out Vector2 from, out Vector2 to)
        {
            var d = b - a;
            float len = d.magnitude;
            from = to = a;
            if (len <= ra + rb + 2 * gap + 4) return false;
            var u = d / len;
            from = a + u * (ra + gap);
            to = b - u * (rb + gap);
            return true;
        }
    }
}
