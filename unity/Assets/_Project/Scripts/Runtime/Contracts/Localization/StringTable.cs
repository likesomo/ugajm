using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// UI strings of one language (docs/architecture.md 13.2): Data/Strings/&lt;code&gt;.json,
    /// { "entries": [ { "key": "hud.undo", "text": "되돌리기 Z" } ] }. Keys: lowercase, dot separated, never renamed.
    /// A missing key is a bug (LocalizationCheck catches it before a build): Get throws.
    /// </summary>
    public sealed class StringTable
    {
        [Serializable] public sealed class Entry { public string key = ""; public string text = ""; }
        [Serializable] public sealed class FileDto { public Entry[] entries = new Entry[0]; }

        readonly Dictionary<string, string> map;

        public Language Language { get; }
        public int Count => map.Count;
        public IEnumerable<string> Keys => map.Keys;

        public StringTable(Language language, IEnumerable<Entry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            Language = language;
            map = new Dictionary<string, string>();
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.key)) throw new ArgumentException($"{Languages.Code(language)}: entry without a key", nameof(entries));
                if (e.text == null) throw new ArgumentException($"{Languages.Code(language)}: {e.key} has no text", nameof(entries));
                if (map.ContainsKey(e.key)) throw new ArgumentException($"{Languages.Code(language)}: key {e.key} twice", nameof(entries));
                map.Add(e.key, e.text);
            }
        }

        /// Parses a Data/Strings/*.json text. Broken JSON throws (the file is ours, not player data).
        public static StringTable FromJson(Language language, string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException($"{Languages.Code(language)}: empty string table", nameof(json));
            var dto = JsonUtility.FromJson<FileDto>(json);
            return new StringTable(language, dto?.entries ?? throw new ArgumentException("not a string table", nameof(json)));
        }

        public bool Has(string key) => key != null && map.ContainsKey(key);

        public string Get(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            return map.TryGetValue(key, out var text) ? text
                : throw new KeyNotFoundException($"[SevenSteps] string \"{key}\" missing in {Languages.Code(Language)} (LocalizationCheck)");
        }

        /// Get + string.Format ({0}, {1}...). Only positional placeholders (13.2).
        public string Format(string key, params object[] args) => string.Format(Get(key), args);
    }

    /// <summary>One language's table file as an asset reference (GameAssets.strings, docs/architecture.md 13.2).</summary>
    [Serializable]
    public struct StringTableAsset
    {
        public Language language;
        [Tooltip("Data/Strings/<code>.json")]
        public TextAsset json;
    }

    /// <summary>
    /// The current UI language and its table (docs/architecture.md 13.2-13.3). Pure C#; created once per run by the
    /// bootstrap with every table the build has, registered in Services. Views redraw on <see cref="Changed"/>.
    /// </summary>
    public sealed class Localizer
    {
        readonly Dictionary<Language, StringTable> tables = new Dictionary<Language, StringTable>();

        public Language Current { get; private set; }
        public StringTable Table => tables[Current];
        public event Action<Language> Changed;

        /// <param name="tables">At least the base language (ko). <paramref name="current"/> must be one of them.</param>
        public Localizer(IEnumerable<StringTable> tables, Language current)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            foreach (var t in tables)
            {
                if (t == null) throw new ArgumentException("null table", nameof(tables));
                if (this.tables.ContainsKey(t.Language)) throw new ArgumentException($"two {Languages.Code(t.Language)} tables", nameof(tables));
                this.tables.Add(t.Language, t);
            }
            if (!this.tables.ContainsKey(Languages.Base)) throw new ArgumentException("the base language (ko) table is required", nameof(tables));
            if (!this.tables.ContainsKey(current)) throw new ArgumentException($"no {Languages.Code(current)} table", nameof(current));
            Current = current;
        }

        /// Tables from GameAssets.strings. A language whose asset is missing is a build error (LocalizationCheck), so it throws.
        public static Localizer FromAssets(IEnumerable<StringTableAsset> assets, Language current)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            var tables = new List<StringTable>();
            foreach (var a in assets)
            {
                if (a.json == null) throw new ArgumentException($"GameAssets.strings: {Languages.Code(a.language)} has no json", nameof(assets));
                tables.Add(StringTable.FromJson(a.language, a.json.text));
            }
            return new Localizer(tables, current);
        }

        public bool Has(Language lang) => tables.ContainsKey(lang);
        public IEnumerable<Language> Available => tables.Keys;

        public void Set(Language lang)
        {
            if (!tables.ContainsKey(lang)) throw new ArgumentException($"no {Languages.Code(lang)} table", nameof(lang));
            if (lang == Current) return;
            Current = lang;
            Changed?.Invoke(lang);
        }

        public string Get(string key) => Table.Get(key);
        public string Format(string key, params object[] args) => Table.Format(key, args);
    }
}
