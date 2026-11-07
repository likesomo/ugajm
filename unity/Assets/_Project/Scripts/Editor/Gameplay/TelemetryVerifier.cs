using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SevenSteps.Core;
using SevenSteps.Game.Telemetry;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Checks the C# <see cref="TelemetryTracker"/> against server/fixtures/telemetry.golden.json (made by
    /// server/tools/telemetry-golden.mjs from telemetry.js): every trace is replayed the same way (clock fixed at 0,
    /// U D L R K X moves, Z undo, * restart, H hint, > next stage via the selector) and the events must match field by field
    /// (t, durationMs, idleMs, sessionMs dropped; idle and session_end skipped). Owner: gameplay/UI.
    /// Menu: Seven Steps > Verify Telemetry. Batch: -executeMethod SevenSteps.EditorTools.TelemetryVerifier.BatchVerify
    /// </summary>
    public static class TelemetryVerifier
    {
        const string GoldenRel = "../server/fixtures/telemetry.golden.json";
        const string LevelsPath = "Assets/_Project/Data/levels.json";
        static readonly HashSet<string> Drop = new HashSet<string> { "t", "durationMs", "idleMs", "sessionMs" };
        static readonly HashSet<string> Skip = new HashSet<string> { "idle", "session_end" };

        [MenuItem("Seven Steps/Verify Telemetry (C# vs telemetry.js)", priority = 11)]
        public static void VerifyMenu()
        {
            bool ok = Run(out string summary);
            EditorUtility.DisplayDialog("Seven Steps", summary, "OK");
        }

        public static void BatchVerify()
        {
            bool ok;
            try { ok = Run(out _); }
            catch (Exception e) { Debug.LogException(e); ok = false; }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Run(out string summary)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string path = Path.GetFullPath(Path.Combine(projectRoot, GoldenRel));
            if (!File.Exists(path)) { summary = "golden missing: " + path; Debug.LogError("[TelemetryVerify] " + summary); return false; }
            var golden = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(path, Encoding.UTF8));
            var levelFile = JsonUtility.FromJson<LevelFileDto>(File.ReadAllText(Path.Combine(projectRoot, LevelsPath), Encoding.UTF8));
            var stages = levelFile.levels.Select(d => d.ToLevel()).ToArray();

            int total = 0, failed = 0, events = 0;
            foreach (Dictionary<string, object> trace in (List<object>)golden["traces"])
            {
                total++;
                string id = (string)trace["id"];
                try
                {
                    var mine = Replay(trace, stages);
                    var want = (List<object>)trace["events"];
                    events += want.Count;
                    string diff = Compare(want, mine);
                    if (diff != null) { failed++; Debug.LogError($"[TelemetryVerify] {id}: {diff}"); }
                }
                catch (Exception e) { failed++; Debug.LogError($"[TelemetryVerify] {id}: {e}"); }
            }
            summary = failed == 0 ? $"telemetry OK: {total} traces, {events} events match telemetry.js" : $"telemetry FAILED: {failed}/{total} traces differ";
            Debug.Log("[TelemetryVerify] " + summary);
            return failed == 0;
        }

        static List<object> Replay(Dictionary<string, object> trace, Level[] stages)
        {
            var map = ((List<object>)trace["map"]).Cast<string>().ToArray();
            int bodies = (int)(double)trace["bodies"];
            int n = (int)(double)trace["level"];
            var level = Level.Parse((string)trace["levelName"], "", bodies, map);
            var outEvents = new List<object>();
            var tr = new TelemetryTracker(e => outEvents.Add(MiniJson.Parse(e.ToJson())), () => 0);
            var state = Rules.NewState(level);
            tr.SessionStart(true, 0);
            tr.LevelStart(state, n, level.Name, "boot");
            foreach (char ch in (string)trace["commands"])
            {
                if (ch == 'H') { tr.Hint(state); continue; }
                if (ch == '>')
                {
                    tr.Leave(state, n + 1);
                    n += 1;
                    state = Rules.NewState(stages[n - 1]);
                    tr.LevelStart(state, n, stages[n - 1].Name, "select");
                    continue;
                }
                Command cmd = ch switch
                {
                    'U' => Command.Move(Dir.Up), 'D' => Command.Move(Dir.Down), 'L' => Command.Move(Dir.Left), 'R' => Command.Move(Dir.Right),
                    'K' => Command.Keep, 'X' => Command.Switch, 'Z' => Command.Undo, '*' => Command.Restart,
                    _ => throw new Exception("unknown command letter " + ch),
                };
                var before = state;
                var r = Rules.Dispatch(state, cmd);
                tr.Command(before, cmd, r);
                state = r.State;
            }
            return outEvents.Cast<Dictionary<string, object>>()
                .Where(e => !Skip.Contains((string)e["type"]))
                .Select(e => (object)e.Where(kv => !Drop.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value))
                .ToList();
        }

        static string Compare(List<object> want, List<object> got)
        {
            int n = Math.Min(want.Count, got.Count);
            for (int k = 0; k < n; k++)
                if (!Same(want[k], got[k])) return $"event {k} differs\n  want {MiniJson.Write(want[k])}\n  got  {MiniJson.Write(got[k])}";
            if (want.Count != got.Count)
                return $"event count {got.Count}, want {want.Count}; first extra: {MiniJson.Write(want.Count > got.Count ? want[n] : got[n])}";
            return null;
        }

        static bool Same(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a is double da && b is double db) return Math.Abs(da - db) < 1e-9;
            if (a is List<object> la && b is List<object> lb) return la.Count == lb.Count && la.Zip(lb, Same).All(x => x);
            if (a is Dictionary<string, object> ma && b is Dictionary<string, object> mb)
                return ma.Count == mb.Count && ma.All(kv => mb.TryGetValue(kv.Key, out var v) && Same(kv.Value, v));
            return a.Equals(b);
        }

        /// Minimal JSON reader/writer for the verifier: objects, arrays, strings, numbers (double), true/false/null.
        static class MiniJson
        {
            public static object Parse(string s) { int i = 0; var v = Value(s, ref i); return v; }

            static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            static object Value(string s, ref int i)
            {
                Ws(s, ref i);
                char c = s[i];
                if (c == '{')
                {
                    var d = new Dictionary<string, object>();
                    i++; Ws(s, ref i);
                    if (s[i] == '}') { i++; return d; }
                    while (true)
                    {
                        Ws(s, ref i);
                        string k = Str(s, ref i);
                        Ws(s, ref i); i++; // :
                        d[k] = Value(s, ref i);
                        Ws(s, ref i);
                        if (s[i++] == '}') return d;
                    }
                }
                if (c == '[')
                {
                    var l = new List<object>();
                    i++; Ws(s, ref i);
                    if (s[i] == ']') { i++; return l; }
                    while (true)
                    {
                        l.Add(Value(s, ref i));
                        Ws(s, ref i);
                        if (s[i++] == ']') return l;
                    }
                }
                if (c == '"') return Str(s, ref i);
                if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
                if (s.Substring(i, Math.Min(5, s.Length - i)).StartsWith("false")) { i += 5; return false; }
                if (s.Substring(i, Math.Min(4, s.Length - i)).StartsWith("null")) { i += 4; return null; }
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
            }

            static string Str(string s, ref int i)
            {
                var sb = new StringBuilder();
                i++; // opening quote
                while (s[i] != '"')
                {
                    char c = s[i++];
                    if (c != '\\') { sb.Append(c); continue; }
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                        default: sb.Append(e); break;
                    }
                }
                i++;
                return sb.ToString();
            }

            public static string Write(object v)
            {
                switch (v)
                {
                    case null: return "null";
                    case bool b: return b ? "true" : "false";
                    case double d: return d.ToString(CultureInfo.InvariantCulture);
                    case string s: return "\"" + s + "\"";
                    case List<object> l: return "[" + string.Join(",", l.Select(Write)) + "]";
                    case Dictionary<string, object> m: return "{" + string.Join(",", m.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"\"{kv.Key}\":{Write(kv.Value)}")) + "}";
                    default: return v.ToString();
                }
            }
        }
    }
}
