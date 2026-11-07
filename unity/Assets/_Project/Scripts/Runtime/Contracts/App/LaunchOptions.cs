using System;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Start options from the command line (desktop) or the page URL (WebGL). Same format as the web prototype:
    /// desktop  "Seven Steps.exe" -level 8 -replay LRRRRDDK [-screenshot out.png] [-telemetry 0]
    /// WebGL    ?level=8&amp;replay=LRRRRDDK&amp;telemetry=0
    /// Pure parsing: the caller passes Environment.GetCommandLineArgs() and Application.absoluteURL.
    /// </summary>
    public sealed class LaunchOptions
    {
        /// 1-based stage, 0 = not given (go to Title).
        public int Level;
        public string Replay = "";
        public string Screenshot = "";
        /// Forced telemetry off for this run (?telemetry=0).
        public bool TelemetryOff;

        public bool DirectToGame => Level > 0;

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
                        case "-telemetry": o.TelemetryOff = v == "0"; break;
                    }
                }

            int q = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (q < 0) return o;
            foreach (var kv in url.Substring(q + 1).Split('&'))
            {
                var p = kv.Split('=');
                if (p.Length != 2) continue;
                string v = Uri.UnescapeDataString(p[1]);
                switch (p[0])
                {
                    case "level": int.TryParse(v, out o.Level); break;
                    case "replay": o.Replay = v; break;
                    case "telemetry": o.TelemetryOff = v == "0"; break;
                }
            }
            return o;
        }
    }
}
