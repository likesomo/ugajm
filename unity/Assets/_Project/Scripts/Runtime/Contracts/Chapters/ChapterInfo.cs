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
        /// One line for the planet select screen (docs 6.4). Empty until level design fills it (export: all chapters or none).
        public string tagline = "";
        /// ChapterPreset.id in AppConfig.chapters. Empty = same as id.
        public string artPreset = "";

        /// Translations (docs/architecture.md 13.5). The base fields above are ko.
        public ChapterText[] text = new ChapterText[0];

        public string ArtPreset => string.IsNullOrEmpty(artPreset) ? id : artPreset;

        public string Planet(Language lang) => lang == Languages.Base ? planet : Text(lang).planet;
        public string Concept(Language lang) => lang == Languages.Base ? concept : Text(lang).concept;
        public string Intro(Language lang) => lang == Languages.Base ? intro : Text(lang).intro;
        /// Empty when the data has no taglines yet (then the translation is not required either).
        public string Tagline(Language lang) => lang == Languages.Base || string.IsNullOrEmpty(tagline) ? tagline ?? "" : Text(lang).tagline;

        /// The translation for a non-base language. Missing = data error (export and LocalizationCheck catch it), so it throws.
        public ChapterText Text(Language lang)
        {
            string code = Languages.Code(lang);
            if (text != null) foreach (var t in text) if (t != null && t.lang == code) return t;
            throw new System.Collections.Generic.KeyNotFoundException($"chapters.json {id}: no \"{code}\" text");
        }
    }

    /// <summary>One translation of a chapter (chapters.json "text" entry).</summary>
    [Serializable]
    public sealed class ChapterText
    {
        /// "en" (Languages.Code).
        public string lang = "";
        public string planet = "";
        public string concept = "";
        public string intro = "";
        public string tagline = "";
    }
}
