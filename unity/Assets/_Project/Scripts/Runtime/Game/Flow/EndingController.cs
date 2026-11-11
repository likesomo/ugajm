using System;

namespace SevenSteps.Game.Flow
{
    /// <summary>The ending screen as the controller sees it: an overlay that shows one card (EndingView; a fake in tests).</summary>
    public interface IEndingView : IOverlay
    {
        /// Shows card <paramref name="index"/> of <paramref name="count"/>: its picture (StoryImageSet, none = text only)
        /// and its line. The last card offers "to the map" instead of "next".
        void ShowPage(int index, int count, EndingPage page);
        /// Next button, a click on the card, Enter / Space / Right (GameController).
        event Action NextRequested;
        /// Skip button. Esc closes the overlay through the BackNavigator (OverlayStack.Changed), which also skips.
        event Action SkipRequested;
    }

    /// <summary>
    /// The story ending after the last stage (docs/architecture.md 16.8, story-integration.md 0.1 / 0.5): four cards from
    /// story.json "ending" (branches by StoryRules.Ending), opened by the last result's [끝까지 보기]
    /// (GameFlowController.EndingRequested). Next turns a card; Skip or Esc ends at once. At the end, or on a skip,
    /// <see cref="Finished"/> is raised once with how many cards were seen (GameController: StoryBook.EndingFinished, then
    /// the star map). Pure C#.
    /// </summary>
    public sealed class EndingController : IDisposable
    {
        readonly OverlayStack overlays;
        readonly IEndingView view;
        EndingModel model;
        int seen;

        public EndingController(OverlayStack overlays, IEndingView view)
        {
            this.overlays = overlays ?? throw new ArgumentNullException(nameof(overlays));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            view.NextRequested += Next;
            view.SkipRequested += Skip;
            overlays.Changed += OnOverlaysChanged;
        }

        public void Dispose()
        {
            view.NextRequested -= Next;
            view.SkipRequested -= Skip;
            overlays.Changed -= OnOverlaysChanged;
        }

        public bool IsOpen => model != null;
        /// 0-based card on screen; -1 when closed.
        public int Page { get; private set; } = -1;
        /// The cards being shown (null when closed).
        public EndingModel Model => model;

        /// Raised for every card shown (flow check, telemetry later).
        public event Action<int> PageShown;
        /// The ending is over: the model, cards seen (1..4) and whether it was skipped. Raised once per Begin.
        public event Action<EndingModel, int, bool> Finished;

        /// Opens the ending on card 1. Ignored while it is open.
        public void Begin(EndingModel cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            if (IsOpen) return;
            model = cards;
            seen = 0;
            overlays.Push(view);
            Show(0);
        }

        /// The next card, or the end after the last one.
        public void Next()
        {
            if (!IsOpen) return;
            if (Page + 1 < model.Pages.Count) Show(Page + 1);
            else End(false);
        }

        public void Skip()
        {
            if (IsOpen) End(true);
        }

        void Show(int page)
        {
            Page = page;
            seen = Math.Max(seen, page + 1);
            view.ShowPage(page, model.Pages.Count, model.Pages[page]);
            PageShown?.Invoke(page);
        }

        void End(bool skipped)
        {
            var done = model;
            model = null;
            Page = -1;
            if (overlays.Contains(view)) overlays.Close(view); // re-enters OnOverlaysChanged closed: nothing
            Finished?.Invoke(done, seen, skipped);
        }

        /// Esc: the BackNavigator popped the view. That is a skip.
        void OnOverlaysChanged()
        {
            if (IsOpen && !overlays.Contains(view)) End(true);
        }
    }
}
