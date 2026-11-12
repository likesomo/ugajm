using System;
using System.Collections.Generic;
using SevenSteps.Game.Flow;
using UnityEngine;

namespace SevenSteps.Game.UI.Planets
{
    /// <summary>
    /// A planet's stage constellation on the U5 surface (D2b Orbit sparks, user choice 2026-11-12; concepts/ui/
    /// stage-select-2026-11-11/surface-ui-2026-11-12/d2-refine IMPLEMENT.md): one node per stage at authored anchors in the
    /// 1280x720 reference (top-left origin, sky space around the enlarged body, not on the turning crust), joined in stage
    /// order, plus moss's closing edge 7-4 once the planet is complete (the bowl). Altar's hoop stays open: its 8-1 edge
    /// would cross the body's landmark (lead 2026-11-12: no bright lines across the landmark).
    /// Pure data; the view converts anchors to its canvas.
    /// </summary>
    public sealed class ConstellationFigure
    {
        public const float RefW = 1280, RefH = 720;

        /// Node centres, reference pixels, top-left origin.
        public IReadOnlyList<Vector2> Anchors { get; }
        /// Edges between slots: stage order first, then the closing edges.
        public IReadOnlyList<(int a, int b, bool closing)> Edges { get; }

        ConstellationFigure(Vector2[] anchors, List<(int, int, bool)> edges) { Anchors = anchors; Edges = edges; }

        /// The figure for a planet's <paramref name="count"/> stages. Unknown keys take the default arch. Throws when the
        /// figure has fewer anchors than stages (a planet with more stages needs an authored figure).
        public static ConstellationFigure For(string key, int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "stages");
            var v = Anchors_(key);
            if (count > v.Length) throw new InvalidOperationException($"[SevenSteps] constellation for {key}: {v.Length} anchors for {count} stages");
            var a = new Vector2[count];
            Array.Copy(v, a, count);
            var edges = new List<(int, int, bool)>();
            for (int i = 1; i < count; i++) edges.Add((i - 1, i, false));
            if (key == "moss" && count == 7) edges.Add((6, 3, true));
            return new ConstellationFigure(a, edges);
        }

        static Vector2[] Anchors_(string key)
        {
            switch (key)
            {
                case "moss": return P(200, 475, 250, 280, 410, 115, 880, 100, 1090, 155, 1120, 345, 935, 270); // dipper: handle and the four-light bowl
                case "frost": return P(220, 440, 245, 155, 420, 64, 640, 112, 860, 64, 1035, 155, 1060, 440); // cleft crown
                case "altar": return P(260, 500, 245, 260, 360, 125, 530, 65, 750, 65, 920, 125, 1035, 260, 1020, 500); // henge hoop
                case "joinery": return P(230, 470, 230, 210, 440, 100, 640, 65, 840, 100, 1050, 210, 1050, 470); // dovetail roof
                case "hollow": return P(225, 470, 210, 230, 380, 100, 640, 65, 900, 100, 1070, 230, 1055, 470);
                case "carved": return P(210, 470, 280, 220, 360, 95, 640, 95, 920, 95, 1000, 220, 1070, 470);
                default: return P(230, 500, 190, 270, 390, 90, 640, 65, 890, 90, 1090, 270, 1050, 500, 640, 560);
            }
        }

        static Vector2[] P(params float[] xy)
        {
            var v = new Vector2[xy.Length / 2];
            for (int i = 0; i < v.Length; i++) v[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
            return v;
        }

        /// A stage edge is drawn (warm) once its earlier stage is cleared (the route toward the next one opens); a closing
        /// edge once the planet is complete.
        public static bool Drawn(IReadOnlyList<bool> cleared, (int a, int b, bool closing) edge)
        {
            if (!edge.closing) return cleared[Math.Min(edge.a, edge.b)];
            foreach (bool c in cleared) if (!c) return false;
            return true;
        }

        /// The same for a planet of the model.
        public static bool Drawn(StageSelectModel model, int planet, (int a, int b, bool closing) edge) =>
            edge.closing ? model.Complete(planet) : model.IsCleared(planet, Math.Min(edge.a, edge.b));

        /// The slot with the current scarf arcs: the model's current slot, none once the planet is complete.
        public static int Current(StageSelectModel model, int planet) => model.Complete(planet) ? -1 : model.CurrentSlot(planet);
    }
}
