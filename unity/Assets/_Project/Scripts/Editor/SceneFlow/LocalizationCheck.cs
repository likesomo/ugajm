using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Pre-build localization check (docs/architecture.md 13.6), run by SceneFlowValidator:
    /// - GameAssets.strings: ko present, each language once, every table parses (no empty or duplicate keys),
    ///   and every language has exactly the ko keys (missing and extra keys are errors);
    /// - every literal key in code (.Get("a.b") / .Format("a.b", ...)) exists in the ko table;
    /// - glyphs per language: that language's strings, level and chapter texts must be in the language's title AND body
    ///   fonts of every UiTheme (a text may be shown in either role); number-role characters in the number font;
    /// - Korean UI literals left in Scripts/Runtime/Game: WARN while gameplay/UI moves the text to keys (13.7), then ERROR
    ///   (set <see cref="KoreanLiteralsAreErrors"/>).
    /// Level texts: each language's LevelDto.TextFor (13.5); a stage without that language is skipped here (the export check reports it).
    /// </summary>
    public static class LocalizationCheck
    {
        const string CodeRoot = "Assets/_Project/Scripts/Runtime/Game";

        /// Flip to true when the UI text has moved to the string tables (gameplay/UI tells scene flow).
        public static bool KoreanLiteralsAreErrors = true;

        /// Characters shown with the number role in every language (board labels, steps, bodies, statues, stars).
        public const string NumberRoleCharacters = "0123456789/·★"; // not U+00D7 "×": Jua has none (write "x")

        /// A key: lower case words joined by dots, at least two parts ("hud.undo").
        static readonly Regex KeyCall = new Regex(@"\.(?:Get|Format)\s*\(\s*""([a-z0-9_]+(?:\.[a-z0-9_]+)+)""", RegexOptions.Compiled);

        public static void Check(GameAssets assets, List<string> issues)
        {
            if (assets == null) { issues.Add("ERROR localization: no GameAssets"); return; }
            var tables = LoadTables(assets, issues);
            if (!tables.TryGetValue(Languages.Base, out var ko)) return; // reported by LoadTables

            foreach (var kv in tables)
            {
                if (kv.Key == Languages.Base) continue;
                string code = Languages.Code(kv.Key);
                foreach (var key in ko.Keys.Where(k => !kv.Value.Has(k)).OrderBy(k => k)) issues.Add($"ERROR strings {code}: missing key {key}");
                foreach (var key in kv.Value.Keys.Where(k => !ko.Has(k)).OrderBy(k => k)) issues.Add($"ERROR strings {code}: key {key} is not in ko");
            }

            var koreanFiles = new List<string>();
            foreach (var file in Directory.GetFiles(CodeRoot, "*.cs", SearchOption.AllDirectories))
            {
                string rel = file.Replace('\\', '/');
                string src = File.ReadAllText(file);
                foreach (Match m in KeyCall.Matches(StripComments(src)))
                    if (!ko.Has(m.Groups[1].Value)) issues.Add($"ERROR strings: {rel} uses key {m.Groups[1].Value}, not in ko.json");
                int korean = GlyphCheck.StringLiterals(src).Count(l => l.text.Any(IsHangul) && !NativeNames.Contains(l.text));
                if (korean > 0) koreanFiles.Add($"{rel} ({korean})");
            }
            if (koreanFiles.Count > 0)
                issues.Add($"{(KoreanLiteralsAreErrors ? "ERROR" : "WARN")} Korean UI literals left in code (move them to ko.json, 13.6): {string.Join(", ", koreanFiles)}");

            CheckGlyphs(assets, tables, issues);
        }

        static Dictionary<Language, StringTable> LoadTables(GameAssets assets, List<string> issues)
        {
            var tables = new Dictionary<Language, StringTable>();
            foreach (var a in assets.strings ?? new StringTableAsset[0])
            {
                string code = Languages.Code(a.language);
                if (a.json == null) { issues.Add($"ERROR GameAssets.strings {code}: json is not set"); continue; }
                if (tables.ContainsKey(a.language)) { issues.Add($"ERROR GameAssets.strings: {code} appears twice"); continue; }
                try { tables[a.language] = StringTable.FromJson(a.language, a.json.text); }
                catch (Exception e) { issues.Add($"ERROR {AssetDatabase.GetAssetPath(a.json)}: {e.Message}"); }
            }
            if (!tables.ContainsKey(Languages.Base)) issues.Add($"ERROR GameAssets.strings: no {Languages.Code(Languages.Base)} table (the base language)");
            foreach (var lang in Languages.All)
                if (!tables.ContainsKey(lang)) issues.Add($"ERROR GameAssets.strings: no {Languages.Code(lang)} table");
            return tables;
        }

        /// Every text a language shows, with where it comes from.
        static List<(string text, string where)> TextsOf(Language lang, StringTable table, GameAssets assets)
        {
            string code = Languages.Code(lang);
            var texts = new List<(string, string)>();
            if (table != null)
                foreach (var key in table.Keys) texts.Add((table.Get(key), $"strings {code} {key}"));
            if (assets.levelsJson != null)
            {
                var file = JsonUtility.FromJson<LevelFileDto>(assets.levelsJson.text);
                for (int k = 0; k < file.levels.Length; k++)
                {
                    var dto = file.levels[k];
                    if (lang != Languages.Base && !(dto.text ?? new LevelTextDto[0]).Any(t => t != null && t.lang == code)) continue; // export check (13.6) reports it
                    var t = dto.TextFor(code);
                    texts.Add((t.name, $"levels.json stage {k + 1} name ({code})"));
                    texts.Add((t.hint, $"levels.json stage {k + 1} hint ({code})"));
                    texts.Add((t.story, $"levels.json stage {k + 1} story ({code})"));
                }
            }
            if (assets.chaptersJson != null)
            {
                var chapters = JsonUtility.FromJson<ChapterFileDto>(assets.chaptersJson.text);
                foreach (var c in chapters.chapters ?? new ChapterInfo[0])
                {
                    if (lang != Languages.Base && !(c.text ?? new ChapterText[0]).Any(t => t.lang == code)) continue; // export check (13.6) reports it
                    texts.Add((c.Planet(lang), $"chapters.json {c.id} planet ({code})"));
                    texts.Add((c.Intro(lang), $"chapters.json {c.id} intro ({code})"));
                    texts.Add((c.Tagline(lang), $"chapters.json {c.id} tagline ({code})"));
                }
            }
            // story.json (docs/architecture.md 16, story-integration.md 3.2): every line and brother name, in both role fonts.
            // A story.json that fails its load check is reported by SceneFlowValidator.ValidateStory.
            var story = StoryCatalogOf(assets);
            if (story != null)
            {
                for (int n = 1; n <= 3; n++) texts.Add((story.BrotherName(n, lang), $"story.json brother {n} ({code})"));
                foreach (var e in story.Entries)
                    for (int i = 0; i < e.lines.Length; i++)
                        texts.Add((story.Line(e, i, lang).Replace(StoryCatalog.BrotherToken, ""), $"story.json {e.id}/{e.lines[i].id} ({code})"));
            }
            // cutscenes.json (design/story/cutscenes.md): every line of every page. A file that fails its load check is
            // reported by SceneFlowValidator.ValidateCutscenes.
            if (assets.cutscenesJson != null)
            {
                CutsceneCatalog cutscenes = null;
                try { cutscenes = CutsceneCatalog.FromJson(assets.cutscenesJson.text); } catch (FormatException) { }
                if (cutscenes != null)
                    foreach (var s in cutscenes.Scenes)
                        foreach (var p in s.pages)
                            for (int i = 0; i < p.lines.Length; i++)
                                texts.Add((CutsceneCatalog.Line(p, i, lang), $"cutscenes.json {s.id}.{p.id} line {i + 1} ({code})"));
            }
            return texts;
        }

        /// story.json loaded against chapters.json, or null when there is none or it does not load.
        internal static StoryCatalog StoryCatalogOf(GameAssets assets)
        {
            if (assets.storyJson == null || assets.chaptersJson == null) return null;
            var chapters = JsonUtility.FromJson<ChapterFileDto>(assets.chaptersJson.text);
            try { return StoryCatalog.FromJson(assets.storyJson.text, (chapters.chapters ?? new ChapterInfo[0]).Select(c => c.id).ToArray()); }
            catch (Exception) { return null; }
        }

        static void CheckGlyphs(GameAssets assets, Dictionary<Language, StringTable> tables, List<string> issues)
        {
            var cmaps = new Dictionary<Font, HashSet<int>>();
            HashSet<int> Cmap(Font f)
            {
                if (cmaps.TryGetValue(f, out var set)) return set;
                try { set = GlyphCheck.ReadCmap(File.ReadAllBytes(AssetDatabase.GetAssetPath(f))); }
                catch (Exception e) { issues.Add($"ERROR glyphs: cannot read {AssetDatabase.GetAssetPath(f)}: {e.Message}"); set = null; }
                return cmaps[f] = set;
            }

            var reported = new HashSet<string>();
            void Missing(string text, string where, Font font)
            {
                var set = Cmap(font);
                if (set == null || string.IsNullOrEmpty(text)) return;
                for (int i = 0; i < text.Length; i++)
                {
                    int cp = char.ConvertToUtf32(text, i);
                    if (char.IsSurrogatePair(text, i)) i++;
                    // GlyphCheck.Allowed (U+00B7) was seen drawn with Jua only: other fonts must have it in their cmap.
                    if (cp < 0x20 || set.Contains(cp) || (font.name.StartsWith("Jua", StringComparison.Ordinal) && GlyphCheck.Allowed.Contains(cp))) continue;
                    string line = $"ERROR glyph U+{cp:X4} '{char.ConvertFromUtf32(cp)}' is not in {font.name}: {where}";
                    if (reported.Add(line)) issues.Add(line);
                }
            }

            foreach (var guid in AssetDatabase.FindAssets("t:UiTheme", new[] { "Assets/_Project" }))
            {
                var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(AssetDatabase.GUIDToAssetPath(guid));
                if (theme.number != null) Missing(NumberRoleCharacters, $"number role ({theme.name})", theme.number);
                foreach (var lang in Languages.All)
                {
                    LanguageFonts fonts;
                    try { fonts = theme.FontsFor(lang); }
                    catch (KeyNotFoundException) { continue; } // UiTheme.Validate reports it
                    tables.TryGetValue(lang, out var table);
                    foreach (var (text, where) in TextsOf(lang, table, assets))
                    {
                        if (fonts.title != null) Missing(text, where, fonts.title);
                        if (fonts.body != null) Missing(text, where, fonts.body);
                    }
                }
            }
        }

        /// Language names in their own language (SettingsPanel.NativeName): code, not table strings, because they read the
        /// same in every UI language (13.4). Not counted as Korean UI literals (gameplay/UI request, 93a3a89).
        static readonly HashSet<string> NativeNames = new HashSet<string>(Languages.All.Select(SettingsPanel.NativeName));

        static bool IsHangul(char c) => (c >= '가' && c <= '힣') || (c >= 'ㄱ' && c <= 'ㆎ');

        /// Removes // and /* */ comments (string contents kept) so commented-out calls are not checked.
        static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            int i = 0, n = src.Length;
            while (i < n)
            {
                char c = src[i];
                if (c == '"' || c == '\'')
                {
                    char q = c; sb.Append(c); i++;
                    while (i < n && src[i] != q) { if (src[i] == '\\' && i + 1 < n) { sb.Append(src[i]); i++; } sb.Append(src[i]); i++; }
                    if (i < n) { sb.Append(src[i]); i++; }
                    continue;
                }
                if (c == '/' && i + 1 < n && src[i + 1] == '/') { while (i < n && src[i] != '\n') i++; continue; }
                if (c == '/' && i + 1 < n && src[i + 1] == '*') { i += 2; while (i + 1 < n && !(src[i] == '*' && src[i + 1] == '/')) i++; i += 2; continue; }
                sb.Append(c); i++;
            }
            return sb.ToString();
        }
    }
}
