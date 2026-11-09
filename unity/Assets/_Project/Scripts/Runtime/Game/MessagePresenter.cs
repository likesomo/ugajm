using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;

namespace SevenSteps.Game
{
    /// <summary>
    /// The bottom message line of the Game HUD, worded from session updates (docs/scene-flow.md S3 cleanup).
    /// Plain C#: reads the events Core produced, decides nothing about the rules.
    /// The stage hint is not a message: HudView shows <c>Level.Hint</c> under the stage title for the whole stage.
    /// </summary>
    public sealed class MessagePresenter
    {
        static readonly Dictionary<GameEventType, string> ByEvent = new Dictionary<GameEventType, string>
        {
            { GameEventType.Choose, "행운의 7번째 걸음. 이 몸을 어떻게 할까?" },
            { GameEventType.Stone, "KEEP. 몸이 석상이 되었다. 다음 몸은 출발점에서 깨어난다." },
            { GameEventType.Switch, "SWITCH. 이 자리가 새 출발점이 되었다." },
            { GameEventType.Bridge, "7번째 걸음으로 구덩이에 몸을 던졌다. 다리가 되었다." },
            { GameEventType.Seal, "금 간 바닥 위에서 석상이 되었다. 바닥이 영원히 굳었다." },
            { GameEventType.Fall, "구덩이에 떨어졌다. 7번째 걸음이 아니면 아무것도 남지 않는다." },
            { GameEventType.Vanish, "출발점에서는 석상이 될 수 없다. 몸 하나를 잃었다." },
            { GameEventType.Crumble, "금 간 바닥이 무너졌다." },
            { GameEventType.Win, "7번째 걸음으로 출구에 닿았다!" },
            { GameEventType.Lose, "몸을 모두 잃었다. Z로 되돌리거나 R로 다시 시작." },
            { GameEventType.Undo, "한 걸음 되돌렸다." },
            { GameEventType.Restart, "처음부터." },
            { GameEventType.Flip, "7번째 걸음으로 레버를 당겼다. 벽이 뒤집혔다." },
        };

        public const string ChooseNudge = "먼저 고르자: K = KEEP (석상), X = SWITCH (새 출발점).";
        public const string LuckyNext = "다음 걸음이 행운의 7번째 걸음이다.";

        /// Text to show now ("" = empty line).
        public string Current { get; private set; } = "";

        /// A move was refused because KEEP/SWITCH must be chosen first (the HUD shakes the choice panel).
        public static bool NeedsChoice(SessionUpdate u) =>
            u.Kind == SessionUpdateKind.Feedback && !(u.Result != null && u.Result.Has(GameEventType.Bump))
            && u.State.Status == Status.Choose && u.Command.Type == CommandType.Move;

        public void Apply(SessionUpdate u)
        {
            switch (u.Kind)
            {
                case SessionUpdateKind.Rebuild:
                    Current = ""; // new stage: the hint is already under the title
                    break;
                case SessionUpdateKind.Feedback:
                    if (NeedsChoice(u)) Current = ChooseNudge;
                    break;
                default:
                    if (u.Result == null) break;
                    foreach (var e in u.Result.Events)
                    {
                        if (ByEvent.TryGetValue(e.Type, out var m)) Current = m;
                        if (e.Type == GameEventType.Step) Current = e.StepIdx == Rules.Steps - 1 ? LuckyNext : "";
                    }
                    break;
            }
        }

        /// Replaces the line with other text (the H solution).
        public void Show(string text) => Current = text ?? throw new ArgumentNullException(nameof(text));
    }
}
