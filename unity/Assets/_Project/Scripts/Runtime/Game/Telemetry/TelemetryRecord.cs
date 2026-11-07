using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SevenSteps.Game.Telemetry
{
    /// <summary>
    /// One event of docs/telemetry.md v1: seq, t, type and ordered fields whose values are int/long, bool, string, null
    /// or a cell [x, y]. Writes its own JSON (no reflection: WebGL code stripping).
    /// Game-internal until Contracts' TelemetryEvent can carry these value kinds (asked of the architect).
    /// </summary>
    public sealed class TelemetryRecord
    {
        public readonly long Seq;
        public readonly long T;
        public readonly string Type;
        readonly List<KeyValuePair<string, object>> fields = new List<KeyValuePair<string, object>>(12);

        public TelemetryRecord(long seq, long t, string type) { Seq = seq; T = t; Type = type; }

        public IReadOnlyList<KeyValuePair<string, object>> Fields => fields;

        public TelemetryRecord Set(string key, object value)
        {
            for (int k = 0; k < fields.Count; k++)
                if (fields[k].Key == key) { fields[k] = new KeyValuePair<string, object>(key, value); return this; }
            fields.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        public object Get(string key)
        {
            foreach (var kv in fields) if (kv.Key == key) return kv.Value;
            return null;
        }

        public string ToJson()
        {
            var sb = new StringBuilder(160);
            sb.Append("{\"seq\":").Append(Seq).Append(",\"t\":").Append(T).Append(",\"type\":");
            Json.Str(sb, Type);
            foreach (var kv in fields)
            {
                sb.Append(',');
                Json.Str(sb, kv.Key);
                sb.Append(':');
                Json.Value(sb, kv.Value);
            }
            return sb.Append('}').ToString();
        }
    }

    /// <summary>A cell coordinate [x, y] (docs/telemetry.md 2). Null cells are written as JSON null by the caller.</summary>
    public readonly struct XY
    {
        public readonly int X, Y;
        public XY(int x, int y) { X = x; Y = y; }
    }

    static class Json
    {
        public static void Value(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int i: sb.Append(i); break;
                case long l: sb.Append(l); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case string s: Str(sb, s); break;
                case XY c: sb.Append('[').Append(c.X).Append(',').Append(c.Y).Append(']'); break;
                default: Str(sb, v.ToString()); break;
            }
        }

        public static void Str(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
