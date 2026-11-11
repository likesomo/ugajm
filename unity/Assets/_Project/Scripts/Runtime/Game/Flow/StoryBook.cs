using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;

namespace SevenSteps.Game.Flow
{
    /// <summary>One ending card (design/story/story-integration.md 0.1: a picture and one line).</summary>
    public sealed class EndingPage
    {
        public string Line { get; }
        /// story.json image key (ending.1..4), "" = none. StoryImageSet.Get gives the sprite or null (text only).
        public string ImageKey { get; }
        /// The story.json entry (counted with StoryProgress.MarkShown once the page has been on screen).
        public string EntryId { get; }

        public EndingPage(string line, string imageKey, string entryId)
        {
            Line = !string.IsNullOrEmpty(line) ? line : throw new ArgumentException("ending line is empty", nameof(line));
            ImageKey = imageKey ?? "";
            EntryId = !string.IsNullOrEmpty(entryId) ? entryId : throw new ArgumentException("entry id is empty", nameof(entryId));
        }
    }

    /// <summary>The four ending cards for one won last stage (docs/architecture.md 16.8): branches from StoryRules.Ending.</summary>
    public sealed class EndingModel
    {
        public IReadOnlyList<EndingPage> Pages { get; }
        /// Chapters whose finale is cleared (StoryTruths.Truths) and the chapter count; card 3 is "allTruths" when equal.
        public int Truths { get; }
        public int ChapterCount { get; }
        public bool AllTruths => Truths == ChapterCount;

