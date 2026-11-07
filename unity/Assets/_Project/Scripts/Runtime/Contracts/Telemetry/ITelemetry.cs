using System;
using System.Collections.Generic;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// One analytics event. PROVISIONAL: names and fields are decided by docs/telemetry.md (feat/server);
    /// this struct will be aligned to that schema. Values are strings/numbers only, so a sender can write JSON
    /// without reflection (WebGL code stripping).
    /// </summary>
    public sealed class TelemetryEvent
    {
        public readonly string Name;
        public readonly DateTime TimeUtc;
        public readonly Dictionary<string, string> Strings = new Dictionary<string, string>();
        public readonly Dictionary<string, double> Numbers = new Dictionary<string, double>();

        public TelemetryEvent(string name) { Name = name; TimeUtc = DateTime.UtcNow; }

        public TelemetryEvent With(string key, string value) { Strings[key] = value ?? ""; return this; }
        public TelemetryEvent With(string key, double value) { Numbers[key] = value; return this; }
    }

    /// Context attached to every event by the sender (not by callers).
    public sealed class TelemetryContext
    {
        public string Endpoint = "";
        public string PlayerId = "";
        public string SessionId = "";
        public string BuildVersion = "";
        public string BuildChannel = "";
        public string Platform = "";
    }

    /// <summary>
    /// Play analytics sink. Game code calls Track; it never blocks and never throws.
    /// - Core does not know telemetry. A recorder in SevenSteps.Game listens to GameSession.Updated / AppFlow.Changed and calls Track.
    /// - Opt-out: when disabled, Track does nothing (no buffering).
    /// - The HTTP implementation lives in SevenSteps.Game (gameplay/UI, UnityWebRequest, batched, Flush on pause/quit).
    /// </summary>
    public interface ITelemetry
    {
        bool Enabled { get; }
        void SetEnabled(bool enabled);
        void Track(TelemetryEvent e);
        /// Send what is buffered. Called on stage end, application pause and quit.
        void Flush();
    }

    /// Does nothing. Used when the endpoint is empty, the player opted out, in the editor by default, and in tests.
    public sealed class NullTelemetry : ITelemetry
    {
        public bool Enabled => false;
        public void SetEnabled(bool enabled) { }
        public void Track(TelemetryEvent e) { }
        public void Flush() { }
    }
}
