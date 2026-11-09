using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SevenSteps.Core;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Every character the UI can show must exist in the UI font (UiTheme.font, Jua): a missing glyph draws nothing in
    /// WebGL (U+2212 minus, arrows, box rules were found by hand, 2026-11-09). Part of SceneFlowValidator, so it also runs
    /// before every build. Checked text: Text components in prefabs under Assets/_Project that use the theme font,
    /// levels.json stage names, hints and stories, chapters.json planet names and intros (chapter card), UI/Credits.txt, and string literals in Scripts/Runtime/Game (comments skipped;
    /// literals that contain a non-ASCII character are UI text and are checked whole; ASCII-only literals are logs and keys).
    /// The font's coverage comes from its TTF/OTF cmap table (formats 4 and 12), not from Font.HasCharacter.
    /// </summary>
    public static class GlyphCheck
    {
        const string ProjectRoot = "Assets/_Project";
        const string CodeRoot = "Assets/_Project/Scripts/Runtime/Game";
        const string CreditsPath = "Assets/_Project/UI/Credits.txt";
        const int MaxReportsPerSource = 20;
        /// Prefab layouts are first written in code by UiBuilder.
        static readonly string[] ExtraCode = { "Assets/_Project/Scripts/Editor/SceneFlow/UiBuilder.cs" };

        /// Not in the Unicode cmap but drawn anyway (seen in WebGL): U+00B7 "·" comes from Jua's old Mac Roman table
        /// (HUD key help, result text, footprints). Add others only after seeing them in a WebGL build.
        public static readonly HashSet<int> Allowed = new HashSet<int> { 0x00B7 };

        /// Adds "ERROR ..." lines for missing glyphs. Returns the number of distinct missing characters.
        public static int Check(UiTheme theme, GameAssets assets, List<string> issues)
        {
            if (theme == null || theme.font == null) { issues.Add("ERROR glyphs: no UiTheme font to check against"); return 1; }
            string fontPath = AssetDatabase.GetAssetPath(theme.font);
            HashSet<int> cmap;
            try { cmap = ReadCmap(File.ReadAllBytes(fontPath)); }
            catch (Exception e) { issues.Add($"ERROR glyphs: cannot read the cmap of {fontPath}: {e.Message}"); return 1; }

            var missing = new SortedDictionary<int, List<string>>();
            void Text(string text, string where)
            {
                if (string.IsNullOrEmpty(text)) return;
                for (int i = 0; i < text.Length; i++)
                {
                    int cp = char.ConvertToUtf32(text, i);
                    if (char.IsSurrogatePair(text, i)) i++;
                    if (cp < 0x20 || cmap.Contains(cp) || Allowed.Contains(cp)) continue; // control characters (\n, \t) are not drawn
                    if (!missing.TryGetValue(cp, out var at)) missing[cp] = at = new List<string>();
                    if (at.Count < MaxReportsPerSource && !at.Contains(where)) at.Add(where);
                }
            }

            // Not "all ASCII once": Jua has no backtick (U+0060), so ASCII is checked where it is actually shown.
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ProjectRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var t in prefab.GetComponentsInChildren<Text>(true))
                    if (t.font == null || t.font == theme.font) Text(t.text, $"{path} ({t.name})");
            }

            if (assets != null && assets.levelsJson != null)
            {
                var file = JsonUtility.FromJson<LevelFileDto>(assets.levelsJson.text);
                for (int k = 0; k < file.levels.Length; k++)
                {
                    Text(file.levels[k].name, $"levels.json stage {k + 1} name");
                    Text(file.levels[k].hint, $"levels.json stage {k + 1} hint");
                    Text(file.levels[k].story, $"levels.json stage {k + 1} story");
                }
            }
            if (assets != null && assets.chaptersJson != null)
            {
                var chapters = JsonUtility.FromJson<SevenSteps.Contracts.ChapterFileDto>(assets.chaptersJson.text);
                foreach (var c in chapters.chapters ?? new SevenSteps.Contracts.ChapterInfo[0])
                {
                    Text(c.planet, $"chapters.json {c.id} planet");
                    Text(c.intro, $"chapters.json {c.id} intro");
                }
            }

            var credits = AssetDatabase.LoadAssetAtPath<TextAsset>(CreditsPath);
            if (credits != null) Text(credits.text, CreditsPath);

            var codeFiles = new List<string>(Directory.GetFiles(CodeRoot, "*.cs", SearchOption.AllDirectories));
            codeFiles.AddRange(ExtraCode);
            foreach (var file in codeFiles)
            {
                string rel = file.Replace('\\', '/');
                foreach (var (literal, line) in StringLiterals(File.ReadAllText(file)))
                    if (HasNonAscii(literal)) Text(literal, $"{rel}:{line}");
            }

            foreach (var kv in missing)
                issues.Add($"ERROR glyph U+{kv.Key:X4} '{char.ConvertFromUtf32(kv.Key)}' is not in {Path.GetFileName(fontPath)}: {string.Join(", ", kv.Value)}");
            return missing.Count;
        }

        static bool HasNonAscii(string s)
        {
            foreach (char c in s) if (c > '~') return true;
            return false;
        }

        // ---------- C# string literals (comments skipped) ----------

        /// Contents of "...", @"..." and $"..." literals with their line numbers. Interpolation holes {...} are dropped
        /// (they are code); escapes are decoded for the common cases (\n, \t, \", \\, \uXXXX).
        public static IEnumerable<(string text, int line)> StringLiterals(string src)
        {
            int i = 0, line = 1, n = src.Length;
            var nested = new List<string>();
            while (i < n)
            {
                char c = src[i];
                if (c == '\n') { line++; i++; continue; }
                if (c == '/' && i + 1 < n && src[i + 1] == '/') { while (i < n && src[i] != '\n') i++; continue; }
                if (c == '/' && i + 1 < n && src[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < n && !(src[i] == '*' && src[i + 1] == '/')) { if (src[i] == '\n') line++; i++; }
                    i += 2;
                    continue;
                }
                if (c == '\'')
                {
                    i++;
                    while (i < n && src[i] != '\'') { if (src[i] == '\\') i++; i++; }
                    i++;
                    continue;
                }
                bool verbatim = false, interpolated = false;
                int j = i;
                while (j < n && (src[j] == '@' || src[j] == '$')) { if (src[j] == '@') verbatim = true; else interpolated = true; j++; }
                if (j < n && src[j] == '"' && (j > i || c == '"'))
                {
                    int startLine = line;
                    var sb = new StringBuilder();
                    i = j + 1;
                    int depth = 0;
                    while (i < n)
                    {
                        char d = src[i];
                        if (d == '\n') line++;
                        if (interpolated && d == '{')
                        {
                            if (i + 1 < n && src[i + 1] == '{' && depth == 0) { sb.Append('{'); i += 2; continue; }
                            depth++; i++; continue;
                        }
                        if (interpolated && depth > 0)
                        {
                            if (d == '}') depth--;
                            else if (d == '"')
                            {
                                // A literal inside a hole ($"{(a ? "x" : "")}") is shown too: report it as its own literal.
                                var inner = new StringBuilder();
                                i++;
                                while (i < n && src[i] != '"') { if (src[i] == '\\') i++; if (i < n) inner.Append(src[i]); i++; }
                                nested.Add(inner.ToString());
                            }
                            i++;
                            continue;
                        }
                        if (interpolated && d == '}' && i + 1 < n && src[i + 1] == '}') { sb.Append('}'); i += 2; continue; }
                        if (verbatim)
                        {
                            if (d == '"') { if (i + 1 < n && src[i + 1] == '"') { sb.Append('"'); i += 2; continue; } break; }
                            sb.Append(d); i++; continue;
                        }
                        if (d == '"') break;
                        if (d == '\\' && i + 1 < n)
                        {
                            char e = src[i + 1];
                            if (e == 'u' && i + 5 < n) { sb.Append((char)Convert.ToInt32(src.Substring(i + 2, 4), 16)); i += 6; continue; }
                            sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e);
                            i += 2;
                            continue;
                        }
                        sb.Append(d); i++;
                    }
                    i++; // closing quote
                    yield return (sb.ToString(), startLine);
                    foreach (var inner in nested) yield return (inner, startLine);
                    nested.Clear();
                    continue;
                }
                i++;
            }
        }

        // ---------- TrueType / OpenType cmap ----------

        /// Code points the font maps to a glyph: Unicode subtables only (platform 0, or 3 = Windows), formats 4 and 12.
        /// Hangul is checked per syllable too: Jua has only the 2350 KS X 1001 syllables, not all 11172.
        public static HashSet<int> ReadCmap(byte[] f)
        {
            int U16(int o) => (f[o] << 8) | f[o + 1];
            long U32(int o) => ((long)f[o] << 24) | ((long)f[o + 1] << 16) | ((long)f[o + 2] << 8) | f[o + 3];
            int tables = U16(4);
            int cmapOffset = -1;
            for (int t = 0; t < tables; t++)
            {
                int rec = 12 + t * 16;
                if (f[rec] == 'c' && f[rec + 1] == 'm' && f[rec + 2] == 'a' && f[rec + 3] == 'p') cmapOffset = (int)U32(rec + 8);
            }
            if (cmapOffset < 0) throw new InvalidDataException("no cmap table");

            var set = new HashSet<int>();
            int subtables = U16(cmapOffset + 2);
            for (int s = 0; s < subtables; s++)
            {
                int platform = U16(cmapOffset + 4 + s * 8);
                if (platform != 0 && platform != 3) continue; // 1 = Macintosh (Mac Roman): not trusted, see Allowed
                int sub = cmapOffset + (int)U32(cmapOffset + 4 + s * 8 + 4);
                int format = U16(sub);
                if (format == 4)
                {
                    int segX2 = U16(sub + 6);
                    int ends = sub + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
                    for (int k = 0; k < segX2 / 2; k++)
                    {
                        int end = U16(ends + 2 * k), start = U16(starts + 2 * k);
                        int delta = (short)U16(deltas + 2 * k), rangeOffset = U16(ranges + 2 * k);
                        for (int cp = start; cp <= end && cp != 0xFFFF; cp++)
                        {
                            int glyph;
                            if (rangeOffset == 0) glyph = (cp + delta) & 0xFFFF;
                            else
                            {
                                int at = ranges + 2 * k + rangeOffset + 2 * (cp - start);
                                glyph = U16(at);
                                if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                            }
                            if (glyph != 0) set.Add(cp);
                        }
                    }
                }
                else if (format == 12)
                {
                    long groups = U32(sub + 12);
                    for (long g = 0; g < groups; g++)
                    {
                        int rec = sub + 16 + (int)g * 12;
                        long start = U32(rec), end = U32(rec + 4), glyph = U32(rec + 8);
                        for (long cp = start; cp <= end; cp++, glyph++) if (glyph != 0) set.Add((int)cp);
                    }
                }
            }
            if (set.Count == 0) throw new InvalidDataException("cmap has no format 4 or 12 subtable");
            return set;
        }

        [MenuItem("Seven Steps/Scene Flow/Check UI Glyphs", priority = 52)]
        public static void CheckMenu()
        {
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            var issues = new List<string>();
            int missing = Check(assets != null ? assets.uiTheme : null, assets, issues);
            if (missing > 0) Debug.LogError("[SevenSteps] UI glyphs:\n" + string.Join("\n", issues));
            else Debug.Log("[SevenSteps] UI glyphs: OK");
        }
    }
}
