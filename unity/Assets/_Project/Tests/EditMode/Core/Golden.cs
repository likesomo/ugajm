using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SevenSteps.Core;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// Golden data written by unity/Tools/gen-golden.mjs from the web prototype's core.js (the rules reference).
    /// Regenerate after changing core.js or levels.js:  node unity/Tools/gen-golden.mjs
    public static class Golden
    {
        public const string Regenerate = "Regenerate the golden data: node unity/Tools/gen-golden.mjs";
        static GoldenLevelsFile levels;
        static GoldenTracesFile traces;

        public static string Dir => Path.Combine(Application.dataPath, "_Project", "Tests", "Golden");
        public static GoldenLevelsFile Levels => levels ??= Load<GoldenLevelsFile>("levels.golden.json");
        public static GoldenTracesFile Traces => traces ??= Load<GoldenTracesFile>("traces.golden.json");

        static T Load<T>(string file)
        {
            var path = Path.Combine(Dir, file);
            if (!File.Exists(path)) throw new FileNotFoundException($"{path} is missing. {Regenerate}");
            return JsonUtility.FromJson<T>(File.ReadAllText(path));
        }

        public static Level ToLevel(GoldenLevelDef d) => Level.Parse(d.name, d.hint, d.bodies, d.map);

        /// Command letters of the traces: U D L R move, K keep, X switch, Z undo, * restart.
        public static Command ToCommand(char c)
        {
            if (c == 'Z') return Command.Undo;
            if (c == '*') return Command.Restart;
            if (Rules.TryParseLetter(c, out var a)) return Command.FromAct(a);
            throw new ArgumentException($"bad trace command '{c}'");
        }

        // Both sides are turned into the same text so a mismatch shows as a readable diff.

        public static string Describe(State s)
        {
            int depth = 0;
            for (var p = s.Prev; p != null; p = p.Prev) depth++;
            var sb = new StringBuilder();
            sb.Append($"start={s.Start} pos={s.Pos} used={s.Used} bodies={s.Bodies} status={s.Status.ToString().ToLowerInvariant()}");
            sb.Append($" statues=[{string.Join(",", s.Statues)}] bridges=[{string.Join(",", s.Bridges)}] broken=[{string.Join(",", s.Broken)}] trail=[");
            for (int k = 0; k < s.Trail.Length; k++) sb.Append(k > 0 ? "," : "").Append(s.Trail[k].Cell).Append(':').Append(s.Trail[k].Step);
            sb.Append($"] undo={depth}");
            return sb.ToString();
        }

        public static string Describe(GoldenState g)
        {
            var sb = new StringBuilder();
            sb.Append($"start={g.start} pos={g.pos} used={g.used} bodies={g.bodies} status={g.status}");
            sb.Append($" statues=[{string.Join(",", g.statues)}] bridges=[{string.Join(",", g.bridges)}] broken=[{string.Join(",", g.broken)}] trail=[");
            for (int k = 0; k < g.trail.Length; k++) sb.Append(k > 0 ? "," : "").Append(g.trail[k].i).Append(':').Append(g.trail[k].n);
            sb.Append($"] undo={g.undoDepth}");
            return sb.ToString();
        }

        /// core.js sets stepIdx only on 'step' events, so only those carry it here (C# also fills it on 7th-step events).
        public static string Describe(List<GameEvent> events)
        {
            var sb = new StringBuilder();
            foreach (var e in events)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(e.Type.ToString().ToLowerInvariant()).Append('@').Append(e.At);
                if (e.Type == GameEventType.Step) sb.Append('#').Append(e.StepIdx);
            }
            return sb.ToString();
        }

        public static string Describe(GoldenEvent[] events)
        {
            var sb = new StringBuilder();
            foreach (var e in events)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(e.type.ToLowerInvariant()).Append('@').Append(e.at);
                if (e.type == "step") sb.Append('#').Append(e.stepIdx);
            }
            return sb.ToString();
        }
    }

    // JSON shapes (JsonUtility: public fields, no dictionaries, no nested arrays).

    [Serializable] public sealed class GoldenLevelsFile { public string generator; public int steps; public GoldenLevel[] levels; }

    [Serializable]
    public sealed class GoldenLevel
    {
        public string name, hint, tiles, minPath;
        public int bodies, w, h, start, minBodies, minMoves;
        public string[] map;
        public bool needsSwitch;
        public GoldenPlate[] plates;
        public GoldenSolve[] solves;
        public override string ToString() => name;
    }

    [Serializable] public sealed class GoldenPlate { public string plate; public int[] cells; }
    [Serializable] public sealed class GoldenSolve { public int bodies, bodiesUsed; public bool noSwitch, solvable; public string path; }

    [Serializable] public sealed class GoldenTracesFile { public string generator; public int steps; public GoldenTrace[] traces; }

    [Serializable]
    public sealed class GoldenTrace
    {
        public string id, kind, commands;
        public GoldenLevelDef level;
        public GoldenState initial;
        public GoldenStep[] steps;
        public override string ToString() => id;
    }

    [Serializable] public sealed class GoldenLevelDef { public string name, hint; public int bodies; public string[] map; }
    [Serializable] public sealed class GoldenStep { public string cmd; public bool changed; public GoldenEvent[] events; public GoldenState state; }
    [Serializable] public sealed class GoldenEvent { public string type; public int at, stepIdx; }
    [Serializable] public sealed class GoldenFoot { public int i, n; }

    [Serializable]
    public sealed class GoldenState
    {
        public int start, pos, used, bodies, undoDepth;
        public string status;
        public int[] statues, bridges, broken;
        public GoldenFoot[] trail;
    }
}
