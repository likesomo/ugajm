using System;
using System.Collections.Generic;
using SevenSteps.Contracts;

namespace SevenSteps.Game.Flow
{
    /// <summary>What the cutscene view draws: one page's picture key and lines, and how many lines are out so far.</summary>
    public sealed class CutsceneFrame
    {
        public string SceneId { get; }
        /// 0-based page and the page count of the scene.
        public int Page { get; }
        public int PageCount { get; }
        /// cutscene.&lt;scene&gt;.&lt;page&gt;: a sprite in the cutscene image set, or none (the plain card).
        public string ImageKey { get; }
        /// Every line of the page in the current language (the view lays out the box for all of them).
        public IReadOnlyList<string> Lines { get; }
        /// Lines shown, 1..Lines.Count.
        public int Shown { get; }

        public CutsceneFrame(string sceneId, int page, int pageCount, string imageKey, IReadOnlyList<string> lines, int shown)
        {
            SceneId = !string.IsNullOrEmpty(sceneId) ? sceneId : throw new ArgumentException("scene id is empty", nameof(sceneId));
            if (pageCount < 1) throw new ArgumentOutOfRangeException(nameof(pageCount), pageCount, ">= 1");
            if (page < 0 || page >= pageCount) throw new ArgumentOutOfRangeException(nameof(page), page, $"0..{pageCount - 1}");
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
            if (lines.Count < 1) throw new ArgumentException("a page has at least one line", nameof(lines));
            if (shown < 1 || shown > lines.Count) throw new ArgumentOutOfRangeException(nameof(shown), shown, $"1..{lines.Count}");
            Page = page;
            PageCount = pageCount;
            ImageKey = imageKey ?? "";
            Shown = shown;
        }
    }

    /// <summary>The storybook viewer as the controller sees it: an overlay that draws one frame (U5CutsceneView; a fake in tests).</summary>
    public interface ICutsceneView : IOverlay
    {
        void ShowFrame(CutsceneFrame frame);
        /// A tap / click anywhere, the next icon, Space, Enter or Right.
        event Action NextRequested;
        /// The skip icon. Esc closes the overlay through the BackNavigator (OverlayStack.Changed), which also skips.
        event Action SkipRequested;
    }

    /// <summary>
    /// Plays one storybook cutscene (design/story/cutscenes.md 2): the page's lines come out one by one, next shows the
    /// next line, then the next page; after the last line of the last page, or on a skip (icon or Esc), the overlay closes
    /// and the callback given to <see cref="Begin"/> runs once with whether it was skipped. Pure C#: the scene roots
    /// (TitleScreen, GameController) decide when and what comes after.
    /// </summary>
    public sealed class CutsceneController : IDisposable
    {
        readonly OverlayStack overlays;
        readonly ICutsceneView view;
        readonly Func<Language> language;
        CutsceneSceneDto scene;
        Action<bool> done;

        public CutsceneController(OverlayStack overlays, ICutsceneView view, Func<Language> language)
        {
            this.overlays = overlays ?? throw new ArgumentNullException(nameof(overlays));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.language = language ?? throw new ArgumentNullException(nameof(language));
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

        public bool IsOpen => scene != null;
        /// The scene playing (null when closed).
        public CutsceneSceneDto Scene => scene;
        /// 0-based page on screen; -1 when closed.
        public int Page { get; private set; } = -1;
        /// Lines of the page shown so far (1..); 0 when closed.
        public int Shown { get; private set; }
        /// The frame on screen (null when closed).
        public CutsceneFrame Frame { get; private set; }

        /// Opens <paramref name="cutscene"/> on its first line. <paramref name="onDone"/> runs once when it ends (true = skipped).
        /// Asserts: nothing is playing yet.
        public void Begin(CutsceneSceneDto cutscene, Action<bool> onDone)
        {
            if (cutscene == null) throw new ArgumentNullException(nameof(cutscene));
            if (onDone == null) throw new ArgumentNullException(nameof(onDone));
            if (cutscene.pages == null || cutscene.pages.Length == 0) throw new ArgumentException($"cutscene {cutscene.id} has no pages", nameof(cutscene));
            if (IsOpen) throw new InvalidOperationException($"cutscene {scene.id} is playing; {cutscene.id} cannot begin");
            scene = cutscene;
            done = onDone;
            Show(0, 1); // the view has its frame before the stack shows it
            overlays.Push(view);
        }

        /// The next line, else the next page, else the end.
        public void Next()
        {
            if (!IsOpen) return;
            if (Shown < scene.pages[Page].lines.Length) Show(Page, Shown + 1);
            else if (Page + 1 < scene.pages.Length) Show(Page + 1, 1);
            else End(false);
        }

        public void Skip()
        {
            if (IsOpen) End(true);
        }

        /// The language changed: the same frame in the new language.
        public void Redraw()
        {
            if (IsOpen) Show(Page, Shown);
        }

        void Show(int page, int shown)
        {
            Page = page;
            Shown = shown;
            var p = scene.pages[page];
            Frame = new CutsceneFrame(scene.id, page, scene.pages.Length, p.image, CutsceneCatalog.Lines(p, language()), shown);
            view.ShowFrame(Frame);
        }

        void End(bool skipped)
        {
            var then = done;
            scene = null;
            done = null;
            Page = -1;
            Shown = 0;
            Frame = null;
            if (overlays.Contains(view)) overlays.Close(view); // re-enters OnOverlaysChanged closed: nothing
            then(skipped);
        }

        /// Esc: the BackNavigator popped the view. That is a skip.
        void OnOverlaysChanged()
        {
            if (IsOpen && !overlays.Contains(view)) End(true);
        }

        /// The prologue plays on a new game: the data has a newGame scene, nothing was played yet, the player has not seen
        /// it (TutorialIds.Prologue, cleared by -newplayer) and the run is not a dev check (title / flow checks press play).
        public static bool PlaysOnNewGame(CutsceneSceneDto prologue, bool canContinue, bool seen, bool devRun) =>
            prologue != null && !canContinue && !seen && !devRun;
    }
}
