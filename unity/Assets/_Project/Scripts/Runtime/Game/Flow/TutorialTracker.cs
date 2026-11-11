using System;
using SevenSteps.Contracts;
using SevenSteps.Core;

namespace SevenSteps.Game.Flow
{
    /// <summary>Where the one-time guides are remembered (the profile save; a memory store in tests).</summary>
    public interface ITutorialStore
    {
        /// The guide <paramref name="id"/> was finished once (its first successful use).
        bool Seen(string id);
        /// Remembers <paramref name="id"/> as seen and saves. Never undone by restart, undo or a language change.
        void MarkSeen(string id);
        /// Confirmed KEEP / SWITCH choices so far (counts up to <see cref="TutorialTracker.ChoiceExplanations"/>).
        int ChoiceConfirms { get; }
        /// One more confirmed choice, saved.
        void CountChoiceConfirm();
    }

    /// <summary>
    /// One-time guides of the minimal UI (production/ui-minimal-plan.md, design/art/ui-minimal.md "등장·해제·한 번 안내").
    /// Decides only what shows; the HUD and the choice cards draw it (gameplay-ui).
    /// - Move guide (direction buttons, controls line, first-move text): until the first applied move ever. A bump or a
    ///   move while choosing does not use it up.
    /// - Choice guide (KEEP / SWITCH): the first time the rules ask for a choice, until the first confirmed choice.
    /// - <see cref="ExplainChoices"/>: both cards keep their one-line result until <see cref="ChoiceExplanations"/>
    ///   choices were confirmed. A refused K / X or an undo does not count.
    /// - Menu "controls" (<see cref="PlayControls"/>) shows the move guide again without changing anything saved; the next
    ///   applied move hides it.
    /// Reads only <see cref="GameSession.Updated"/>: applied commands (Present). Rebuild / Snap (load, undo, restart, replay
    /// snaps) only redraw, so replays and the QA hash are untouched. Pure C#.
    /// </summary>
    public sealed class TutorialTracker
    {
        /// Guide ids with the tutorial version (Contracts TutorialIds): a changed guide gets a new id and shows once more.
        public const string MoveId = TutorialIds.Move, ChoiceId = TutorialIds.Choose;
        /// Confirmed choices that still show both result lines (art: "첫 세 번의 확정 선택").
        public const int ChoiceExplanations = TutorialData.ChoiceExplainCount;

        readonly ITutorialStore store;
        bool manualControls;
        bool choosing;

        public TutorialTracker(ITutorialStore store) => this.store = store ?? throw new ArgumentNullException(nameof(store));

        /// Direction buttons, the controls line and the first-move text: never moved yet, or replayed from the menu.
        public bool ShowMoveGuide => manualControls || !store.Seen(MoveId);
        /// The KEEP / SWITCH explanation: the rules ask for a choice now and none was confirmed ever.
        public bool ShowChoiceGuide => choosing && !store.Seen(ChoiceId);
        /// Both choice cards show their result line (first <see cref="ChoiceExplanations"/> confirmed choices).
        public bool ExplainChoices => store.ChoiceConfirms < ChoiceExplanations;
        /// The move guide is the menu's replay (not the first-time guide).
        public bool ManualControls => manualControls;

        /// Raised when any of the values above may have changed.
        public event Action Changed;

        /// GameSession.Updated.
        public void Apply(SessionUpdate u)
        {
            bool before = choosing;
            choosing = u.State != null && u.State.Status == Status.Choose;
            bool changed = before != choosing;
            if (u.Kind == SessionUpdateKind.Present && u.Result != null)
            {
                switch (u.Command.Type)
                {
                    case CommandType.Move:
                        if (manualControls) { manualControls = false; changed = true; }
                        if (!store.Seen(MoveId)) { store.MarkSeen(MoveId); changed = true; }
                        break;
                    case CommandType.Keep:
                    case CommandType.Switch:
                        if (u.Prev == null || u.Prev.Status != Status.Choose) break;
                        if (!store.Seen(ChoiceId)) store.MarkSeen(ChoiceId);
                        if (store.ChoiceConfirms < ChoiceExplanations) store.CountChoiceConfirm();
                        changed = true;
                        break;
                }
            }
            if (changed) Changed?.Invoke();
        }

        /// Menu "controls": the move guide once more, saved state unchanged. The next applied move hides it.
        public void PlayControls()
        {
            if (manualControls) return;
            manualControls = true;
            Changed?.Invoke();
        }

        /// Hides the menu replay (stage change, or the guide's own close).
        public void StopControls()
        {
            if (!manualControls) return;
            manualControls = false;
            Changed?.Invoke();
        }
    }

    /// <summary>The profile save (SaveSystem key seven-steps.tutorial; cleared by -newplayer with the progress).</summary>
    public sealed class SaveTutorialStore : ITutorialStore
    {
        readonly SaveSystem save;
        public SaveTutorialStore(SaveSystem save) => this.save = save ?? throw new ArgumentNullException(nameof(save));
        public bool Seen(string id) => save.TutorialSeen(id);
        public void MarkSeen(string id) => save.MarkTutorialSeen(id);
        public int ChoiceConfirms => save.ChoiceConfirms;
        public void CountChoiceConfirm() => save.CountChoiceConfirm();
    }

    /// <summary>A store kept in memory: tests, and dev runs that must not touch the profile (-replay, -flowcheck, smoke),
    /// which start with every guide seen so screenshots show the plain HUD.</summary>
    public sealed class MemoryTutorialStore : ITutorialStore
    {
        public MemoryTutorialStore(bool allSeen = false)
        {
            if (!allSeen) return;
            seen.Add(TutorialTracker.MoveId);
            seen.Add(TutorialTracker.ChoiceId);
            ChoiceConfirms = TutorialTracker.ChoiceExplanations;
        }

        readonly System.Collections.Generic.HashSet<string> seen = new System.Collections.Generic.HashSet<string>();
        public bool Seen(string id) => seen.Contains(id);
        public void MarkSeen(string id) => seen.Add(id);
        public int ChoiceConfirms { get; private set; }
        public void CountChoiceConfirm() { if (ChoiceConfirms < TutorialData.ChoiceExplainCount) ChoiceConfirms++; } // capped like the save (review C2)
    }
}
