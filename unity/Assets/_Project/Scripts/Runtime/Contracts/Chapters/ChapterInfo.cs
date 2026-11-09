using System;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Data/chapters.json (copied from design/levels/chapters.json by unity.sh levels; docs/architecture.md 6.4).
    /// Order of <see cref="chapters"/> = chapter order. Read with JsonUtility.FromJson&lt;ChapterFileDto&gt;.
    /// </summary>
    [Serializable]
    public sealed class ChapterFileDto
    {
        public ChapterInfo[] chapters = new ChapterInfo[0];

        /// Chapter by id. Empty id = first chapter (stages without "chapter"); null when the file has no chapters.
        /// Unknown id throws: levels.json and chapters.json disagree (export-levels.mjs checks this before export).
        public ChapterInfo Get(string id)
        {
            if (chapters == null || chapters.Length == 0) return null;
            if (string.IsNullOrEmpty(id)) return chapters[0];
            foreach (var c in chapters)
                if (c != null && c.id == id) return c;
            throw new ArgumentException($"chapters.json: no chapter \"{id}\"", nameof(id));
        }
    }

    /// <summary>One chapter (one small planet) as level design writes it. Text for the UI, art via <see cref="ArtPreset"/>.</summary>
    [Serializable]
    public sealed class ChapterInfo
    {
        /// Stable lowercase id, e.g. "moss". levels.json "chapter" refers to it.
        public string id = "";
        /// Planet name shown to the player.
        public string planet = "";
        public string concept = "";
        /// Line shown when the chapter is entered.
        public string intro = "";
        /// ChapterPreset.id in AppConfig.chapters. Empty = same as id.
        public string artPreset = "";

        public string ArtPreset => string.IsNullOrEmpty(artPreset) ? id : artPreset;
    }
}
