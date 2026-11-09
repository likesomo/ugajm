using System;
using System.Collections.Generic;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Start options from the command line (desktop) or the page URL (WebGL). Same format as the web prototype:
    /// desktop  "Seven Steps.exe" -level 8 -replay LRRRRDDK [-screenshot out.png] [-telemetry <url>|off] [-stageselect] [-credits]
    /// WebGL    ?level=8&amp;replay=LRRRRDDK&amp;telemetry=<url>|off   (docs/telemetry.md section 4)
    /// Pure parsing: the caller passes Environment.GetCommandLineArgs() and Application.absoluteURL.
    /// Bad values are not exceptions (external input): they are skipped and described in <see cref="Warnings"/>,
    /// which the bootstrap logs (Contracts does not log). Read-only after parsing, except the two editor/once-only changes below.
    /// </summary>
    public sealed class LaunchOptions
    {
        /// 1-based stage, 0 = not given (go to Title).
        public int Level { get; private set; }
        /// Move string (U D L R K X) to play once on the start stage. Read it with <see cref="TakeReplay"/>.
        public string Replay { get; private set; } = "";
        public string Screenshot { get; private set; } = "";
        /// Telemetry forced off for this run (telemetry=off or 0).
        public bool TelemetryOff { get; private set; }
        /// Collector URL override (telemetry=http...). Empty = use AppConfig.telemetryEndpoint.
        public string TelemetryUrl { get; private set; } = "";
        /// Smoke tests with -screenshot and no -level: open Stage Select / Credits on the title screen first.
        public bool OpenStageSelect { get; private set; }
        public bool OpenCredits { get; private set; }
        /// UI language for this run only (-lang en / ?lang=en, docs 13.3; not saved). Null = the saved setting.
        public Language? Language { get; private set; }

        readonly List<string> warnings = new List<string>();
        /// What was ignored while parsing, one line each. Empty when everything parsed.
        public IReadOnlyList<string> Warnings => warnings;

        public bool DirectToGame => Level > 0;

        /// The replay, once: returns it and clears it so a later stage entry does not replay again.
        public string TakeReplay()
        {
            string r = Replay;
            Replay = "";
            return r;
        }

        /// Editor direct Play of the Game scene: use the inspector's start stage and replay when the launch gave none.
        /// Returns true when the level came from here (not from the command line or URL).
        public bool UseEditorStart(int level, string replay)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "1-based stage");
            if (string.IsNullOrEmpty(Replay)) Replay = replay ?? "";
            if (DirectToGame) return false;
            Level = level;
            return true;
        }

        void SetLevel(string source, string v)
        {
            if (int.TryParse(v, out int n) && n >= 1) Level = n;
            else warnings.Add($"{source}: level \"{v}\" is not a stage number (1 or more); ignored");
        }

        void SetLanguage(string source, string v)
        {
            if (Languages.TryParse(v, out var lang)) Language = lang;
            else warnings.Add($"{source}: lang \"{v}\" is not ko or en; ignored");
        }

        void SetTelemetry(string source, string v)
        {
            if (v == "off" || v == "0") TelemetryOff = true;
            else if (v.StartsWith("http://", StringComparison.Ordinal) || v.StartsWith("https://", StringComparison.Ordinal)) TelemetryUrl = v;
            else warnings.Add($"{source}: telemetry \"{v}\" is neither off/0 nor an http(s) URL; ignored");
        }

        public static LaunchOptions Parse(string[] args, string url)
        {
            var o = new LaunchOptions();
            if (args != null)
                for (int k = 0; k < args.Length; k++)
                {
                    switch (args[k])
                    {
                        case "-stageselect": o.OpenStageSelect = true; continue;
                        case "-credits": o.OpenCredits = true; continue;
                        case "-level": case "-replay": case "-screenshot": case "-telemetry": case "-lang": break;
                        default: continue;
                    }
                    if (k + 1 >= args.Length || args[k + 1].StartsWith("-", StringComparison.Ordinal))
                    {
                        o.warnings.Add($"command line: {args[k]} has no value; ignored");
                        continue;
                    }
                    string v = args[++k];
                    switch (args[k - 1])
                    {
                        case "-level": o.SetLevel("command line", v); break;
                        case "-replay": o.Replay = v; break;
                        case "-screenshot": o.Screenshot = v; break;
                        case "-telemetry": o.SetTelemetry("command line", v); break;
                        case "-lang": o.SetLanguage("command line", v); break;
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
                    case "level": o.SetLevel("url", v); break;
                    case "replay": o.Replay = v; break;
                    case "telemetry": o.SetTelemetry("url", v); break;
                    case "lang": o.SetLanguage("url", v); break;
                }
            }
            return o;
        }
    }
}
