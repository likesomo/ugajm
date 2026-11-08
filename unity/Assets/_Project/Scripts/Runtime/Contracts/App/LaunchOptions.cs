using System;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Start options from the command line (desktop) or the page URL (WebGL). Same format as the web prototype:
    /// desktop  "Seven Steps.exe" -level 8 -replay LRRRRDDK [-screenshot out.png] [-telemetry <url>|off]
    /// WebGL    ?level=8&amp;replay=LRRRRDDK&amp;telemetry=<url>|off   (docs/telemetry.md section 4)
    /// Pure parsing: the caller passes Environment.GetCommandLineArgs() and Application.absoluteURL.
    /// </summary>
    public sealed class LaunchOptions
    {
        /// 1-based stage, 0 = not given (go to Title).
        public int Level;
        public string Replay = "";
        public string Screenshot = "";
        /// Telemetry forced off for this run (telemetry=off or 0).
        public bool TelemetryOff;
        /// Collector URL override (telemetry=http...). Empty = use AppConfig.telemetryEndpoint.
        public string TelemetryUrl = "";

        public bool DirectToGame => Level > 0;

        void SetTelemetry(string v)
        {
            if (v == "off" || v == "0") TelemetryOff = true;
            else if (v.StartsWith("http://") || v.StartsWith("https://")) TelemetryUrl = v;
        }

        public static LaunchOptions Parse(string[] args, string url)
        {
            var o = new LaunchOptions();
            if (args != null)
                for (int k = 0; k + 1 < args.Length; k++)
                {
                    string v = args[k + 1];
                    switch (args[k])
                    {
                        case "-level": int.TryParse(v, out o.Level); break;
                        case "-replay": if (!v.StartsWith("-")) o.Replay = v; break;
                        case "-screenshot": o.Screenshot = v; break;
                        case "-telemetry": o.SetTelemetry(v); break;
                    }
                }

            int q = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (q < 0) return o;
            foreach (var kv in url.Substring(q + 1).Split('&'))
            {
                int eq = kv.IndexOf('=');
                if (eq <= 0) continue;
                string v = Uri.UnescapeDataString(kv.Substring(eq + 1));
                switch (kv.Substring(0, eq))
                {
                    case "level": int.TryParse(v, out o.Level); break;
                    case "replay": o.Replay = v; break;
                    case "telemetry": o.SetTelemetry(v); break;
                }
            }
            return o;
        }
    }
}
