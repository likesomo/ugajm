using System;
using System.Collections.Generic;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// What the player has been shown of the story (docs/architecture.md 16.5, A5 / A12). Saved under
    /// <see cref="SaveSystem.StoryKey"/> as JSON { version, counts[{id,n}], endingSeen, carvingSeen[], finalTruthSeen }.
    /// The Mark methods change memory only; SaveSystem.SaveStory() writes (result panel, ending end, star-map carving
    /// end, app quit: a WebGL save is an IndexedDB write). Presentation flags only: a lost or broken value just shows a
    /// line or a carving once more. Cleared by SaveSystem.StartNewPlayer. Fields are for JsonUtility.
    /// </summary>
    [Serializable]
    public sealed class StoryProgress
    {
        public const int Version = 1;

        public int version = Version;
        public List<StoryCount> counts = new List<StoryCount>();
        public bool endingSeen;
        /// Chapter ids whose carving appeared on the star map (A12: ids, not numbers).
        public List<string> carvingSeen = new List<string>();
        public bool finalTruthSeen;

        /// Times the entry was shown (line pick: StoryCatalog.Pick).
        public int Count(string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) throw new ArgumentException("entry id is empty", nameof(entryId));
            foreach (var c in counts) if (c.id == entryId) return c.n;
            return 0;
        }

        public void MarkShown(string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) throw new ArgumentException("entry id is empty", nameof(entryId));
            foreach (var c in counts)
                if (c.id == entryId) { if (c.n < int.MaxValue) c.n++; return; }
            counts.Add(new StoryCount { id = entryId, n = 1 });
        }

        public bool EndingSeen => endingSeen;
        public void MarkEndingSeen() => endingSeen = true;

        public bool CarvingSeen(string chapterId) => carvingSeen.Contains(Id(chapterId));
        public void MarkCarvingSeen(string chapterId)
        {
            string id = Id(chapterId);
            if (!carvingSeen.Contains(id)) carvingSeen.Add(id);
        }

        public bool FinalTruthSeen => finalTruthSeen;
        public void MarkFinalTruthSeen() => finalTruthSeen = true;

        static string Id(string chapterId) =>
            string.IsNullOrEmpty(chapterId) ? throw new ArgumentException("chapter id is empty", nameof(chapterId)) : chapterId;

        /// After JsonUtility: no nulls, no negative or repeated counts (a hand-edited or broken save).
        internal void Repair()
        {
            counts ??= new List<StoryCount>();
            carvingSeen ??= new List<string>();
            var seen = new HashSet<string>();
            counts.RemoveAll(c => c == null || string.IsNullOrEmpty(c.id) || c.n <= 0 || !seen.Add(c.id));
            var chapters = new HashSet<string>();
            carvingSeen.RemoveAll(id => string.IsNullOrEmpty(id) || !chapters.Add(id));
        }
    }

    [Serializable]
    public sealed class StoryCount
    {
        public string id = "";
        public int n;
    }
}
