using System;

namespace SevenSteps.Contracts
{
    // JSON shapes of unity/Assets/_Project/Data/story.json (copied from design/story/story.json by unity.sh levels after
    // check-story.mjs; docs/architecture.md 16, design/story/story-integration.md 2.2). JsonUtility: no dictionaries, no
    // null, empty = "" / 0 / -1. Read through StoryCatalog, which checks everything on load (16.3).

    [Serializable]
    public sealed class StoryFileDto
    {
        public string about = "";
        public int version;
        /// Same as chapters.json: ko first, then every translation.
        public string[] languages = new string[0];
        public StoryBrotherDto[] brothers = new StoryBrotherDto[0];
        /// Metadata for analysis only. Not read at run time.
        public StoryBeatDto[] beats = new StoryBeatDto[0];
        /// Every image key an entry may name (StoryImageSet resolves them).
        public string[] images = new string[0];
        /// Star-map lines (A13): drawn when the "from" chapter's finale is cleared.
        public StoryDipperDto[] dipper = new StoryDipperDto[0];
        public StoryEntryDto[] entries = new StoryEntryDto[0];
    }

    [Serializable]
    public sealed class StoryBrotherDto
    {
        /// 1..3 (= BodyIndex + 1).
        public int n;
        /// ko name. Ends without a final consonant (check-story #11), so ko lines can put a particle after {brother}.
        public string name = "";
        public StoryBrotherTextDto[] text = new StoryBrotherTextDto[0];
    }

    [Serializable]
    public sealed class StoryBrotherTextDto
    {
        public string lang = "";
        public string name = "";
    }

    [Serializable]
    public sealed class StoryBeatDto
    {
        public string chapter = "";
        public int star;
        public string part = "";
        public string beat = "";
        public int focus;
    }

    [Serializable]
    public sealed class StoryDipperDto
    {
        /// A chapter id.
        public string from = "";
        /// A chapter id, or "circle" (the last stage's node).
        public string to = "";
    }

    [Serializable]
    public sealed class StoryEntryDto
    {
        /// Unique, [a-z0-9.]+ . StoryProgress counts by it.
        public string id = "";
        /// StoryOns code ("finale", "ending", ...).
        public string on = "";
        /// Qualifiers. Empty value = any: chapter "", brother 0, stones -1, card 0, after "".
        public string chapter = "";
        public int brother;
        public int stones = -1;
        public int card;
        public string after = "";
        /// Key in StoryFileDto.images, "" = none.
        public string image = "";
        public StoryLineDto[] lines = new StoryLineDto[0];
    }

    [Serializable]
    public sealed class StoryLineDto
    {
        /// Unique inside its entry.
        public string id = "";
        /// ko.
        public string line = "";
        public StoryLineTextDto[] text = new StoryLineTextDto[0];
    }

    [Serializable]
    public sealed class StoryLineTextDto
    {
        public string lang = "";
        public string line = "";
    }
}
