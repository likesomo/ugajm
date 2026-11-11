using System;

namespace SevenSteps.Contracts
{
    /// <summary>Where a story line is delivered (design/story/story-integration.md 2.3). Append only.</summary>
    public enum StoryOn
    {
        Title, Map, Loading, SeventhFirst, Keep, Switch, Bridge, Seal, Fall, Vanish, Lose,
        Finale, Constellation, Ending, StillStar,
    }

    /// <summary>story.json "on" codes and "after" values.</summary>
    public static class StoryOns
    {
        /// "after" values (A11). Any = "".
        public const string AfterEnding = "ending";
        public const string AfterAllStars = "allStars";
        public const string AfterAllTruths = "allTruths";
        /// "dipper" "to" value for the last stage's node.
        public const string Circle = "circle";

        static readonly string[] Codes =
        {
            "title", "map", "loading", "seventhFirst", "keep", "switch", "bridge", "seal", "fall", "vanish", "lose",
            "finale", "constellation", "ending", "stillStar",
        };

        public static string Code(StoryOn on)
        {
            int i = (int)on;
            if (i < 0 || i >= Codes.Length) throw new ArgumentOutOfRangeException(nameof(on), on, "unknown StoryOn");
            return Codes[i];
        }

        public static bool TryParse(string code, out StoryOn on)
        {
            for (int i = 0; i < Codes.Length; i++)
                if (Codes[i] == code) { on = (StoryOn)i; return true; }
            on = StoryOn.Title;
            return false;
        }

        public static bool IsAfter(string after) =>
            after == "" || after == AfterEnding || after == AfterAllStars || after == AfterAllTruths;
    }

    /// <summary>
    /// What to show (not the text). Made by StoryRules (board moments, ending branches) or by a flow controller (title,
    /// map, loading, lose). StoryCatalog.Find turns it into an entry. Empty qualifier = none given.
    /// </summary>
    public readonly struct StoryCue : IEquatable<StoryCue>
    {
        public readonly StoryOn On;
        /// Chapter id, "" = none.
        public readonly string Chapter;
        /// 1..3 = BodyIndex + 1 (A2; StoryRules converts), 0 = none.
        public readonly int Brother;
        /// Statues 0..2, -1 = none.
        public readonly int Stones;
        /// Ending card 1..4, 0 = none.
        public readonly int Card;
        /// "", "ending", "allStars", "allTruths" (StoryOns.After*).
        public readonly string After;

        public StoryCue(StoryOn on, string chapter = "", int brother = 0, int stones = -1, int card = 0, string after = "")
        {
            if (brother < 0 || brother > 3) throw new ArgumentOutOfRangeException(nameof(brother), brother, "0..3");
            if (stones < -1 || stones > 2) throw new ArgumentOutOfRangeException(nameof(stones), stones, "-1..2");
            if (card < 0 || card > 4) throw new ArgumentOutOfRangeException(nameof(card), card, "0..4");
            if (!StoryOns.IsAfter(after ?? "")) throw new ArgumentException($"unknown after \"{after}\"", nameof(after));
            On = on; Chapter = chapter ?? ""; Brother = brother; Stones = stones; Card = card; After = after ?? "";
        }

        public bool Equals(StoryCue o) =>
            On == o.On && Chapter == o.Chapter && Brother == o.Brother && Stones == o.Stones && Card == o.Card && After == o.After;
        public override bool Equals(object obj) => obj is StoryCue c && Equals(c);
        public override int GetHashCode() => ((int)On * 397) ^ (Chapter ?? "").GetHashCode() ^ (Brother << 4) ^ ((Stones + 1) << 8) ^ (Card << 12) ^ (After ?? "").GetHashCode();
        public override string ToString() =>
            $"{StoryOns.Code(On)}(chapter \"{Chapter}\", brother {Brother}, stones {Stones}, card {Card}, after \"{After}\")";
    }
}
