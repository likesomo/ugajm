using SevenSteps.Core;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// What KEEP and SWITCH would leave, for the choice cards and the board ghosts (docs/architecture.md 4.4). Pure: two
    /// Rules.Dispatch calls on the on-screen Choose state; nothing is sent to the GameSession, so the session state, its Undo
    /// chain, telemetry and replays are untouched. Make it again from every update (Undo back into Choose, language change).
    /// </summary>
    public readonly struct ChoicePreview
    {
        /// Upper bound of the confirm presentation (IBoardPresenter.Present of a Keep / Switch command), seconds. 0 with
        /// reduce motion. The session stays locked while it plays, as for any Present.
        public const float ConfirmSecondsMax = 0.5f;

        /// The choosing body's cell: KEEP leaves a statue here; SWITCH wakes the next body here.
        public readonly int Cell;
        /// Result of KEEP / SWITCH from the Choose state (Rules.Dispatch: State.Prev is the Choose state).
        public readonly StepResult Keep, Switch;

        ChoicePreview(int cell, StepResult keep, StepResult sw) { Cell = cell; Keep = keep; Switch = sw; }

        /// False unless <paramref name="s"/> waits for KEEP / SWITCH (Status.Choose).
        public static bool TryOf(State s, out ChoicePreview preview)
        {
            preview = default;
            if (s == null || s.Status != Status.Choose) return false;
            preview = new ChoicePreview(s.Pos,
                Rules.Dispatch(s, Command.Keep),
                Rules.Dispatch(s, Command.Switch));
            return true;
        }

        /// Where the next body starts after KEEP (the current start) / after SWITCH (the choosing cell).
        public int KeepStart => Keep.State.Start;
        public int SwitchStart => Switch.State.Start;
        /// The next body (BodyIndex) and so the card border colour; the same for both choices.
        public int NextBody => BodyIndex.Current(Keep.State);
    }
}