        public EndingModel(IReadOnlyList<EndingPage> pages, int truths, int chapterCount)
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            if (pages.Count != StoryRules.EndingCards) throw new ArgumentException($"{pages.Count} ending pages, expected {StoryRules.EndingCards}", nameof(pages));
            if (chapterCount < 1) throw new ArgumentOutOfRangeException(nameof(chapterCount), chapterCount, ">= 1");
            if (truths < 0 || truths > chapterCount) throw new ArgumentOutOfRangeException(nameof(truths), truths, $"0..{chapterCount}");
            Pages = pages;
            Truths = truths;
            ChapterCount = chapterCount;
        }
    }

    /// <summary>
    /// The story as the flow screens use it (docs/architecture.md 16, story-integration.md 5.2 "흐름 쪽 지점"): story.json
    /// (StoryCatalog) and what has been shown (StoryProgress, SaveSystem.Story). Title line, ending cards, loading carving.
    /// Off (<see cref="On"/> false) when there is no story.json or it failed its load check (A8: the game goes on without
    /// story lines). Dev runs (replay, smoke, flow / title checks) get a fresh memory-only progress and never save, like the
    /// tutorial guides. Pure C#: Bootstrap builds it from GameAssets and registers it in Services.
    /// </summary>
    public sealed class StoryBook
    {
        readonly Action save;
        readonly Func<string, bool> finaleCleared;

        /// story.json, or null when the story is off.
        public StoryCatalog Catalog { get; }
        public StoryProgress Progress { get; }
        public bool On => Catalog != null;
        /// Chapters in the game (chapters.json, = StoryCatalog.ChapterIds); 0 when off.
        public int ChapterCount => Catalog != null ? Catalog.ChapterIds.Count : 0;

        /// <param name="catalog">Null = story off.</param>
        /// <param name="progress">SaveSystem.Story, or a fresh one for dev runs.</param>
        /// <param name="save">SaveSystem.SaveStory, or null for dev runs (nothing written).</param>
        /// <param name="finaleCleared">Chapter id → its finale stage is cleared now (StoryTruths.FinaleCleared over the progress save).</param>
        public StoryBook(StoryCatalog catalog, StoryProgress progress, Action save, Func<string, bool> finaleCleared)
        {
            Catalog = catalog;
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.save = save;
            this.finaleCleared = finaleCleared ?? throw new ArgumentNullException(nameof(finaleCleared));
        }

        /// No story: every line is null and nothing is saved.
        public static StoryBook Off() => new StoryBook(null, new StoryProgress(), null, _ => false);

        /// The chapter's truth is found: its finale stage is cleared (story-integration.md 0.5; false when off).
        public bool FinaleCleared(string chapterId) => On && finaleCleared(chapterId);

        /// Chapters whose finale is cleared (0 when off). All collected = Truths == ChapterCount (A10).
        public int Truths
        {
            get
            {
                if (!On) return 0;
                int n = 0;
                foreach (var id in Catalog.ChapterIds) if (finaleCleared(id)) n++;
                return n;
            }
        }

        /// A chapter's truth (story.json "finale", its first line; the star map's carving tap). Null when off. Not counted:
        /// a replay of a line already shown.
        public string FinaleLine(string chapterId, Language lang)
        {
            if (!On) return null;
            var entry = Catalog.Find(StoryRules.Finale(chapterId));
            return Catalog.Line(entry, 0, lang);
        }

        /// The still-star line once every star is earned (story.json "stillStar" after "allStars"); null when off or none.
        public string StillStarLine(Language lang)
        {
            if (!On || !Catalog.TryFind(new StoryCue(StoryOn.StillStar, after: StoryOns.AfterAllStars), out var entry)) return null;
            return Catalog.Line(entry, StoryCatalog.Pick(entry, Progress.Count(entry.id)), lang);
        }

        /// The title's one sentence (story-integration.md 0.1 "왜 걷는가"), the "ending" branch once the ending has been
        /// seen. Null when off (the title keeps its own string).
        public string TitleLine(Language lang)
        {
            if (!On) return null;
            var cue = new StoryCue(StoryOn.Title, after: Progress.EndingSeen ? StoryOns.AfterEnding : "");
            return Catalog.Text(cue, Progress, lang, out _);
        }

        /// A loading carving (optional "loading" entry, story-integration.md 0.1: out of the jam data). Counted as shown
        /// here, so the next load picks the next line. Null when off or story.json has none.
        public string LoadingLine(Language lang)
        {
            if (!On || !Catalog.TryFind(new StoryCue(StoryOn.Loading), out var entry)) return null;
            string line = Catalog.Line(entry, StoryCatalog.Pick(entry, Progress.Count(entry.id)), lang);
            Progress.MarkShown(entry.id);
            return line;
        }

        /// The four ending cards for the won last stage. Null when off.
        public EndingModel Ending(State win, Language lang)
        {
            if (win == null) throw new ArgumentNullException(nameof(win));
            if (!On) return null;
            int count = ChapterCount, t = Math.Min(Truths, count);
            var pages = new EndingPage[StoryRules.EndingCards];
            for (int card = 1; card <= pages.Length; card++)
            {
                string line = Catalog.Text(StoryRules.Ending(win, card, t, count), Progress, lang, out var entry);
                pages[card - 1] = new EndingPage(line, entry.image, entry.id);
            }
            return new EndingModel(pages, t, count);
        }

        /// The ending is over (read or skipped after <paramref name="pagesSeen"/> pages): the shown cards are counted, the
        /// title and map turn to their after-ending branch, the final truth counts as seen when its card was on screen
        /// (story-integration.md 0.5 #7), and the story memory is saved (16.5 "엔딩 끝").
        public void EndingFinished(EndingModel model, int pagesSeen)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (pagesSeen < 0 || pagesSeen > model.Pages.Count) throw new ArgumentOutOfRangeException(nameof(pagesSeen), pagesSeen, $"0..{model.Pages.Count}");
            for (int i = 0; i < pagesSeen; i++) Progress.MarkShown(model.Pages[i].EntryId);
            Progress.MarkEndingSeen();
            if (model.AllTruths && pagesSeen >= 3) Progress.MarkFinalTruthSeen();
            Save();
        }

        /// SaveSystem.SaveStory at the save points (result panel open, ending end, app quit). Nothing in dev runs.
        public void Save() => save?.Invoke();
    }
}
