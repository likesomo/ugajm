using System;
using SevenSteps.Contracts;

namespace SevenSteps.Game.Flow
{
    /// <summary>What the chapter card shows (read-only; the constructor checks it). From chapters.json (docs/architecture.md 6.4).</summary>
    public sealed class ChapterCardModel
    {
        public string ChapterId { get; }
        /// 1-based chapter number (order in chapters.json).
        public int Number { get; }
        public string Planet { get; }
        /// Line shown when the chapter is entered (may be empty).
        public string Intro { get; }
        /// Every planet name in chapter order (the card's planet row; Number - 1 is this one). Empty when not given.
        public System.Collections.Generic.IReadOnlyList<string> Planets { get; }

        public ChapterCardModel(string chapterId, int number, string planet, string intro, System.Collections.Generic.IReadOnlyList<string> planets = null)
        {
            if (string.IsNullOrEmpty(chapterId)) throw new ArgumentException("chapter id is empty", nameof(chapterId));
            if (number < 1) throw new ArgumentOutOfRangeException(nameof(number), number, "1-based chapter number");
            if (string.IsNullOrEmpty(planet)) throw new ArgumentException($"chapter {chapterId} has no planet name", nameof(planet));
            ChapterId = chapterId;
            Number = number;
            Planet = planet;
            Intro = intro ?? "";
            Planets = planets ?? new string[0];
            if (Planets.Count > 0 && (number > Planets.Count || Planets[number - 1] != planet))
                throw new ArgumentException($"chapter {chapterId}: planet list does not have {planet} at {number}", nameof(planets));
        }
    }

    /// <summary>The chapter card as the controller sees it: an overlay that can show a model (ChapterCardView; a fake in tests).</summary>
    public interface IChapterCard : IOverlay
    {
        void Bind(ChapterCardModel model);
    }

    /// <summary>
    /// Chapter entry presentation (docs/scene-flow.md S4/S6 hook, 12.8): when the game flow enters a stage of another
    /// chapter, show the chapter card as an overlay (a board key closes the card and is applied as usual, feat/view 365d5bb; Esc, a click or the card's own timer also close it).
    /// Pure C#. No card (theme without one) or no chapter data (no chapters.json yet) = nothing happens.
    /// Card content and look are gameplay/UI's (prefab on UiTheme.chapterCard); when it shows is decided here.
    /// </summary>
    public sealed class ChapterIntroController : IDisposable
    {
        readonly GameFlowController flow;
        readonly OverlayStack overlays;
        readonly IChapterCard card;
        readonly Func<string, ChapterCardModel> modelFor;

        /// <param name="modelFor">Chapter id -> card model, null when there is nothing to show.</param>
        public ChapterIntroController(GameFlowController flow, OverlayStack overlays, IChapterCard card, Func<string, ChapterCardModel> modelFor)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.overlays = overlays ?? throw new ArgumentNullException(nameof(overlays));
            this.card = card; // optional
            this.modelFor = modelFor ?? throw new ArgumentNullException(nameof(modelFor));
            flow.ChapterEntering += OnChapterEntering;
        }

        public void Dispose() => flow.ChapterEntering -= OnChapterEntering;

        /// Raised after the card was pushed (tests, analytics hooks later).
        public event Action<ChapterCardModel> Shown;

        void OnChapterEntering(string fromChapter, string toChapter, int stage)
        {
            if (card == null || overlays.Contains(card)) return;
            var model = modelFor(toChapter);
            if (model == null) return;
            card.Bind(model);
            overlays.Push(card);
            Shown?.Invoke(model);
        }
    }
}
