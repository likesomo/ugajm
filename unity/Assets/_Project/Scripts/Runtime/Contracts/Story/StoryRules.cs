using System;
using SevenSteps.Core;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Which story point a session update or a won ending reaches (docs/architecture.md 16.4, story-integration.md 4.2).
    /// Pure and stateless (A3): no time, randomness, save data or engine. "Once per attempt", the first-seventh-step filter
    /// and skipping while QueuedCount &gt; 0 belong to StoryDirector. The brother number is BodyIndex + 1, converted here
    /// only (A2).
    /// </summary>
    public static class StoryRules
    {
        /// Ending cards (story-integration.md 0.1: four pictures, one line each).
        public const int EndingCards = 4;

        /// A board moment: Present updates only. KEEP → keep, SWITCH → switch; otherwise the first of Fall, Vanish,
        /// Bridge, Seal in that order. The brother is the body that gave the command (BodyIndex.Current(Prev) + 1).
        /// None when the command ended the attempt (Win or Lose: the result / lose panel speaks) or for plain steps.
        public static bool TryMoment(SessionUpdate u, out StoryCue cue)
        {
            cue = default;
            if (u.Kind != SessionUpdateKind.Present || u.Result == null || u.Prev == null || u.State == null) return false;
            if (u.State.Status == Status.Win || u.State.Status == Status.Lose) return false;
            int brother = Brother(u.Prev);
            if (u.Command.Type == CommandType.Keep && u.Result.Has(GameEventType.Stone)) { cue = new StoryCue(StoryOn.Keep, brother: brother); return true; }
            if (u.Command.Type == CommandType.Switch && u.Result.Has(GameEventType.Switch)) { cue = new StoryCue(StoryOn.Switch, brother: brother); return true; }
            if (u.Command.Type != CommandType.Move) return false;
            if (u.Result.Has(GameEventType.Fall)) { cue = new StoryCue(StoryOn.Fall, brother: brother); return true; }
            if (u.Result.Has(GameEventType.Vanish)) { cue = new StoryCue(StoryOn.Vanish, brother: brother); return true; }
            if (u.Result.Has(GameEventType.Bridge)) { cue = new StoryCue(StoryOn.Bridge, brother: brother); return true; }
            if (u.Result.Has(GameEventType.Seal)) { cue = new StoryCue(StoryOn.Seal, brother: brother); return true; }
            return false;
        }

        /// The move that was a seventh step (any event at step 7: Choose, Win, Bridge, Seal, Vanish, Stone, ...). Always
        /// raised for it; StoryDirector shows "seventhFirst" only while StoryProgress has never shown it (A3).
        public static bool TrySeventh(SessionUpdate u, out StoryCue cue)
        {
            cue = default;
            if (u.Kind != SessionUpdateKind.Present || u.Result == null || u.Prev == null || u.Command.Type != CommandType.Move) return false;
            foreach (var e in u.Result.Events)
                if (e.StepIdx == Rules.Steps) { cue = new StoryCue(StoryOn.SeventhFirst, brother: Brother(u.Prev)); return true; }
            return false;
        }

        /// The ending card for the won last stage (story-integration.md 0.5): card 1 by the brother who reached the exit,
        /// card 2 by the statues standing (0..2, min(2, count)), card 3 "allTruths" when every chapter's finale is cleared,
        /// card 4 fixed.
        public static StoryCue Ending(State win, int card, int truths, int chapterCount)
        {
            if (win == null) throw new ArgumentNullException(nameof(win));
            if (win.Status != Status.Win) throw new ArgumentException($"ending needs a won state, got {win.Status}", nameof(win));
            if (card < 1 || card > EndingCards) throw new ArgumentOutOfRangeException(nameof(card), card, $"1..{EndingCards}");
            if (chapterCount < 1) throw new ArgumentOutOfRangeException(nameof(chapterCount), chapterCount, ">= 1");
            if (truths < 0 || truths > chapterCount) throw new ArgumentOutOfRangeException(nameof(truths), truths, $"0..{chapterCount}");
            switch (card)
            {
                case 1: return new StoryCue(StoryOn.Ending, brother: Brother(win), card: 1);
                case 2: return new StoryCue(StoryOn.Ending, stones: Math.Min(2, win.Statues.Length), card: 2);
                case 3: return new StoryCue(StoryOn.Ending, card: 3, after: truths == chapterCount ? StoryOns.AfterAllTruths : "");
                default: return new StoryCue(StoryOn.Ending, card: 4);
            }
        }

        /// The finale line (truth) of a chapter: shown on a won finale stage (StoryTruths.IsFinaleStage).
        public static StoryCue Finale(string chapterId)
        {
            if (string.IsNullOrEmpty(chapterId)) throw new ArgumentException("chapter id is empty", nameof(chapterId));
            return new StoryCue(StoryOn.Finale, chapter: chapterId);
        }

        /// StoryCue.Brother (1..3) of the body playing in a state. Stages have at most 3 bodies (export check); a 4th or
        /// 5th body (old data) cycles 1, 2, 3 like its look (12.6).
        public static int Brother(State s) => BodyIndex.Current(s) % 3 + 1;
    }
}
