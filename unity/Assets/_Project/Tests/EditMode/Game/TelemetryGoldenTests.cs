using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Telemetry;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// <summary>
    /// The C# <see cref="TelemetryTracker"/> against server/fixtures/telemetry.golden.json (server/tools/telemetry-golden.mjs,
    /// reference telemetry.js): every trace is replayed the same way (clock fixed at 0, U D L R K X, Z undo, * restart,
    /// H hint, > next stage via the selector) into <see cref="MemoryTelemetry"/>, and the events must match field by field
    /// (t, durationMs, idleMs, sessionMs dropped; idle and session_end skipped). Owner: gameplay/UI.
    /// The golden file is read from the repo (not copied) so a regenerated file is picked up without a second copy.
    /// </summary>
    public sealed class TelemetryGoldenTests
    {
        const string GoldenRel = "../server/fixtures/telemetry.golden.json";
        static readonly HashSet<string> Drop = new HashSet<string> { "t", "durationMs", "idleMs", "sessionMs" };
        static readonly HashSet<string> Skip = new HashSet<string> { "idle", "session_end" };

        [Test]
        public void TrackerMatchesTelemetryJs()
        {
            string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), GoldenRel));
            Assert.That(File.Exists(path), "missing " + path);
            var golden = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(path, Encoding.UTF8));
            var stages = JsonUtility.FromJson<LevelFileDto>(File.ReadAllText(
                Path.Combine(Application.dataPath, "_Project", "Data", "levels.json"), Encoding.UTF8)).levels.Select(d => d.ToLevel()).ToArray();
            // The golden trace has no statueCount; the rule traces it was made from do.
            var statueCounts = Golden.Traces.traces.ToDictionary(t => t.id, t => t.level.statueCount);

            var failures = new List<string>();
            int traces = 0, events = 0;
            foreach (Dictionary<string, object> trace in (List<object>)golden["traces"])
            {
                traces++;
                string id = (string)trace["id"];
                try
                {
                    int n = (int)(double)trace["level"];
                    int count = trace.TryGetValue("statueCount", out var sc) && sc is double d ? (int)d
                        : statueCounts.TryGetValue(id, out int c) ? c : stages[n - 1].StatueCount;
                    var level = Level.Parse((string)trace["levelName"], "", (int)(double)trace["bodies"],
                        ((List<object>)trace["map"]).Cast<string>().ToArray(), count);
                    var want = (List<object>)trace["events"];
                    events += want.Count;
                    string diff = Compare(want, Replay(level, n, (string)trace["commands"], stages));
                    if (diff != null) failures.Add($"{id}: {diff}");
                }
                catch (Exception e) { failures.Add($"{id}: {e.Message}"); }
            }
            Assert.That(traces, Is.GreaterThan(0));
            Assert.IsEmpty(failures, $"{failures.Count}/{traces} traces differ from telemetry.js:\n" + string.Join("\n", failures.Take(5)));
            Debug.Log($"[TelemetryGolden] {traces} traces, {events} events match telemetry.js");
        }

        static List<object> Replay(Level level, int n, string commands, Level[] stages)
        {
            var sink = new MemoryTelemetry();
            var attempts = new Dictionary<int, int>();
            int NextAttempt(int lv) { attempts.TryGetValue(lv, out int a); return attempts[lv] = a + 1; }
            var tr = new TelemetryTracker(sink, () => 0, NextAttempt);
            var state = Rules.NewState(level);
            tr.SessionStart(true, 0);
            tr.LevelStart(state, n, level.Name, TelemetryNames.Via.Boot);
            foreach (char ch in commands)
            {
                if (ch == 'H') { tr.Hint(state); continue; }
                if (ch == '>')
                {
                    tr.Leave(state, n + 1);
                    n += 1;
                    state = Rules.NewState(stages[n - 1]);
                    tr.LevelStart(state, n, stages[n - 1].Name, TelemetryNames.Via.Select);
                    continue;
                }
                var cmd = Golden.ToCommand(ch);
                var before = state;
                var r = Rules.Dispatch(state, cmd);
                tr.Command(before, cmd, r);
                state = r.State;
            }
            var sb = new StringBuilder();
            var list = new List<object>();
            foreach (var e in sink.Events)
            {
                if (Skip.Contains(e.Type)) continue;
                sb.Clear();
                TelemetryJson.WriteEvent(sb, e, includeTime: false);
                var obj = (Dictionary<string, object>)MiniJson.Parse(sb.ToString());
                foreach (var k in Drop) obj.Remove(k);
                list.Add(obj);
            }
            // The golden seq counts every event (idle/session_end included); MemoryTelemetry does the same.
            return list;
        }

        static string Compare(List<object> want, List<object> got)
        {
            foreach (Dictionary<string, object> w in want) foreach (var k in Drop) w.Remove(k);
            int n = Math.Min(want.Count, got.Count);
            for (int k = 0; k < n; k++)
                if (!Same(want[k], got[k])) return $"event {k}\n  want {MiniJson.Write(want[k])}\n  got  {MiniJson.Write(got[k])}";
            if (want.Count != got.Count)
                return $"{got.Count} events, want {want.Count}; first extra {MiniJson.Write(want.Count > got.Count ? want[n] : got[n])}";
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

        /// Minimal JSON for the test: objects, arrays, strings, numbers (double), true/false/null.
        static class MiniJson
        {
            public static object Parse(string s) { int i = 0; return Value(s, ref i); }

            static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            static bool At(string s, int i, string word) => string.CompareOrdinal(s, i, word, 0, word.Length) == 0;

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
                        Ws(s, ref i); i++; // ':'
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
                if (At(s, i, "true")) { i += 4; return true; }
                if (At(s, i, "false")) { i += 5; return false; }
                if (At(s, i, "null")) { i += 4; return null; }
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
            }

            static string Str(string s, ref int i)
            {
                var sb = new StringBuilder();
                i++;
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

            public static string Write(object v) => v switch
            {
                null => "null",
                bool b => b ? "true" : "false",
                double d => d.ToString(CultureInfo.InvariantCulture),
                string s => "\"" + s + "\"",
                List<object> l => "[" + string.Join(",", l.Select(Write)) + "]",
                Dictionary<string, object> m => "{" + string.Join(",", m.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"\"{kv.Key}\":{Write(kv.Value)}")) + "}",
                _ => v.ToString(),
            };
        }
    }
}
