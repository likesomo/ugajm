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
    /// <see cref="ITelemetry"/> sender for docs/telemetry.md v1 section 4 (docs/architecture.md 11.4, 11.5):
    /// stamps seq/t, keeps a local queue (max 1000, PlayerPrefs "seven-steps.telemetry-queue"), sends batches every 5 s or at
    /// 20 events (one session per batch, ≤100 events, ≤~60 KB), retries only on network error / 429 / 5xx (5 s doubling to
    /// 5 min, Retry-After wins), drops the batch on 2xx/400/403/413, and on page close beacons (WebGL jslib) or does one
    /// short blocking send (desktop quit). Also owns the app-lifetime <see cref="TelemetryTracker"/>.
    /// Driven by <see cref="TelemetryRunner"/>. Never throws into the game; disabled = nothing queued.
    /// </summary>
    public sealed class HttpTelemetry : ITelemetry
    {
        const int QueueMax = 1000;
        const int BatchMax = 100;
        const int BytesMax = 60000;
        const int FlushCount = 20;
        const float FlushSeconds = 5f;
        const int TimeoutSeconds = 10;
        public const string QueueKey = "seven-steps.telemetry-queue";

        /// Player and session the event was recorded under: a batch never goes out under another player's id
        /// (-newplayer starts a new id while the last tester's events may still wait in the queue).
        struct Item { public string Player; public string Session; public string Json; }

        readonly ISaveStore store;
        readonly List<Item> queue = new List<Item>();
        readonly StringBuilder sb = new StringBuilder(4096);

        UnityWebRequest inFlight;
        int inFlightCount;
        long seq;
        float nextFlushAt;
        float retryAt;
        float backoff;
        bool queueDirty;

        public readonly TelemetryContext Context;
        public readonly TelemetryTracker Tracker;
        public bool Enabled { get; private set; }
        /// Current game state for session_end/idle (set by the Game scene's recorder; null outside a stage).
        public Func<State> CurrentState;
        /// Last state of a Game scene left for the stage select; closes that attempt at the next stage start.
        public State PendingLeaveState;

        // Status for the playtest facilitator (settings panel line, playtest-1-prep E): read only, no effect on sending.
        /// Events the collector accepted (2xx) this run.
        public int SentEvents { get; private set; }
        /// Events waiting in the queue (kept across runs).
        public int PendingEvents => queue.Count;
        /// HTTP status of the last answered POST, 0 = connection error, -1 = none yet this run.
        public long LastStatus { get; private set; } = -1;

        /// <param name="minBodies">(1-based level, level) -> fewest bodies for level_clear.stars; null = the solver (TelemetryTracker).</param>
        public HttpTelemetry(TelemetryContext context, ISaveStore store, Func<int, int> nextAttempt, Func<int, Level, int> minBodies = null)
        {
            Context = context;
            this.store = store;
            Tracker = new TelemetryTracker(this, NowMs, nextAttempt, minBodies: minBodies);
            LoadQueue();
        }

        public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // ---------- ITelemetry ----------

        public void SetEnabled(bool enabled)
        {
            if (Enabled == enabled) return;
            Enabled = enabled;
            if (enabled) return;
            queue.Clear();
            store.Delete(QueueKey);
            store.Flush();
        }

        public void Track(TelemetryEvent e)
        {
            if (!Enabled || e == null) return;
            try
            {
                e.Stamp(seq++, NowMs());
                sb.Clear();
                TelemetryJson.WriteEvent(sb, e);
                queue.Add(new Item { Player = Context.PlayerId, Session = Context.SessionId, Json = sb.ToString() });
                if (queue.Count > QueueMax) queue.RemoveRange(0, queue.Count - QueueMax);
                queueDirty = true;
                if (queue.Count >= FlushCount) nextFlushAt = 0;
            }
            catch (Exception ex) { Debug.LogWarning("[Telemetry] " + ex.Message); }
        }

        public void Flush() => nextFlushAt = 0;

        /// Called every frame by the runner.
        public void Tick(float time)
        {
            if (inFlight != null)
            {
                if (!inFlight.isDone) return;
                Finish(time);
            }
            if (!Enabled || queue.Count == 0 || time < retryAt || time < nextFlushAt) return;
            nextFlushAt = time + FlushSeconds;
            try
            {
                inFlight = Post(NextBatch(out inFlightCount), TimeoutSeconds);
                inFlight.SendWebRequest();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Telemetry] send failed: " + ex.Message);
                inFlight = null;
            }
        }

        UnityWebRequest Post(string body, int timeout)
        {
            var req = new UnityWebRequest(Context.EventsUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeout,
            };
            req.SetRequestHeader("Content-Type", "application/json");
            return req;
        }

        static bool ShouldRetry(UnityWebRequest r) =>
            r.result == UnityWebRequest.Result.ConnectionError || r.responseCode == 0 || r.responseCode == 429 || r.responseCode >= 500;

        void Finish(float time)
        {
            bool retry = ShouldRetry(inFlight);
            LastStatus = inFlight.responseCode;
            if (inFlight.responseCode >= 200 && inFlight.responseCode < 300) SentEvents += inFlightCount;
            string retryAfter = inFlight.GetResponseHeader("Retry-After");
            inFlight.Dispose();
            inFlight = null;
            if (retry)
            {
                backoff = backoff <= 0 ? 5f : Mathf.Min(backoff * 2f, 300f);
                retryAt = time + (float.TryParse(retryAfter, out float ra) && ra > 0 ? ra : backoff);
                return;
            }
            // 2xx, 400, 403, 413: the answer will not change, drop the batch.
            backoff = 0;
            queue.RemoveRange(0, Mathf.Min(inFlightCount, queue.Count));
            queueDirty = true;
            if (queue.Count > 0) nextFlushAt = time;
        }

        /// Oldest events of one session as a POST body (header from Context, events as queued).
        string NextBatch(out int count)
        {
            string pid = queue[0].Player, sid = queue[0].Session;
            var b = new StringBuilder(1024);
            b.Append("{\"v\":").Append(TelemetryContext.SchemaVersion).Append(",\"playerId\":");
            TelemetryJson.Str(b, pid);
            b.Append(",\"sessionId\":");
            TelemetryJson.Str(b, sid);
            b.Append(",\"client\":");
            TelemetryJson.Str(b, Context.Client);
            b.Append(",\"build\":");
            TelemetryJson.Str(b, Context.Build.Length > 40 ? Context.Build.Substring(0, 40) : Context.Build);
            b.Append(",\"events\":[");
            int bytes = b.Length + 2;
            count = 0;
            foreach (var item in queue)
            {
                if (item.Player != pid || item.Session != sid || count >= BatchMax) break;
                int n = Encoding.UTF8.GetByteCount(item.Json) + 1;
                if (bytes + n > BytesMax && count > 0) break;
                if (count > 0) b.Append(',');
                b.Append(item.Json);
                bytes += n;
                count++;
            }
            return b.Append("]}").ToString();
        }

        // ---------- page close / quit ----------

        /// WebGL tab hidden or closing: session_end, then the whole queue through navigator.sendBeacon.
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

        /// Tab visible again: same sessionId, session_start resumed (telemetry.js sends cleared 0 here).
        public void OnPageVisible()
        {
            if (Enabled) Tracker.SessionStart(false, 0, resumed: true);
        }

        /// Quit: session_end; desktop waits at most 1 s for one send, WebGL beacons. The rest is persisted.
        public void OnQuit()
        {
            if (!Enabled) return;
            try
            {
                Tracker.SessionEnd(CurrentState?.Invoke(), TelemetryNames.EndReason.Unload);
#if UNITY_WEBGL && !UNITY_EDITOR
                Beacon();
#else
                if (inFlight != null) { inFlight.Abort(); inFlight.Dispose(); inFlight = null; }
                if (queue.Count > 0)
                {
                    using var req = Post(NextBatch(out int count), 1);
                    var op = req.SendWebRequest();
                    var until = DateTime.UtcNow.AddSeconds(1);
                    while (!op.isDone && DateTime.UtcNow < until) System.Threading.Thread.Sleep(10); // desktop only
                    if (op.isDone && !ShouldRetry(req)) queue.RemoveRange(0, Mathf.Min(count, queue.Count));
                }
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
                if (!SendBeacon(Context.EventsUrl, body)) break;
                queue.RemoveRange(0, count);
            }
            queueDirty = true;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int SevenSteps_SendBeacon(string url, string json);
        static bool SendBeacon(string url, string body) => SevenSteps_SendBeacon(url, body) != 0;
#else
        static bool SendBeacon(string url, string body) => false;
#endif

        // ---------- persistence ----------

        /// Lines "playerId\tsessionId\tjson" (older queues "sessionId\tjson" read as the current player). Set every few seconds (memory only) and written to disk on hide/quit/pause.
        public void SaveQueue(bool toDisk)
        {
            if (!queueDirty && !toDisk) return;
            queueDirty = false;
            if (!Enabled) return;
            var b = new StringBuilder(queue.Count * 160);
            foreach (var item in queue) b.Append(item.Player).Append('\t').Append(item.Session).Append('\t').Append(item.Json).Append('\n');
            store.Set(QueueKey, b.ToString());
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
                string rest = line.Substring(tab + 1);
                if (rest.StartsWith("{", StringComparison.Ordinal)) // a queue saved before players were kept: "sessionId\tjson"
                {
                    queue.Add(new Item { Player = Context.PlayerId, Session = line.Substring(0, tab), Json = rest });
                    continue;
                }
                int tab2 = rest.IndexOf('\t');
                if (tab2 <= 0 || tab2 == rest.Length - 1) continue;
                queue.Add(new Item { Player = line.Substring(0, tab), Session = rest.Substring(0, tab2), Json = rest.Substring(tab2 + 1) });
            }
            if (queue.Count > QueueMax) queue.RemoveRange(0, queue.Count - QueueMax);
        }
    }
}
