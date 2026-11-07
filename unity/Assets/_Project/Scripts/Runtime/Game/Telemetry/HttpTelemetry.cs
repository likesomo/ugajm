using System;
using System.Collections.Generic;
using System.Text;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace SevenSteps.Game.Telemetry
{
    /// <summary>
    /// Sender for docs/telemetry.md v1 (4. 전송 사양). Owns the app-lifetime <see cref="TelemetryTracker"/> (seq, session,
    /// attempts), a local queue (max 1000, persisted in PlayerPrefs), batching (every 5 s or at 20 events, up to 100 events
    /// and ~60 KB, one session per batch), retry only on network error / 429 / 5xx (5 s doubling to 5 min, Retry-After wins),
    /// and the page-close path (WebGL sendBeacon through SevenStepsTelemetry.jslib, desktop: a short blocking send on quit).
    /// Driven by <see cref="TelemetryRunner"/>. Never throws into the game; when disabled nothing is queued.
    /// </summary>
    public sealed class HttpTelemetry : ITelemetry
    {
        public const int SchemaVersion = 1;
        const int QueueMax = 1000;
        const int BatchMax = 100;
        const int BytesMax = 60000;
        const int FlushCount = 20;
        const float FlushSeconds = 5f;
        const int TimeoutSeconds = 10;
        const string QueueKey = "seven-steps-telemetry-queue";
        const string AttemptsKey = "seven-steps-attempts";

        struct Item { public string Session; public string Json; }

        readonly string url;
        readonly string client;
        readonly string build;
        readonly ISaveStore store;
        readonly List<Item> queue = new List<Item>();
        readonly StringBuilder sb = new StringBuilder(4096);

        UnityWebRequest inFlight;
        int inFlightCount;
        float nextFlushAt;
        float retryAt;
        float backoff;
        bool queueDirty;

        public TelemetryContext Context { get; }
        public TelemetryTracker Tracker { get; }
        public bool Enabled { get; private set; }
        /// Current game state for session_end/idle (set by the Game scene's recorder; null outside a stage).
        public Func<State> CurrentState;
        /// Last state of a Game scene that was left for the stage select; closes that attempt at the next stage start.
        public State PendingLeaveState;

        public HttpTelemetry(AppConfig config, ISaveStore store, string playerId)
        {
            this.store = store;
            url = config.telemetryEndpoint.TrimEnd('/') + "/v1/events";
#if UNITY_WEBGL && !UNITY_EDITOR
            client = "unity-webgl";
#else
            client = "unity-desktop";
#endif
            string version = Application.version + (Application.isEditor ? "+editor" : "");
            build = version.Length > 40 ? version.Substring(0, 40) : version;
            Context = new TelemetryContext
            {
                Endpoint = config.telemetryEndpoint, PlayerId = playerId, SessionId = Guid.NewGuid().ToString("D"),
                BuildVersion = build, BuildChannel = config.buildChannel, Platform = client,
            };
            Tracker = new TelemetryTracker(Enqueue, NowMs, LoadAttempts());
            LoadQueue();
        }

        static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // ---------- ITelemetry ----------

        public void SetEnabled(bool enabled)
        {
            if (Enabled == enabled) return;
            Enabled = enabled;
            if (!enabled)
            {
                queue.Clear();
                store.Delete(QueueKey);
                store.Flush();
            }
        }

        /// Provisional Contracts event (name + string/number fields). Mapped onto a schema record as-is.
        public void Track(TelemetryEvent e)
        {
            if (!Enabled || e == null) return;
            try
            {
                // Goes through the tracker so it gets the session's next seq (the server dedups on sessionId + seq).
                Tracker.Custom(e.Name, r =>
                {
                    foreach (var kv in e.Strings) r.Set(kv.Key, kv.Value);
                    foreach (var kv in e.Numbers) r.Set(kv.Key, kv.Value);
                });
            }
            catch (Exception ex) { Debug.LogWarning("[Telemetry] " + ex.Message); }
        }

        public void Flush() => nextFlushAt = 0;

        // ---------- queue ----------

        void Enqueue(TelemetryRecord r)
        {
            if (!Enabled) return;
            queue.Add(new Item { Session = Context.SessionId, Json = r.ToJson() });
            if (queue.Count > QueueMax) queue.RemoveRange(0, queue.Count - QueueMax);
            queueDirty = true;
            if (r.Type == "level_start") SaveAttempts();
            if (queue.Count >= FlushCount) nextFlushAt = 0;
        }

        /// Called every frame by the runner.
        public void Tick(float time)
        {
            if (inFlight != null)
            {
                if (!inFlight.isDone) return;
                Finish(time);
            }
            if (!Enabled || queue.Count == 0 || time < retryAt) return;
            if (time < nextFlushAt) return;
            nextFlushAt = time + FlushSeconds;
            try
            {
                string body = NextBatch(out inFlightCount);
                inFlight = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = TimeoutSeconds,
                };
                inFlight.SetRequestHeader("Content-Type", "application/json");
                inFlight.SendWebRequest();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Telemetry] send failed: " + ex.Message);
                inFlight = null;
            }
        }

        void Finish(float time)
        {
            long code = inFlight.responseCode;
            bool retry = inFlight.result == UnityWebRequest.Result.ConnectionError || code == 0 || code == 429 || code >= 500;
            string retryAfter = inFlight.GetResponseHeader("Retry-After");
            inFlight.Dispose();
            inFlight = null;
            if (retry)
            {
                backoff = backoff <= 0 ? 5f : Mathf.Min(backoff * 2f, 300f);
                retryAt = time + (float.TryParse(retryAfter, out float ra) && ra > 0 ? ra : backoff);
                return;
            }
            // 2xx, 400, 403, 413: the server's answer will not change, drop the batch.
            backoff = 0;
            queue.RemoveRange(0, Mathf.Min(inFlightCount, queue.Count));
            queueDirty = true;
            if (queue.Count > 0) nextFlushAt = time; // keep draining
        }

        /// Oldest events of one session, up to 100 and ~60 KB, as a batch body.
        string NextBatch(out int count)
        {
            string sid = queue[0].Session;
            sb.Clear();
            sb.Append("{\"v\":").Append(SchemaVersion).Append(",\"playerId\":");
            Json.Str(sb, Context.PlayerId);
            sb.Append(",\"sessionId\":");
            Json.Str(sb, sid);
            sb.Append(",\"client\":");
            Json.Str(sb, client);
            sb.Append(",\"build\":");
            Json.Str(sb, build);
            sb.Append(",\"events\":[");
            int bytes = sb.Length + 2;
            count = 0;
            foreach (var item in queue)
            {
                if (item.Session != sid || count >= BatchMax) break;
                int n = Encoding.UTF8.GetByteCount(item.Json) + 1;
                if (bytes + n > BytesMax && count > 0) break;
                if (count > 0) sb.Append(',');
                sb.Append(item.Json);
                bytes += n;
                count++;
            }
            return sb.Append("]}").ToString();
        }

        // ---------- page close / quit ----------

        /// WebGL tab hidden or closing: end the session and hand the whole queue to navigator.sendBeacon.
        public void OnPageHidden(string reason)
        {
            if (!Enabled) return;
            try
            {
                Tracker.SessionEnd(CurrentState?.Invoke(), reason);
                Beacon();
            }
            catch (Exception ex) { Debug.LogWarning("[Telemetry] " + ex.Message); }
            SaveQueue(true);
        }

        public void OnPageVisible(int cleared)
        {
            if (!Enabled) return;
            Tracker.SessionStart(false, 0, resumed: true);
        }

        /// Desktop quit: session_end + one blocking send of at most 1 s (never used on WebGL), then persist the rest.
        public void OnQuit()
        {
            if (!Enabled) return;
            try
            {
                Tracker.SessionEnd(CurrentState?.Invoke(), "unload");
#if !UNITY_WEBGL || UNITY_EDITOR
                if (inFlight != null) { inFlight.Abort(); inFlight.Dispose(); inFlight = null; }
                if (queue.Count > 0)
                {
                    string body = NextBatch(out int count);
                    using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
                    {
                        uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                        downloadHandler = new DownloadHandlerBuffer(),
                        timeout = 1,
                    };
                    req.SetRequestHeader("Content-Type", "application/json");
                    var op = req.SendWebRequest();
                    var until = DateTime.UtcNow.AddSeconds(1);
                    while (!op.isDone && DateTime.UtcNow < until) System.Threading.Thread.Sleep(10);
                    if (op.isDone && req.responseCode >= 200 && req.responseCode < 500 && req.responseCode != 429)
                        queue.RemoveRange(0, Mathf.Min(count, queue.Count));
                }
#else
                Beacon();
#endif
            }
            catch (Exception ex) { Debug.LogWarning("[Telemetry] " + ex.Message); }
            SaveQueue(true);
        }

        void Beacon()
        {
            while (queue.Count > 0)
            {
                string body = NextBatch(out int count);
                if (!SendBeacon(url, body)) break;
                queue.RemoveRange(0, count);
            }
            queueDirty = true;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int SevenSteps_SendBeacon(string url, string body);
        static bool SendBeacon(string url, string body) => SevenSteps_SendBeacon(url, body) != 0;
#else
        static bool SendBeacon(string url, string body) => false;
#endif

        // ---------- persistence ----------

        /// Queue lines: "sessionId\tjson". Written on the flush tick (no disk write) and on hide/quit (with a disk write).
        public void SaveQueue(bool toDisk)
        {
            if (!queueDirty && !toDisk) return;
            queueDirty = false;
            if (!Enabled) return;
            sb.Clear();
            foreach (var item in queue) sb.Append(item.Session).Append('\t').Append(item.Json).Append('\n');
            store.Set(QueueKey, sb.ToString());
            if (toDisk) store.Flush();
        }

        void LoadQueue()
        {
            string text = store.Get(QueueKey);
            if (string.IsNullOrEmpty(text)) return;
            foreach (var line in text.Split('\n'))
            {
                int tab = line.IndexOf('\t');
                if (tab <= 0 || tab == line.Length - 1) continue;
                queue.Add(new Item { Session = line.Substring(0, tab), Json = line.Substring(tab + 1) });
            }
            if (queue.Count > QueueMax) queue.RemoveRange(0, queue.Count - QueueMax);
        }

        Dictionary<int, int> LoadAttempts()
        {
            var d = new Dictionary<int, int>();
            foreach (var part in store.Get(AttemptsKey).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[0], out int lv) && int.TryParse(kv[1], out int n)) d[lv] = n;
            }
            return d;
        }

        void SaveAttempts()
        {
            var parts = new List<string>(Tracker.Attempts.Count);
            foreach (var kv in Tracker.Attempts) parts.Add(kv.Key + ":" + kv.Value);
            store.Set(AttemptsKey, string.Join(",", parts));
        }
    }
}
