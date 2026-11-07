using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SevenSteps.Core;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// JSON for docs/telemetry.md v1 without reflection, plus levelKey. Pure; same output on every platform.
    /// </summary>
    public static class TelemetryJson
    {
        /// One event object: {"seq":..,"t":..,"type":..,fields...}. includeTime=false drops "t" (golden comparisons).
        public static void WriteEvent(StringBuilder sb, TelemetryEvent e, bool includeTime = true)
        {
            sb.Append("{\"seq\":").Append(e.Seq < 0 ? 0 : e.Seq);
            if (includeTime) sb.Append(",\"t\":").Append(e.TimeMs);
            sb.Append(",\"type\":");
            Str(sb, e.Type);
            foreach (var f in e.Fields)
            {
                sb.Append(',');
                Str(sb, f.Key);
                sb.Append(':');
                switch (f.Kind)
                {
                    case TelemetryEvent.FieldKind.String: Str(sb, f.S); break;
                    case TelemetryEvent.FieldKind.Long: sb.Append(f.L); break;
                    case TelemetryEvent.FieldKind.Double: sb.Append(f.D.ToString("R", CultureInfo.InvariantCulture)); break;
                    case TelemetryEvent.FieldKind.Bool: sb.Append(f.L != 0 ? "true" : "false"); break;
                    case TelemetryEvent.FieldKind.Cell: sb.Append('[').Append(f.X).Append(',').Append(f.Y).Append(']'); break;
                    default: sb.Append("null"); break;
                }
            }
            sb.Append('}');
        }

        /// Whole POST body: {"v":1,"playerId":..,"sessionId":..,"client":..,"build":..,"events":[...]}.
        public static string Batch(TelemetryContext ctx, IList<TelemetryEvent> events, int start = 0, int count = -1)
        {
            if (count < 0) count = events.Count - start;
            var sb = new StringBuilder(256 + count * 128);
            sb.Append("{\"v\":").Append(TelemetryContext.SchemaVersion);
            sb.Append(",\"playerId\":"); Str(sb, ctx.PlayerId);
            sb.Append(",\"sessionId\":"); Str(sb, ctx.SessionId);
            sb.Append(",\"client\":"); Str(sb, ctx.Client);
            sb.Append(",\"build\":"); Str(sb, ctx.Build.Length > 40 ? ctx.Build.Substring(0, 40) : ctx.Build);
            sb.Append(",\"events\":[");
            for (int k = 0; k < count; k++)
            {
                if (k > 0) sb.Append(',');
                WriteEvent(sb, events[start + k]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public static void Str(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s ?? "")
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

        /// FNV-1a 32-bit over UTF-16 code units, 8 lowercase hex chars (telemetry.js fnv1a).
        public static string Fnv1a(string s)
        {
            uint h = 0x811c9dc5;
            foreach (char c in s)
            {
                h ^= c;
                h = unchecked(h * 0x01000193);
            }
            return h.ToString("x8");
        }

        /// Stage content id (docs/telemetry.md section 2, telemetry.js levelKey): "<bodies>\n<row>\n<row>..." with rows
        /// padded to the width with '#' and the start written as 'S'.
        public static string LevelKey(Level level)
        {
            var sb = new StringBuilder(4 + level.Count + level.H);
            sb.Append(level.Bodies);
            for (int y = 0; y < level.H; y++)
            {
                sb.Append('\n');
                for (int x = 0; x < level.W; x++)
                {
                    int i = y * level.W + x;
                    sb.Append(i == level.Start ? 'S' : level.Tile(i));
                }
            }
            return Fnv1a(sb.ToString());
        }
    }
}
