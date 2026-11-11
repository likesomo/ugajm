using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Flow;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// Flow-side story (docs/architecture.md 16.8, story-integration.md 0.5) on the real Data/story.json and chapters.json:
    /// title branch, ending cards and their branches, what the end of the ending marks, the still-star line, story off.
    public sealed class StoryBookTests
    {
        const string StoryPath = "Assets/_Project/Data/story.json", ChaptersPath = "Assets/_Project/Data/chapters.json";

        static readonly Level Seven = Level.Parse("seven", "", 3, new[] { "##########", "#S......E#", "##########" });

        static StoryCatalog Catalog()
        {
            if (!File.Exists(StoryPath)) Assert.Ignore("Data/story.json not exported yet");
            var chapters = JsonUtility.FromJson<ChapterFileDto>(File.ReadAllText(ChaptersPath));
            return StoryCatalog.FromJson(File.ReadAllText(StoryPath), chapters.chapters.Select(c => c.id).ToArray());
        }

        static State Won()
        {
            var s = Rules.NewState(Seven);
            for (int k = 0; k < Rules.Steps; k++) s = Rules.Step(s, Dir.Right).State;
            Assert.AreEqual(Status.Win, s.Status);
            return s;
        }

        [Test]
        public void OffGivesNoLinesAndSavesNothing()
        {
            var book = StoryBook.Off();
            Assert.IsFalse(book.On);
            Assert.IsNull(book.TitleLine(Language.Ko));
            Assert.IsNull(book.LoadingLine(Language.Ko));
            Assert.IsNull(book.StillStarLine(Language.Ko));
            Assert.IsNull(book.Ending(Won(), Language.Ko));
            Assert.AreEqual(0, book.Truths);
            Assert.IsFalse(book.FinaleCleared("moss"));
        }

        [Test]
        public void TitleTurnsToItsAfterEndingBranch()
        {
            var cat = Catalog();
            var book = new StoryBook(cat, new StoryProgress(), null, _ => false);
            string before = book.TitleLine(Language.Ko);
            Assert.AreEqual(cat.Line(cat.Find(new StoryCue(StoryOn.Title)), 0, Language.Ko), before);
            book.Progress.MarkEndingSeen();
            string after = book.TitleLine(Language.En);
            Assert.AreEqual(cat.Line(cat.Find(new StoryCue(StoryOn.Title, after: StoryOns.AfterEnding)), 0, Language.En), after);
            Assert.AreNotEqual(cat.Line(cat.Find(new StoryCue(StoryOn.Title)), 0, Language.En), after);
        }

        [Test]
        public void EndingCardsFollowTheBranches()
        {
            var cat = Catalog();
            int count = cat.ChapterIds.Count;
            bool all = false;
            var book = new StoryBook(cat, new StoryProgress(), null, _ => all);
            var m = book.Ending(Won(), Language.Ko);
            Assert.AreEqual(StoryRules.EndingCards, m.Pages.Count);
            Assert.AreEqual(cat.Find(new StoryCue(StoryOn.Ending, brother: 1, card: 1)).id, m.Pages[0].EntryId, "card 1: the brother at the exit");
            Assert.AreEqual(cat.Find(new StoryCue(StoryOn.Ending, stones: 0, card: 2)).id, m.Pages[1].EntryId, "card 2: no statue");
            Assert.AreEqual(cat.Find(new StoryCue(StoryOn.Ending, card: 3)).id, m.Pages[2].EntryId, "card 3: not every truth");
            Assert.AreEqual(0, m.Truths);
            Assert.IsFalse(m.AllTruths);
            foreach (var p in m.Pages)
            {
                Assert.IsFalse(string.IsNullOrEmpty(p.Line));
                StringAssert.DoesNotContain(StoryCatalog.BrotherToken, p.Line);
                Assert.IsTrue(cat.ImageKeys.Contains(p.ImageKey), p.ImageKey);
            }
            all = true;
            m = book.Ending(Won(), Language.En);
            Assert.AreEqual(count, m.Truths);
            Assert.IsTrue(m.AllTruths);
            Assert.AreEqual(cat.Find(new StoryCue(StoryOn.Ending, card: 3, after: StoryOns.AfterAllTruths)).id, m.Pages[2].EntryId);
        }

        [Test]
        public void EndingEndMarksSeenCountsAndSaves()
        {
            var cat = Catalog();
            int saves = 0;
            var book = new StoryBook(cat, new StoryProgress(), () => saves++, _ => true);
            var m = book.Ending(Won(), Language.Ko);
            book.EndingFinished(m, 2); // skipped on card 2: the final truth (card 3) was not seen
            Assert.IsTrue(book.Progress.EndingSeen);
            Assert.IsFalse(book.Progress.FinalTruthSeen);
            Assert.AreEqual(1, book.Progress.Count(m.Pages[0].EntryId));
            Assert.AreEqual(0, book.Progress.Count(m.Pages[2].EntryId));
            Assert.AreEqual(1, saves);
            book.EndingFinished(m, 4);
            Assert.IsTrue(book.Progress.FinalTruthSeen);
            Assert.AreEqual(2, saves);
            Assert.Throws<ArgumentOutOfRangeException>(() => book.EndingFinished(m, 5));
            StoryBook.Off().Save(); // a dev run: nothing to call
        }

        [Test]
        public void StillStarLineAndTruthCount()
        {
            var cat = Catalog();
            var book = new StoryBook(cat, new StoryProgress(), null, id => id == cat.ChapterIds[0]);
            Assert.IsFalse(string.IsNullOrEmpty(book.StillStarLine(Language.Ko)));
            Assert.AreEqual(1, book.Truths);
            Assert.IsTrue(book.FinaleCleared(cat.ChapterIds[0]));
            Assert.IsFalse(book.FinaleCleared(cat.ChapterIds[1]));
        }
    }
}
