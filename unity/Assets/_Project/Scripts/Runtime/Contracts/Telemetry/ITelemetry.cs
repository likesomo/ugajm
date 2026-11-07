using System.Collections.Generic;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// One play event, docs/telemetry.md v1 section 3. Fields keep insertion order and are written as JSON without
    /// reflection (WebGL code stripping). <see cref="Seq"/> and <see cref="TimeMs"/> are stamped by the sender, not by callers.
    /// Use the constants in <see cref="TelemetryNames"/> for type names and enum-like values.
    /// </summary>
    public sealed class TelemetryEvent
    {
        public enum FieldKind { String, Long, Double, Bool, Cell, Null }

        public readonly struct Field
        {
            public readonly string Key;
            public readonly FieldKind Kind;
            public readonly string S;
            public readonly long L;
            public readonly double D;
            public readonly int X, Y;

            public Field(string key, FieldKind kind, string s = null, long l = 0, double d = 0, int x = 0, int y = 0)
            {
                Key = key; Kind = kind; S = s; L = l; D = d; X = x; Y = y;
            }
        }

        public readonly string Type;
        public long Seq { get; private set; } = -1;
        /// Client time, ms since Unix epoch.
        public long TimeMs { get; private set; }
        public readonly List<Field> Fields = new List<Field>(8);

        public TelemetryEvent(string type) { Type = type; }

        public TelemetryEvent With(string key, string value) => Add(value == null ? new Field(key, FieldKind.Null) : new Field(key, FieldKind.String, s: value));
        public TelemetryEvent With(string key, int value) => Add(new Field(key, FieldKind.Long, l: value));
        public TelemetryEvent With(string key, long value) => Add(new Field(key, FieldKind.Long, l: value));
        public TelemetryEvent With(string key, double value) => Add(new Field(key, FieldKind.Double, d: value));
        public TelemetryEvent With(string key, bool value) => Add(new Field(key, FieldKind.Bool, l: value ? 1 : 0));
        /// Cell as [x, y] (docs/telemetry.md section 2). A negative index (off the map) is written as null.
        public TelemetryEvent WithCell(string key, int cell, int levelWidth) =>
            Add(cell < 0 ? new Field(key, FieldKind.Null) : new Field(key, FieldKind.Cell, x: cell % levelWidth, y: cell / levelWidth));

        /// Called once by the sender when the event is queued.
        public void Stamp(long seq, long timeMs) { Seq = seq; TimeMs = timeMs; }

        public bool TryGet(string key, out Field field)
        {
            foreach (var f in Fields) if (f.Key == key) { field = f; return true; }
            field = default;
            return false;
        }

        TelemetryEvent Add(Field f)
        {
            for (int k = 0; k < Fields.Count; k++)
                if (Fields[k].Key == f.Key) { Fields[k] = f; return this; }
            Fields.Add(f);
            return this;
        }
    }

    /// <summary>Batch header values (docs/telemetry.md section 1). Filled once by the bootstrap.</summary>
    public sealed class TelemetryContext
    {
        public const int SchemaVersion = 1;
        public const string ClientWebGL = "unity-webgl";
        public const string ClientDesktop = "unity-desktop";

        /// Collector base URL; batches go to Endpoint + "/v1/events".
        public string Endpoint = "";
        public string PlayerId = "";
        public string SessionId = "";
        public string Client = ClientDesktop;
        /// ≤ 40 chars. Application.version (BuildTools writes 0.1.0+commit).
        public string Build = "";

        public string EventsUrl => Endpoint.TrimEnd('/') + "/v1/events";
    }

    /// <summary>
    /// Play analytics sink. Game code calls Track; it never blocks and never throws.
    /// - Core does not know telemetry. A tracker in SevenSteps.Game turns GameSession updates into events.
    /// - Disabled (opt-out, no endpoint): Track does nothing and nothing is queued.
    /// - The sender stamps seq/t, batches (5 s or 20 events, max 100 per batch, 64 KB), retries and beacons on page hide.
    /// </summary>
    public interface ITelemetry
    {
        bool Enabled { get; }
        void SetEnabled(bool enabled);
        void Track(TelemetryEvent e);
        /// Send what is queued now. Called on stage end, application pause and quit.
        void Flush();
    }

    /// Does nothing. Used when the endpoint is empty, the player opted out, and in tests.
    public sealed class NullTelemetry : ITelemetry
    {
        public bool Enabled => false;
        public void SetEnabled(bool enabled) { }
        public void Track(TelemetryEvent e) { }
        public void Flush() { }
    }

    /// <summary>Keeps every tracked event in memory. For tests (compare with server/fixtures/telemetry.golden.json).</summary>
    public sealed class MemoryTelemetry : ITelemetry
    {
        public readonly List<TelemetryEvent> Events = new List<TelemetryEvent>();
        public bool Enabled { get; private set; } = true;
        public void SetEnabled(bool enabled) => Enabled = enabled;
        public void Track(TelemetryEvent e)
        {
            if (!Enabled) return;
            e.Stamp(Events.Count, 0);
            Events.Add(e);
        }
        public void Flush() { }
    }
}
