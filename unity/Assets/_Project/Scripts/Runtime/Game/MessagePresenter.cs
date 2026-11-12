using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;

namespace SevenSteps.Game
{
    /// <summary>
    /// The bottom message line of the Game HUD, worded from session updates (docs/scene-flow.md S3 cleanup).
    /// Plain C#: reads the events Core produced, decides nothing about the rules. Holds a string-table key (msg.*,
    /// docs/architecture.md 13.2), not text, so <see cref="Current"/> follows a language change without a new update.
    /// The stage hint is not a message: HudView shows the stage hint under the stage title for the whole stage.
    /// </summary>
    public sealed class MessagePresenter
    {
        static readonly Dictionary<GameEventType, string> ByEvent = new Dictionary<GameEventType, string>
        {
            { GameEventType.Choose, "msg.choose" },
            { GameEventType.Stone, "msg.stone" },
            { GameEventType.Switch, "msg.switch" },
            { GameEventType.Bridge, "msg.bridge" },
            { GameEventType.Seal, "msg.seal" },
            { GameEventType.Fall, "msg.fall" },
            { GameEventType.Vanish, "msg.vanish" },
            { GameEventType.Crumble, "msg.crumble" },
            { GameEventType.Win, "msg.win" },
            { GameEventType.Lose, "msg.lose" },
            { GameEventType.Undo, "msg.undo" },
            { GameEventType.Restart, "msg.restart" },
            { GameEventType.Flip, "msg.flip" },
        };

        public const string ChooseNudge = "msg.choose_nudge";
        public const string LuckyNext = "msg.lucky_next";
        /// A restart inside an attempt that opened a costing hint: the attempt still caps at 1 star (user decision
        /// 2026-11-12-b (2), option A), said right after the restart instead of "msg.restart".
        public const string RestartHinted = "msg.restart_hinted";

        readonly Localizer loc;
        Func<string> line = () => "";

        public MessagePresenter(Localizer loc)
        {
            this.loc = loc ?? throw new ArgumentNullException(nameof(loc));
        }

        /// Text to show now in the current language ("" = empty line).
        public string Current => line();

        /// A move was refused because KEEP/SWITCH must be chosen first (the HUD shakes the choice panel).
        public static bool NeedsChoice(SessionUpdate u) =>
            u.Kind == SessionUpdateKind.Feedback && !(u.Result != null && u.Result.Has(GameEventType.Bump))
            && u.State.Status == Status.Choose && u.Command.Type == CommandType.Move;

        /// <paramref name="hintedAttempt"/>: the attempt this update belongs to opened a costing hint (AttemptCounts.Hinted).
        public void Apply(SessionUpdate u, bool hintedAttempt)
        {
            switch (u.Kind)
            {
                case SessionUpdateKind.Rebuild:
                    Clear(); // new stage: the hint is already under the title
                    break;
                case SessionUpdateKind.Feedback:
                    if (NeedsChoice(u)) Key(ChooseNudge);
                    break;
                default:
                    if (u.Result == null) break;
                    foreach (var e in u.Result.Events)
                    {
                        if (ByEvent.TryGetValue(e.Type, out var key)) Key(e.Type == GameEventType.Restart && hintedAttempt ? RestartHinted : key);
                        if (e.Type == GameEventType.Step)
                        {
                            if (e.StepIdx == Rules.Steps - 1) Key(LuckyNext);
                            else Clear();
                        }
                    }
                    break;
            }
        }

        /// Replaces the line with other text, worded again on each read (the H solution in the current language).
        public void Show(Func<string> text) => line = text ?? throw new ArgumentNullException(nameof(text));

        /// Replaces the line with a string-table line (a one-time guide), worded on each read. Throws for a missing key.
        public void ShowKey(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("no string key", nameof(key));
            if (!loc.Table.Has(key)) throw new ArgumentException($"string key {key} missing", nameof(key));
            Key(key);
        }

        void Key(string key) => line = () => loc.Get(key);
        void Clear() => line = () => "";
    }
}
