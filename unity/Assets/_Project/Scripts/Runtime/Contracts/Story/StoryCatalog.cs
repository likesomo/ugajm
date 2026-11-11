using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// story.json, loaded and checked (docs/architecture.md 16.3, story-integration.md 3.2). Immutable after load.
    /// Checks are assertions: any failure throws InvalidDataException naming the entry and the reason (the build-time
    /// scene-validate and an EditMode test load the real file, so a shipped build has passed them). At boot, a failure
    /// turns the story off and the game goes on (A8): that is the caller's job.
    /// Character-level checks (length, KS X 1001, symbols, banned words, particles) are check-story.mjs and GlyphCheck.
    /// </summary>
    public sealed class StoryCatalog
    {
        public const int Version = 1;
        public const string BrotherToken = "{brother}";

        readonly StoryFileDto file;
        readonly List<StoryEntryDto> entries;
        readonly string[] chapterIds;

        public IReadOnlyList<string> ImageKeys => file.images;
        public IReadOnlyList<StoryDipperDto> Dipper => file.dipper;
        public IReadOnlyList<StoryEntryDto> Entries => entries;
        public IReadOnlyList<string> ChapterIds => chapterIds;

        StoryCatalog(StoryFileDto file, string[] chapterIds)
        {
            this.file = file;
            this.chapterIds = chapterIds;
            entries = new List<StoryEntryDto>(file.entries);
        }

        /// <param name="chapterIds">chapters.json ids in order (LevelCatalog.Chapters). Required entries and the dipper are checked against them.</param>
        public static StoryCatalog FromJson(string json, IReadOnlyList<string> chapterIds)
        {
            if (string.IsNullOrEmpty(json)) throw new InvalidDataException("story.json: empty");
            if (chapterIds == null || chapterIds.Count == 0) throw new InvalidDataException("story.json: no chapter ids to check against (chapters.json)");
            StoryFileDto f;
            try { f = JsonUtility.FromJson<StoryFileDto>(json); }
            catch (ArgumentException e) { throw new InvalidDataException("story.json: not valid JSON: " + e.Message); }
            if (f == null) throw new InvalidDataException("story.json: not valid JSON");
            var ids = new string[chapterIds.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = chapterIds[i] ?? "";
            Normalize(f);
            var c = new StoryCatalog(f, ids);
            c.Check();
            return c;
        }

        // ---------- lookup ----------

        /// The most specific entry whose qualifiers all match (an empty entry qualifier matches anything). None:
        /// KeyNotFoundException. A tie cannot happen in a loaded catalog (checked on load).
        public StoryEntryDto Find(StoryCue cue)
        {
            if (TryFind(cue, out var e)) return e;
            throw new KeyNotFoundException("story.json: no entry for " + cue);
        }

        /// Like Find, false when nothing matches (optional points: map, loading, moments, constellation, stillStar).
        public bool TryFind(StoryCue cue, out StoryEntryDto entry)
        {
            entry = Best(cue, out bool tie);
            if (tie) throw new InvalidOperationException("story.json: two entries tie for " + cue); // load check prevents this
            return entry != null;
        }

        /// Which line of the pool to show: lines[shownCount % lines.Length] (no randomness; story-integration.md 2.2).
        public static int Pick(StoryEntryDto entry, int shownCount)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (shownCount < 0) throw new ArgumentOutOfRangeException(nameof(shownCount), shownCount, ">= 0");
            return shownCount % entry.lines.Length;
        }

        /// Raw line (with any {brother} left in). A missing translation is a data error: KeyNotFoundException.
        public string Line(StoryEntryDto entry, int index, Language lang)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (index < 0 || index >= entry.lines.Length) throw new ArgumentOutOfRangeException(nameof(index), index, $"entry {entry.id} has {entry.lines.Length} lines");
            var l = entry.lines[index];
            if (lang == Languages.Base) return l.line;
            string code = Languages.Code(lang);
            foreach (var t in l.text) if (t.lang == code) return t.line;
            throw new KeyNotFoundException($"story.json {entry.id}/{l.id}: no \"{code}\" line");
        }

        /// The brother's name (1..3) in a language.
        public string BrotherName(int n, Language lang)
        {
            foreach (var b in file.brothers)
                if (b.n == n)
                {
                    if (lang == Languages.Base) return b.name;
                    string code = Languages.Code(lang);
                    foreach (var t in b.text) if (t.lang == code) return t.name;
                    throw new KeyNotFoundException($"story.json brother {n}: no \"{code}\" name");
                }
            throw new ArgumentOutOfRangeException(nameof(n), n, "brother is 1..3");
        }

        /// The text for a cue: Find, pick by how often the entry was shown, put the cue's brother into {brother}.
        /// Does not count it: the caller calls StoryProgress.MarkShown(entry.id) when the line actually shows.
        public string Text(StoryCue cue, StoryProgress progress, Language lang, out StoryEntryDto entry)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            entry = Find(cue);
            string s = Line(entry, Pick(entry, progress.Count(entry.id)), lang);
            if (s.IndexOf(BrotherToken, StringComparison.Ordinal) < 0) return s;
            if (cue.Brother == 0) throw new InvalidOperationException($"story.json {entry.id}: line has {BrotherToken} but the cue names no brother");
            return s.Replace(BrotherToken, BrotherName(cue.Brother, lang));
        }

        // ---------- matching ----------

        static bool Matches(StoryEntryDto e, StoryCue c) =>
            (e.chapter == "" || e.chapter == c.Chapter) &&
            (e.brother == 0 || e.brother == c.Brother) &&
            (e.stones == -1 || e.stones == c.Stones) &&
            (e.card == 0 || e.card == c.Card) &&
            (e.after == "" || e.after == c.After);

        static int Specificity(StoryEntryDto e) =>
            (e.chapter != "" ? 1 : 0) + (e.brother != 0 ? 1 : 0) + (e.stones != -1 ? 1 : 0) + (e.card != 0 ? 1 : 0) + (e.after != "" ? 1 : 0);

        StoryEntryDto Best(StoryCue cue, out bool tie)
        {
            StoryEntryDto best = null;
            int bestSpec = -1;
            tie = false;
            string on = StoryOns.Code(cue.On);
            foreach (var e in entries)
            {
                if (e.on != on || !Matches(e, cue)) continue;
                int s = Specificity(e);
                if (s > bestSpec) { best = e; bestSpec = s; tie = false; }
                else if (s == bestSpec) tie = true;
            }
            return best;
        }

        // ---------- load checks (story-integration.md 3.1 / 3.2, minimal UI 0.3) ----------

        static void Normalize(StoryFileDto f)
        {
            f.languages ??= new string[0];
            f.brothers ??= new StoryBrotherDto[0];
            f.images ??= new string[0];
            f.dipper ??= new StoryDipperDto[0];
            f.entries ??= new StoryEntryDto[0];
            foreach (var e in f.entries)
            {
                if (e == null) throw new InvalidDataException("story.json: null entry");
                e.id ??= ""; e.on ??= ""; e.chapter ??= ""; e.after ??= ""; e.image ??= "";
                e.lines ??= new StoryLineDto[0];
                foreach (var l in e.lines)
                {
                    if (l == null) throw new InvalidDataException($"story.json {e.id}: null line");
                    l.id ??= ""; l.line ??= ""; l.text ??= new StoryLineTextDto[0];
                }
            }
            foreach (var b in f.brothers)
            {
                if (b == null) throw new InvalidDataException("story.json: null brother");
                b.name ??= ""; b.text ??= new StoryBrotherTextDto[0];
            }
        }

        static InvalidDataException Bad(string where, string why) => new InvalidDataException($"story.json {where}: {why}");

        void Check()
        {
            if (file.version != Version) throw Bad("version", $"{file.version}, this build reads {Version}");
            CheckLanguages();
            CheckBrothers();
            var images = new HashSet<string>();
            foreach (var k in file.images)
                if (string.IsNullOrEmpty(k) || !images.Add(k)) throw Bad("images", $"empty or repeated key \"{k}\"");
            var ids = new HashSet<string>();
            foreach (var e in entries) CheckEntry(e, ids, images);
            CheckTies();
            CheckRequired();
            CheckDipper();
        }

        void CheckLanguages()
        {
            if (file.languages.Length == 0 || file.languages[0] != Languages.Code(Languages.Base))
                throw Bad("languages", $"must start with \"{Languages.Code(Languages.Base)}\"");
            foreach (var lang in Languages.All)
                if (Array.IndexOf(file.languages, Languages.Code(lang)) < 0) throw Bad("languages", $"no \"{Languages.Code(lang)}\" (this build shows it)");
        }

        void CheckBrothers()
        {
            for (int n = 1; n <= 3; n++)
            {
                int found = 0;
                foreach (var b in file.brothers)
                    if (b.n == n)
                    {
                        found++;
                        if (b.name == "") throw Bad($"brother {n}", "empty ko name");
                        foreach (var lang in Languages.All)
                            if (lang != Languages.Base && !HasText(b.text, Languages.Code(lang), t => t.lang, t => t.name))
                                throw Bad($"brother {n}", $"no \"{Languages.Code(lang)}\" name");
                    }
                if (found != 1) throw Bad("brothers", $"brother {n} appears {found} times (want once)");
            }
            if (file.brothers.Length != 3) throw Bad("brothers", "want exactly 1, 2 and 3");
        }

        static bool HasText<T>(T[] texts, string code, Func<T, string> lang, Func<T, string> value)
        {
            foreach (var t in texts) if (t != null && lang(t) == code && !string.IsNullOrEmpty(value(t))) return true;
            return false;
        }

        static bool IsId(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char ch in s) if (!((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '.')) return false;
            return true;
        }

        void CheckEntry(StoryEntryDto e, HashSet<string> ids, HashSet<string> images)
        {
            if (!IsId(e.id)) throw Bad($"entry \"{e.id}\"", "id must be [a-z0-9.]+");
            if (!ids.Add(e.id)) throw Bad(e.id, "id used twice");
            if (!StoryOns.TryParse(e.on, out var on)) throw Bad(e.id, $"unknown on \"{e.on}\"");
            if (e.chapter != "" && Array.IndexOf(chapterIds, e.chapter) < 0) throw Bad(e.id, $"chapter \"{e.chapter}\" is not in chapters.json");
            if (e.brother < 0 || e.brother > 3) throw Bad(e.id, $"brother {e.brother} (0..3)");
            if (e.stones < -1 || e.stones > 2) throw Bad(e.id, $"stones {e.stones} (-1..2)");
            if (e.card < 0 || e.card > 4) throw Bad(e.id, $"card {e.card} (0..4)");
            if (!StoryOns.IsAfter(e.after)) throw Bad(e.id, $"after \"{e.after}\"");
            if (e.card != 0 && on != StoryOn.Ending) throw Bad(e.id, "card is for on \"ending\" only");
            if (e.image != "" && !images.Contains(e.image)) throw Bad(e.id, $"image \"{e.image}\" is not in images");
            if (e.lines.Length == 0) throw Bad(e.id, "no lines");
            var lineIds = new HashSet<string>();
            bool brotherAllowed = on == StoryOn.Keep || on == StoryOn.Switch || on == StoryOn.Bridge || on == StoryOn.Seal ||
                                  on == StoryOn.Fall || on == StoryOn.Vanish || on == StoryOn.SeventhFirst || (on == StoryOn.Ending && e.card == 1);
            foreach (var l in e.lines)
            {
                if (l.id == "" || !lineIds.Add(l.id)) throw Bad(e.id, $"line id \"{l.id}\" empty or repeated");
                if (l.line == "") throw Bad($"{e.id}/{l.id}", "empty ko line");
                foreach (var lang in Languages.All)
                    if (lang != Languages.Base && !HasText(l.text, Languages.Code(lang), t => t.lang, t => t.line))
                        throw Bad($"{e.id}/{l.id}", $"no \"{Languages.Code(lang)}\" line");
                foreach (var s in AllTexts(l))
                {
                    string rest = s.Replace(BrotherToken, "");
                    if (rest.IndexOf('{') >= 0 || rest.IndexOf('}') >= 0) throw Bad($"{e.id}/{l.id}", $"only {BrotherToken} may be substituted");
                    if (!brotherAllowed && s.IndexOf(BrotherToken, StringComparison.Ordinal) >= 0)
                        throw Bad($"{e.id}/{l.id}", $"{BrotherToken} where the cue names no brother");
                }
            }
        }

        static IEnumerable<string> AllTexts(StoryLineDto l)
        {
            yield return l.line;
            foreach (var t in l.text) if (t != null && t.line != null) yield return t.line;
        }

        /// Two entries of the same "on" that can both match one cue with the same specificity, where no more specific
        /// entry covers that cue, make the trigger ambiguous (3.1 #10).
        void CheckTies()
        {
            for (int i = 0; i < entries.Count; i++)
                for (int j = i + 1; j < entries.Count; j++)
                {
                    var a = entries[i];
                    var b = entries[j];
                    if (a.on != b.on || Specificity(a) != Specificity(b)) continue;
                    if (!Compatible(a.chapter, b.chapter, "") || !Compatible(a.brother, b.brother, 0) || !Compatible(a.stones, b.stones, -1) ||
                        !Compatible(a.card, b.card, 0) || !Compatible(a.after, b.after, "")) continue;
                    StoryOns.TryParse(a.on, out var on);
                    var union = new StoryCue(on,
                        a.chapter != "" ? a.chapter : b.chapter,
                        a.brother != 0 ? a.brother : b.brother,
                        a.stones != -1 ? a.stones : b.stones,
                        a.card != 0 ? a.card : b.card,
                        a.after != "" ? a.after : b.after);
                    Best(union, out bool tie);
                    if (tie) throw Bad($"{a.id} / {b.id}", "ambiguous: both match " + union);
                }
        }

        static bool Compatible<T>(T a, T b, T any) where T : IEquatable<T> => a.Equals(any) || b.Equals(any) || a.Equals(b);

        /// Required cues (minimal UI, story-integration.md 0.3 / 0.5). Each must resolve to an entry that names every
        /// qualifier the cue sets (a branch is written out, not left to a catch-all).
        void CheckRequired()
        {
            Require(new StoryCue(StoryOn.Title));
            Require(new StoryCue(StoryOn.Title, after: StoryOns.AfterEnding));
            Require(new StoryCue(StoryOn.Lose));
            foreach (var id in chapterIds) Require(new StoryCue(StoryOn.Finale, chapter: id));
            for (int b = 1; b <= 3; b++) Require(new StoryCue(StoryOn.Ending, brother: b, card: 1));
            for (int s = 0; s <= 2; s++) Require(new StoryCue(StoryOn.Ending, stones: s, card: 2));
            Require(new StoryCue(StoryOn.Ending, card: 3));
            Require(new StoryCue(StoryOn.Ending, card: 3, after: StoryOns.AfterAllTruths));
            Require(new StoryCue(StoryOn.Ending, card: 4));
        }

        void Require(StoryCue cue)
        {
            var e = Best(cue, out _);
            if (e == null) throw Bad("required", "no entry for " + cue);
            bool explicitOk = (cue.Chapter == "" || e.chapter == cue.Chapter) && (cue.Brother == 0 || e.brother == cue.Brother) &&
                              (cue.Stones == -1 || e.stones == cue.Stones) && (cue.Card == 0 || e.card == cue.Card) &&
                              (cue.After == "" ? e.after == "" : e.after == cue.After);
            if (!explicitOk) throw Bad(e.id, "is the only match for required " + cue + "; write that branch out");
        }

        void CheckDipper()
        {
            var seen = new HashSet<string>();
            var from = new HashSet<string>();
            foreach (var d in file.dipper)
            {
                if (d == null) throw Bad("dipper", "null line");
                if (Array.IndexOf(chapterIds, d.from) < 0) throw Bad("dipper", $"from \"{d.from}\" is not a chapter id");
                if (d.to != StoryOns.Circle && Array.IndexOf(chapterIds, d.to) < 0) throw Bad("dipper", $"to \"{d.to}\" is not a chapter id or \"{StoryOns.Circle}\"");
                if (!seen.Add(d.from + ">" + d.to)) throw Bad("dipper", $"line {d.from}>{d.to} twice");
                from.Add(d.from);
            }
            foreach (var id in chapterIds)
                if (!from.Contains(id)) throw Bad("dipper", $"chapter \"{id}\" starts no line");
        }
    }
}
