using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenSteps.Game.Flow
{
    /// <summary>A place on a planet's surface path (IMPLEMENT.md: cleared = star, current = three scarf dots, untouched = dark).</summary>
    public enum PlaceState { Untouched, Current, Cleared }

    /// <summary>
    /// Stage select v4 model (pure C#, read only after construction): which stages are cleared, which planet the brothers
    /// were last on, the star-trail edges and which of them are lit, what a planet click does, and arrow-key neighbours.
    /// Built from the save by <see cref="From"/>; views only draw it. docs/stage-select-transitions.md.
    /// </summary>
    public sealed class StageSelectModel
    {
        readonly string[] keys;
        readonly bool[][] cleared;
        readonly (int from, int to)[] trails;

        public int PlanetCount => keys.Length;
        public string Key(int planet) => keys[Check(planet)];
        /// Planet of the last played stage (the brothers' marker), -1 when nothing was played yet.
        public int MarkerPlanet { get; }
        /// Slot of the last played stage on <see cref="MarkerPlanet"/>, -1 when none.
        public int MarkerSlot { get; }
        /// Star-trail edges between planets (story.json "dipper" order, else the Dipper lines), as planet indices.
        public IReadOnlyList<(int from, int to)> Trails => trails;

        /// <param name="keys">Chapter key per planet (map order).</param>
        /// <param name="cleared">Per planet, per stage slot: cleared.</param>
        /// <param name="markerPlanet">-1 or the last played planet; <paramref name="markerSlot"/> its slot.</param>
        /// <param name="trails">Edges as planet indices; each in range and not a loop.</param>
        public StageSelectModel(IReadOnlyList<string> keys, IReadOnlyList<IReadOnlyList<bool>> cleared, int markerPlanet, int markerSlot, IReadOnlyList<(int from, int to)> trails)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (cleared == null) throw new ArgumentNullException(nameof(cleared));
            if (trails == null) throw new ArgumentNullException(nameof(trails));
            if (keys.Count == 0) throw new ArgumentException("no planets", nameof(keys));
            if (cleared.Count != keys.Count) throw new ArgumentException($"{cleared.Count} cleared rows for {keys.Count} planets", nameof(cleared));
            this.keys = new string[keys.Count];
            this.cleared = new bool[keys.Count][];
            for (int p = 0; p < keys.Count; p++)
            {
                this.keys[p] = keys[p] ?? throw new ArgumentNullException(nameof(keys), $"planet {p} has no key");
                if (cleared[p] == null || cleared[p].Count == 0) throw new ArgumentException($"planet {p} ({keys[p]}) has no stages", nameof(cleared));
                this.cleared[p] = new bool[cleared[p].Count];
                for (int s = 0; s < cleared[p].Count; s++) this.cleared[p][s] = cleared[p][s];
            }
            if (markerPlanet < -1 || markerPlanet >= keys.Count) throw new ArgumentOutOfRangeException(nameof(markerPlanet), markerPlanet, "marker planet");
            if (markerPlanet >= 0 && (markerSlot < 0 || markerSlot >= this.cleared[markerPlanet].Length))
                throw new ArgumentOutOfRangeException(nameof(markerSlot), markerSlot, $"marker slot on planet {markerPlanet}");
            MarkerPlanet = markerPlanet;
            MarkerSlot = markerPlanet >= 0 ? markerSlot : -1;
            this.trails = new (int, int)[trails.Count];
            for (int k = 0; k < trails.Count; k++)
            {
                var (a, b) = trails[k];
                if (a < 0 || a >= keys.Count || b < 0 || b >= keys.Count || a == b) throw new ArgumentException($"trail {k} ({a} -> {b}) is not between two planets", nameof(trails));
                this.trails[k] = (a, b);
            }
        }

        /// The model for a save: <paramref name="stages"/> are each planet's stage indices (PlanetEntry.Stages);
        /// <paramref name="isCleared"/> and <paramref name="lastStage"/> (-1 = none) read the progress save.
        public static StageSelectModel From(IReadOnlyList<string> keys, IReadOnlyList<IReadOnlyList<int>> stages, Func<int, bool> isCleared, int lastStage,
            IReadOnlyList<(int from, int to)> trails)
        {
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            if (isCleared == null) throw new ArgumentNullException(nameof(isCleared));
            var rows = new List<IReadOnlyList<bool>>();
            int mp = -1, ms = -1;
            for (int p = 0; p < stages.Count; p++)
            {
                var row = new bool[stages[p].Count];
                for (int s = 0; s < row.Length; s++)
                {
                    row[s] = isCleared(stages[p][s]);
                    if (stages[p][s] == lastStage && mp < 0) { mp = p; ms = s; }
                }
                rows.Add(row);
            }
            return new StageSelectModel(keys, rows, mp, ms, trails);
        }

        /// Trail edges by key pairs (story.json "dipper"); pairs naming a planet that is not on the map are skipped.
        public static List<(int from, int to)> TrailsByKey(IReadOnlyList<string> keys, IEnumerable<(string from, string to)> pairs)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));
            var list = new List<(int, int)>();
            foreach (var (f, t) in pairs)
            {
                int a = IndexOf(keys, f), b = IndexOf(keys, t);
                if (a >= 0 && b >= 0 && a != b) list.Add((a, b));
            }
            return list;
        }

        static int IndexOf(IReadOnlyList<string> keys, string key)
        {
            for (int i = 0; i < keys.Count; i++) if (keys[i] == key) return i;
            return -1;
        }

        int Check(int planet)
        {
            if (planet < 0 || planet >= keys.Length) throw new ArgumentOutOfRangeException(nameof(planet), planet, $"planet is 0..{keys.Length - 1}");
            return planet;
        }

        public int StageCount(int planet) => cleared[Check(planet)].Length;
        public bool IsCleared(int planet, int slot) => cleared[Check(planet)][slot];

        public int ClearedCount(int planet)
        {
            int n = 0;
            foreach (bool c in cleared[Check(planet)]) if (c) n++;
            return n;
        }

        /// Every stage of the planet is cleared.
        public bool Complete(int planet) => ClearedCount(planet) == StageCount(planet);

        /// Played at all: a stage cleared or the brothers were last here. Unvisited planets render dimmed (research point 2).
        public bool Visited(int planet) => ClearedCount(planet) > 0 || MarkerPlanet == Check(planet);

        /// First slot not cleared, -1 when the planet is complete.
        public int FirstUnfinished(int planet)
        {
            var row = cleared[Check(planet)];
            for (int s = 0; s < row.Length; s++) if (!row[s]) return s;
            return -1;
        }

        /// The slot the brothers stand on (three scarf dots): the last played stage when it is on this planet and not
        /// cleared, else the first unfinished one; on a complete planet the last played stage there, else -1.
        public int CurrentSlot(int planet)
        {
            bool here = MarkerPlanet == Check(planet);
            if (here && !cleared[planet][MarkerSlot]) return MarkerSlot;
            int first = FirstUnfinished(planet);
            if (first >= 0) return first;
            return here ? MarkerSlot : -1;
        }

        public PlaceState Place(int planet, int slot)
        {
            if (slot < 0 || slot >= StageCount(planet)) throw new ArgumentOutOfRangeException(nameof(slot), slot, $"slot on planet {planet}");
            if (slot == CurrentSlot(planet)) return PlaceState.Current;
            return cleared[planet][slot] ? PlaceState.Cleared : PlaceState.Untouched;
        }

        /// A planet is completed for the star trails when its last stage (the chapter finale) is cleared: the same moment
        /// as the story's finale and scene-flow's journey return (JourneyReturn.Begin), whatever else is left to replay.
        public bool FinaleCleared(int planet) => cleared[Check(planet)][cleared[planet].Length - 1];

        /// A trail is lit when the planet it leaves is completed (<see cref="FinaleCleared"/>; "a completed world's outgoing
        /// trail lights toward the next world even when that destination is unfinished", v3). <paramref name="held"/>: a
        /// planet whose new trail waits for the journey return pullback (JourneyReturn.Pending), -1 = none.
        public bool TrailLit(int edge, int held = -1)
        {
            var (a, _) = trails[edge];
            return a != held && FinaleCleared(a);
        }

        /// What a map click / Enter on a planet does: play its first unfinished stage (the dive), or, on a complete
        /// planet, open its surface path to replay (v3 input map).
        public bool ChoosePlays(int planet, out int slot)
        {
            slot = FirstUnfinished(planet);
            return slot >= 0;
        }

        /// The planet the map focuses when it opens: the marker planet, else the first one.
        public int DefaultFocus => MarkerPlanet >= 0 ? MarkerPlanet : 0;

        /// After a planet is completed (journey return): the next planet along its outgoing trail, else the planet itself.
        public int NextAlong(int planet)
        {
            Check(planet);
            foreach (var (a, b) in trails) if (a == planet) return b;
            return planet;
        }

        /// Arrow-key neighbour (v3 input map): left / right follow the order (index -1 / +1, stopping at the ends); up /
        /// down pick the nearest point lying that way on screen (y grows downward), favouring straight lines; none = stay.
        /// Used for both the map (planets) and the surface path (places).
        public static int Neighbor(IReadOnlyList<Vector2> points, int from, int dx, int dy)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (from < 0 || from >= points.Count) throw new ArgumentOutOfRangeException(nameof(from), from, "from");
            if (Math.Abs(dx) + Math.Abs(dy) != 1) throw new ArgumentException($"one step in one direction, not ({dx}, {dy})");
            if (dx != 0) return Mathf.Clamp(from + dx, 0, points.Count - 1);
            int best = from;
            float bestScore = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                if (i == from) continue;
                var d = points[i] - points[from];
                float along = d.y * dy;
                if (along <= 1f) continue;
                float score = along + 2f * Mathf.Abs(d.x);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }
    }
}
