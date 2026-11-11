using System;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.UI;

namespace SevenSteps.Game
{
    /// <summary>
    /// The KEEP / SWITCH choice of the minimal UI v3 (docs/architecture.md 4.4, design/art/ui-minimal.md): reads the session
    /// only (SessionUpdate / Presented State), works out what each choice would leave with <see cref="ChoicePreview"/> (pure,
    /// nothing sent), and drives the HUD cards (HudView.ShowChoice / PlaceChoice) and the board's ring, ghost, dotted line
    /// and knot (BoardView.ShowChoice).
    /// - When: a Present that ends in Choose shows it after Presented (the 7th step plays first); Snap / Rebuild into Choose
    ///   (Undo back, replays) show it at once; any update that is not Choose hides it at once; Feedback leaves it as it is.
    /// - Focus: KEEP first; pointer enter or selection on a card, or Tab, moves it. Only the focused choice has a ghost.
    /// - Confirm: a card click, K / X, or Enter on the focused card all end in the same Send (Command.Keep / Switch); the
    ///   session alone decides about locks and queues (4.1: input is never dropped here).
    /// The confirm count and the guide's "seen" are the TutorialTracker's (scene-flow); this reads them through the funcs.
    /// </summary>
    public sealed class ChoiceController : IDisposable
    {
        readonly GameSession session;
        readonly HudView hud;
        readonly BoardView board;
        readonly Action<Command> send;

        /// Both cards explain their result (first 3 confirmed choices). Set by the scene root from TutorialTracker.
        public Func<bool> ExplainChoices = () => true;
        /// The guide line above the cards (the first choice ever). Set by the scene root from TutorialTracker.
        public Func<bool> ShowGuide = () => false;

        bool waiting, shown, focusKeep = true;
        ChoicePreview preview;

        public bool Shown => shown;
        public bool FocusKeep => focusKeep;
        public ChoicePreview Preview => preview;

        public ChoiceController(GameSession session, HudView hud, BoardView board, Action<Command> send)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.hud = hud != null ? hud : throw new ArgumentNullException(nameof(hud));
            this.board = board != null ? board : throw new ArgumentNullException(nameof(board));
            this.send = send ?? throw new ArgumentNullException(nameof(send));
            session.Updated += OnUpdated;
            session.Presented += OnPresented;
            hud.ChoiceFocused += Focus;
        }

        public void Dispose()
        {
            session.Updated -= OnUpdated;
            session.Presented -= OnPresented;
            if (hud != null) hud.ChoiceFocused -= Focus;
        }

        void OnUpdated(SessionUpdate u)
        {
            if (u.State == null || u.State.Status != Status.Choose) { waiting = false; Hide(); return; }
            switch (u.Kind)
            {
                case SessionUpdateKind.Feedback: return; // a move while choosing: the cards stay (the HUD nudges them)
                case SessionUpdateKind.Present: waiting = true; Hide(); return; // after the 7th step's presentation
                default: waiting = false; Show(u.State); return; // Snap (Undo back into Choose), Rebuild, replay
            }
        }

        void OnPresented(State s)
        {
            if (!waiting) return;
            waiting = false;
            if (s != null && s.Status == Status.Choose) Show(s);
        }

        void Show(State s)
        {
            if (!ChoicePreview.TryOf(s, out preview)) { Hide(); return; }
            shown = true;
            focusKeep = true;
            Draw();
        }

        void Hide()
        {
            if (!shown) return;
            shown = false;
            preview = default;
            hud.ShowChoice(default);
            board.ShowChoice(false, default, BoardMarksView.ChoiceFocus.None);
        }

        void Draw()
        {
            hud.ShowChoice(new HudView.ChoiceCards
            {
                On = true, NextBody = preview.NextBody, Explain = ExplainChoices(), Guide = ShowGuide(), FocusKeep = focusKeep,
            });
            hud.PlaceChoice(board.ProjectedArea(), board.BrotherOnCanvas());
            board.ShowChoice(true, preview, focusKeep ? BoardMarksView.ChoiceFocus.Keep : BoardMarksView.ChoiceFocus.Switch);
        }

        /// Tutorial or language changed: redraw what is shown.
        public void Refresh()
        {
            if (shown) Draw();
        }

        public void Focus(bool keep)
        {
            if (!shown || keep == focusKeep) return;
            focusKeep = keep;
            Draw();
        }

        /// Tab: the other card.
        public void ToggleFocus() => Focus(!focusKeep);

        /// Enter: the focused card, the same command a click or K / X sends.
        public void Confirm()
        {
            if (shown) send(focusKeep ? Command.Keep : Command.Switch);
        }
    }
}
