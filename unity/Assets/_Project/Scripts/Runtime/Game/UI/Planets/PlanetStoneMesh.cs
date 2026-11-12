using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenSteps.Game.UI.Planets
{
    /// <summary>
    /// The v4 stone planet bodies (concepts/ui/stage-select-2026-11-11/v4, user approved 2026-11-11) built at run time: a
    /// four-times subdivided icosahedron (5,120 crust triangles) displaced and vertex-coloured per world, plus each world's
    /// small details in the same mesh. Ported from the editor-only review (feat/art-stages 8c6e9b3, PlanetStoneV4.cs) with the
    /// v4b polish as the default (dd6592c PolishV4b: carved ledges + three glyphs, crumble terraces + eaves, altar stones
    /// and sarsen colour); the shapes and colours are the same numbers. Used when a <see cref="PlanetBodySet"/> entry has no authored mesh, so an art
    /// mesh (v4b) replaces it by setting the asset only.
    /// Local space: centre at the origin, radius <see cref="Radius"/> (y squashed by <see cref="Squash"/>). A seated body
    /// has its summit cut flat at <see cref="SeatCut"/> inside |x| &lt; 2.72, |z| &lt; 1.12 so a board sits within it.
    /// </summary>
    public static class PlanetStoneMesh
    {
        public const float Radius = 3.65f, Squash = 0.88f, SeatCut = 2.25f, SeatTop = 2.30f;
        public const int MinTriangles = 4000, MaxTriangles = 6000;
        /// The crust comes first in every look's mesh (the subdivided icosahedron); the details follow it.
        public const int CrustTriangles = 5120;
        /// The review's world order (chapters.json): the look index of a chapter key.
        public static readonly string[] Looks = { "moss", "hollow", "carved", "frost", "joinery", "crumble", "altar" };

        static readonly Dictionary<(int, bool), Mesh> cache = new Dictionary<(int, bool), Mesh>();

        /// The look index for a chapter key; keys outside <see cref="Looks"/> take a look by their position (wraps).
        public static int LookOf(string key, int fallbackIndex)
        {
            int i = Array.IndexOf(Looks, key);
            if (i >= 0) return i;
            if (fallbackIndex < 0) throw new ArgumentOutOfRangeException(nameof(fallbackIndex));
            return fallbackIndex % Looks.Length;
        }

        /// The shared mesh of a look (built once per run and kept; do not destroy it).
        public static Mesh Get(int look, bool seated)
        {
            if (look < 0 || look >= Looks.Length) throw new ArgumentOutOfRangeException(nameof(look), look, "PlanetStoneMesh: look is 0..6");
            if (cache.TryGetValue((look, seated), out var m) && m != null) return m;
            m = Build(look, seated);
            cache[(look, seated)] = m;
            return m;
        }

        /// Crust point for a unit direction (local space), the same displacement as the mesh: surface places sit on it.
        public static Vector3 SurfacePoint(int look, Vector3 direction, bool seated) => StonePoint(look, direction.normalized, seated);

        // ---- mesh building (port of PlanetStoneV4 / PlanetBodyStudy) ----

        sealed class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<int> t = new List<int>();
            public readonly List<Color> c = new List<Color>();

            public void Tri(Vector3 a, Vector3 b, Vector3 d, Color color)
            {
                int n = v.Count;
                v.Add(a); v.Add(b); v.Add(d);
                t.Add(n); t.Add(n + 1); t.Add(n + 2);
                var col = Lin(color);
                c.Add(col); c.Add(col); c.Add(col);
            }

            public void Box(Vector3 p, Vector3 size, Color color, Quaternion rotation)
            {
                var q = new Vector3[8];
                for (int k = 0; k < 8; k++)
                    q[k] = p + rotation * Vector3.Scale(size, new Vector3((k & 1) == 0 ? -.5f : .5f, (k & 2) == 0 ? -.5f : .5f, (k & 4) == 0 ? -.5f : .5f));
                int[] f = { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5 };
                for (int k = 0; k < f.Length; k += 3) Tri(q[f[k]], q[f[k + 1]], q[f[k + 2]], color);
            }

            public void Disc(Vector3 p, Vector3 normal, float radius, Color color, int sides)
            {
                var r = Quaternion.FromToRotation(Vector3.up, normal);
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                    Tri(p, p + r * new Vector3(Mathf.Cos(b) * radius, 0, Mathf.Sin(b) * radius), p + r * new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius), color);
                }
            }

            public void Crystal(Vector3 p, float height, Color color)
            {
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 2 / 5, b = (i + 1) * Mathf.PI * 2 / 5;
                    Tri(p + new Vector3(Mathf.Cos(a) * .22f, 0, Mathf.Sin(a) * .22f), p + Vector3.up * height, p + new Vector3(Mathf.Cos(b) * .22f, 0, Mathf.Sin(b) * .22f), color * (.8f + i * .04f));
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (v.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(v);
                mesh.SetTriangles(t, 0);
                mesh.SetColors(c);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        internal static Color Lin(Color color) => QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;

        internal static Color Hex(string h)
        {
            if (!ColorUtility.TryParseHtmlString("#" + h, out var c)) throw new ArgumentException("bad colour " + h);
            return c;
        }

        static float Noise(Vector3 n, float scale) => Mathf.PerlinNoise(n.x * scale + n.z * scale * .37f + 19, n.y * scale + n.z * scale * .71f + 31);

        static Vector3 Pore(int i, int count)
        {
            float y = 1 - 2 * (i + .5f) / count;
            float a = i * 2.39996323f;
            return new Vector3(Mathf.Sqrt(1 - y * y) * Mathf.Cos(a), y, Mathf.Sqrt(1 - y * y) * Mathf.Sin(a));
        }

        static float Pit(Vector3 n, int count, out float rim)
        {
            float depth = 0;
            rim = 0;
            for (int i = 0; i < count; i++)
            {
                float r = .065f + (i % 5) * .018f;
                float d = Vector3.Distance(n, Pore(i, count)) / r;
                if (d < 1) depth = Mathf.Max(depth, Mathf.Pow(1 - d * d, 1.2f));
                if (d > .82f && d < 1.18f) rim = Mathf.Max(rim, 1 - Mathf.Abs(d - 1) * 5);
            }
            return depth;
        }

        static Vector3 StonePoint(int p, Vector3 n, bool seat)
        {
            float grain = (Noise(n, 17) - .5f) * .055f + (Noise(n, 5) - .5f) * .09f;
            float radius = Radius + grain;
            if (p == 1)
            {
                radius -= Pit(n, 100, out var rim) * .34f;
                radius += rim * .035f;
                float d = Vector3.Distance(n, new Vector3(0, .64f, -.77f).normalized);
                if (d < .48f) radius -= .85f * Mathf.Pow(1 - d / .48f, 1.2f);
            }
            if (p == 2) // v4b: stepped sandstone ledges
            {
                float ledge = Mathf.Repeat(n.y * 6, 1);
                radius = Radius + (Noise(n, 6) - .5f) * .025f + .18f * (1 - Mathf.SmoothStep(.05f, .38f, ledge)) - .02f;
            }
            if (p == 3) radius += Mathf.Sin(n.x * 29 + n.z * 17) * .015f;
            if (p == 4) radius -= Mathf.Pow(1 - Mathf.Abs(Mathf.Sin(n.x * 16 + n.z * 7)), 14) * .035f;
            if (p == 5) // v4b: terraces with eaves, pores, erosion channels
            {
                float phase = Mathf.Repeat(n.y * 5.5f + .045f * Mathf.Sin(Mathf.Atan2(n.z, n.x) * 13), 1);
                radius = Radius + .15f * (1 - Mathf.SmoothStep(.10f, .42f, phase)) - Pit(n, 65, out _) * .20f;
                radius -= Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.Atan2(n.z, n.x) * 17 + n.y * 1.8f)), 10) * .14f;
            }
            var q = Vector3.Scale(n * radius, new Vector3(1, Squash, 1));
            // The review seated only moss (the one dived into); production seats every world the same way.
            if (seat && Mathf.Abs(q.x) < 2.72f && Mathf.Abs(q.z) < 1.12f && q.y > SeatCut) q.y = SeatCut;
            return q;
        }

        static Color StoneColor(int p, Vector3 n)
        {
            float grain = Noise(n, 35), broad = Noise(n, 5), mid = Noise(n, 13);
            Color color;
            switch (p)
            {
                case 0:
                    color = Color.Lerp(Hex("77786F"), Hex("C1B9A2"), grain);
                    if (broad > .54f) color = Color.Lerp(Hex("616836"), Hex("929345"), mid);
                    else if (grain > .83f) color = Hex("DDD4BC");
                    break;
                case 1:
                    float pit = Pit(n, 100, out var rim);
                    color = Color.Lerp(Hex("6C6057"), Hex("292A29"), pit);
                    color = Color.Lerp(color, Hex("95816A"), rim * .3f);
                    if (Vector3.Distance(n, new Vector3(0, .64f, -.77f).normalized) < .29f) color = Hex("22262A");
                    break;
                case 2: // v4b ledges: shadowed riser, lit tread
                    float ledgePhase = Mathf.Repeat(n.y * 6, 1);
                    color = Color.Lerp(Hex("9A4E35"), Hex("CB8056"), .40f + grain * .15f);
                    if (ledgePhase < .13f) color *= .58f;
                    else if (ledgePhase < .33f) color = Hex("DD996A");
                    break;
                case 3:
                    float stream = Mathf.Abs(n.z + .32f * Mathf.Sin(n.x * 4));
                    color = Color.Lerp(Hex("C6D0CD"), Hex("F0EEE0"), grain);
                    if (stream < .16f) color = Color.Lerp(Hex("79AEC5"), Hex("BEDCE2"), stream / .16f);
                    break;
                case 4:
                    float plate = Mathf.Abs(Mathf.Sin(n.x * 15 + n.z * 6) * Mathf.Sin(n.y * 13 - n.z * 8));
                    color = Color.Lerp(Hex("343F49"), Hex("687079"), grain);
                    if (plate < .06f) color *= .56f;
                    float strap = Mathf.Min(Mathf.Abs(n.y - .36f), Mathf.Abs(n.y + .36f));
                    float diagonal = Mathf.Abs(Mathf.Sin(Mathf.Atan2(n.z, n.x) * 2 + n.y * 2));
                    if (strap < .07f || diagonal < .07f) color = Hex("99703E");
                    if ((strap > .048f && strap < .073f) || (diagonal > .048f && diagonal < .078f)) color = Hex("C4A05B");
                    break;
                case 5: // v4b terraces
                    float phase = Mathf.Repeat(n.y * 5.5f + .045f * Mathf.Sin(Mathf.Atan2(n.z, n.x) * 13), 1);
                    color = Color.Lerp(Hex("B49A73"), Hex("E7D2AB"), grain);
                    if (phase < .16f) color *= .64f;
                    else if (phase < .36f) color = Hex("EAD9B5");
                    color *= 1 - Pit(n, 65, out _) * .36f;
                    break;
                default: // v4b sarsen
                    color = Color.Lerp(Hex("88887C"), Hex("BDB29A"), grain);
                    if (broad > .61f) color = Color.Lerp(Hex("9C914A"), Hex("B0A35A"), mid);
                    break;
            }
            color.a = 1;
            return color;
        }

        static Mesh Build(int p, bool seat)
        {
            var m = new Builder();
            float g = (1 + Mathf.Sqrt(5)) / 2;
            var points = new[]
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0), new Vector3(0, -1, g), new Vector3(0, 1, g),
                new Vector3(0, -1, -g), new Vector3(0, 1, -g), new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1),
            };
            int[] faces = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            void Crust(Vector3 a, Vector3 b, Vector3 d)
            {
                int start = m.v.Count;
                foreach (var n in new[] { a, b, d })
                {
                    m.v.Add(StonePoint(p, n, seat));
                    m.c.Add(Lin(StoneColor(p, n)));
                }
                m.t.Add(start); m.t.Add(start + 1); m.t.Add(start + 2);
            }
            void Sub(Vector3 a, Vector3 b, Vector3 d, int depth)
            {
                if (depth == 0) { Crust(a, b, d); return; }
                var ab = (a + b).normalized; var bd = (b + d).normalized; var da = (d + a).normalized;
                Sub(a, ab, da, depth - 1); Sub(b, bd, ab, depth - 1); Sub(d, da, bd, depth - 1); Sub(ab, bd, da, depth - 1);
            }
            for (int i = 0; i < faces.Length; i += 3) Sub(points[faces[i]].normalized, points[faces[i + 1]].normalized, points[faces[i + 2]].normalized, 4);

            if (p == 0) // 24 small five-petal white flowers on the moss
            {
                int flowers = 0;
                for (int i = 0; i < 160 && flowers < 24; i++)
                {
                    var n = Pore(i, 160);
                    if (Noise(n, 5) < .55f || n.y < -.15f) continue;
                    var pos = StonePoint(p, n, seat) + n * .025f;
                    var rot = Quaternion.FromToRotation(Vector3.up, n);
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k * Mathf.PI * 2 / 5;
                        m.Disc(pos + rot * new Vector3(Mathf.Cos(a) * .047f, 0, Mathf.Sin(a) * .047f), n, .033f, Hex("F3EEDC"), 4);
                    }
                    m.Disc(pos + n * .004f, n, .018f, Hex("C4AE64"), 5);
                    flowers++;
                }
            }
            if (p == 4) // brass rivets and wood straps with rails
            {
                for (int row = 0; row < 2; row++)
                    for (int k = 0; k < 16; k++)
                    {
                        float a = k * Mathf.PI / 8, y = row == 0 ? .36f : -.36f;
                        var n = new Vector3(Mathf.Cos(a) * Mathf.Sqrt(1 - y * y), y, Mathf.Sin(a) * Mathf.Sqrt(1 - y * y));
                        m.Box(StonePoint(p, n, false) + n * .065f, Vector3.one * .105f, Hex("CCAC67"), Quaternion.FromToRotation(Vector3.up, n));
                    }
                for (int row = 0; row < 2; row++)
                    for (int segment = 0; segment < 32; segment++)
                    {
                        float center = row == 0 ? .36f : -.36f, a = segment * Mathf.PI / 16, b = (segment + 1) * Mathf.PI / 16;
                        Vector3 StrapPoint(float y, float angle)
                        {
                            var n = new Vector3(Mathf.Cos(angle) * Mathf.Sqrt(1 - y * y), y, Mathf.Sin(angle) * Mathf.Sqrt(1 - y * y));
                            return StonePoint(p, n, false) + n * .05f;
                        }
                        void Band(float lo, float hi, Color color)
                        {
                            var v0 = StrapPoint(lo, a); var v1 = StrapPoint(lo, b); var v2 = StrapPoint(hi, b); var v3 = StrapPoint(hi, a);
                            m.Tri(v0, v3, v2, color); m.Tri(v0, v2, v1, color);
                        }
                        Band(center - .045f, center + .045f, Hex("9C7648") * (.94f + .06f * Mathf.Sin(segment * 4)));
                        Band(center - .054f, center - .045f, Hex("C5A05A"));
                        Band(center + .045f, center + .054f, Hex("C5A05A"));
                    }
            }
            if (p == 6 && !seat) // low seven-stone ring on the summit (a seated altar keeps its board space clear)
                for (int k = 0; k < 7; k++)
                {
                    float a = k * Mathf.PI * 2 / 7;
                    m.Box(new Vector3(Mathf.Cos(a) * 1.50f, 3.15f, Mathf.Sin(a) * 1.50f), new Vector3(.46f, .72f, .40f), Hex("CAC0A5"), Quaternion.Euler(0, k * 51, 0)); // v4b
                }
            if (p == 3)
            {
                if (!seat)
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * Mathf.PI / 3;
                        m.Crystal(new Vector3(Mathf.Cos(a) * 1.5f, 2.80f, Mathf.Sin(a) * 1.5f), .40f, Hex("D1EBEA"));
                    }
                for (int line = 0; line < 5; line++)
                    for (int k = 0; k < 24; k++)
                    {
                        Vector3 Crack(float u)
                        {
                            float a = -2.8f + u * 2.5f;
                            return Quaternion.Euler(0, line * 71, 0) * new Vector3(Mathf.Cos(a), Mathf.Sin(u * 5 + line) * .35f, Mathf.Sin(a)).normalized;
                        }
                        var n = Crack(k / 24f); var n2 = Crack((k + 1) / 24f);
                        var a0 = StonePoint(p, n, seat) + n * .028f; var b0 = StonePoint(p, n2, seat) + n2 * .028f;
                        var side = Vector3.Cross(n, b0 - a0).normalized * .025f;
                        m.Tri(a0 - side, b0 - side, b0 + side, Hex("5F91A9"));
                        m.Tri(a0 - side, b0 + side, a0 + side, Hex("5F91A9"));
                    }
            }
            if (p == 2) // v4b: three carved glyphs on the front
                for (int glyph = 0; glyph < 3; glyph++)
                {
                    Vector3 Mark(float gx, float gy) { var n = new Vector3(gx, gy, -1).normalized; return StonePoint(p, n, seat) + n * .07f; }
                    void Stroke(Vector3 a, Vector3 b)
                    {
                        var side = Vector3.Cross((a + b).normalized, b - a).normalized * .035f;
                        m.Tri(a - side, b + side, b - side, Hex("542F29"));
                        m.Tri(a - side, a + side, b + side, Hex("542F29"));
                    }
                    float x = -.45f + glyph * .43f;
                    Stroke(Mark(x, .20f), Mark(x, .48f));
                    Stroke(Mark(x - .10f, .42f), Mark(x + .10f, .32f));
                    Stroke(Mark(x - .10f, .28f), Mark(x + .10f, .38f));
                }
            if (p == 5) // v4b: hanging eaves under three terrace rows
                for (int row = 0; row < 3; row++)
                    for (int k = 0; k < 10; k++)
                    {
                        float a = k * Mathf.PI / 5 + .17f * row, y = .45f - row * .36f;
                        var n = new Vector3(Mathf.Cos(a) * Mathf.Sqrt(1 - y * y), y, Mathf.Sin(a) * Mathf.Sqrt(1 - y * y));
                        var center = StonePoint(p, n, seat) + n * .09f;
                        var side = Vector3.Cross(Vector3.up, n).normalized * .07f;
                        var tip = center + Vector3.down * (.30f + (k % 3) * .08f) + n * .045f;
                        m.Tri(center - side, tip, center + n * .09f, Hex("E9D5AF"));
                        m.Tri(center + n * .09f, tip, center + side, Hex("C8AF87"));
                    }
            var mesh = m.ToMesh($"Planet stone {Looks[p]}{(seat ? " seated" : "")}");
            int tris = m.t.Count / 3;
            if (tris < MinTriangles || tris > MaxTriangles)
                throw new InvalidOperationException($"[SevenSteps] planet body {Looks[p]}: {tris} triangles, the budget is {MinTriangles}..{MaxTriangles}");
            return mesh;
        }
    }
}
