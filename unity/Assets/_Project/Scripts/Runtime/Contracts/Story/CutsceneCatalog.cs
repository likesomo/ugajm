using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenSteps.Contracts
{
    // JSON shapes of unity/Assets/_Project/Data/cutscenes.json (copied from design/story/cutscenes.json by unity.sh levels
    // after check-cutscenes.mjs; design/story/cutscenes.md). Lines use the story.json line shape (ko base + text[]).

    [Serializable]
    public sealed class CutsceneFileDto
    {
        public string about = "";
        public int version;
        /// ko first, then every translation (every line has each one).
        public string[] languages = new string[0];
        /// The Codex style prompt. Not read at run time.
        public string artStyle = "";
        public CutsceneSceneDto[] scenes = new CutsceneSceneDto[0];
    }

    [Serializable]
    public sealed class CutsceneSceneDto
    {
        /// Unique, [a-z0-9-]+ ("prologue", "epilogue").
        public string id = "";
        /// When it plays: <see cref="CutsceneCatalog.WhenNewGame"/> or <see cref="CutsceneCatalog.WhenAfterEnding"/>.
        public string when = "";
        /// "title": the title's story entry replays it; "" = no replay.
        public string replay = "";
        /// May be left out of a build (its pictures are late); the scene still plays when it is in the data.
        public bool optional;
        public CutscenePageDto[] pages = new CutscenePageDto[0];
    }

    [Serializable]
    public sealed class CutscenePageDto
    {
        public string id = "";
        /// cutscene.&lt;scene&gt;.&lt;page&gt; (a sprite in GameAssets.cutsceneImages; none = the plain card).
        public string image = "";
        /// 1..<see cref="CutsceneCatalog.MaxLines"/>, shown one by one.
        public StoryLineDto[] lines = new StoryLineDto[0];
        /// The plain-language rule caption under the lines (cutscenes v2, user request 2026-11-13), ko base + text[];
        /// none = no caption (no ko line).
        public StoryLineDto rule;
        /// Another page's picture key this page shows (no new art, e.g. the planet arrivals 2026-11-14); "" = its own.
        /// Editor wiring only (CutsceneArtImport): the entry for <see cref="image"/> gets that sprite.
        public string reuse = "";
        /// The Codex prompt for the picture. Not read at run time.
        public string art = "";
    }

    /// <summary>
    /// The storybook cutscenes (design/story/cutscenes.md; user request 2026-11-12 evening), checked on load: a known
    /// version, ko first in "languages", unique scene ids with a known "when", at least one page per scene, each page's
    /// image key cutscene.&lt;scene&gt;.&lt;page&gt;, 1..<see cref="MaxLines"/> lines, and every line in every language.
    /// A failure is a <see cref="FormatException"/> naming the place (CutsceneLoad turns cutscenes off; scene-validate
    /// stops the build). Pure C#.
    /// </summary>
    public sealed class CutsceneCatalog
    {
        /// 2: pages may have a rule caption (2026-11-13). 3: the same fields, rule captions in the story voice (8aaf58f).
        public const int Version = 3;
        public const int MaxLines = 3;
        public const string WhenNewGame = "newGame", WhenAfterEnding = "afterEnding";
        /// A planet interlude (user request 2026-11-14): scene id "interlude.<chapter>", played once after the first clear of
        /// that chapter's finale stage (StoryTruths.FinaleStage), before the map.
        public const string WhenPlanetComplete = "planetComplete", InterludePrefix = "interlude.";
        /// A planet arrival (user request 2026-11-14): "arrival.<chapter>", played once before that chapter's first stage.
        public const string WhenPlanetArrival = "planetArrival", ArrivalPrefix = "arrival.";
        public const string ReplayTitle = "title";
        public const string Prologue = "prologue", Epilogue = "epilogue";

        readonly CutsceneFileDto file;
        readonly List<string> imageKeys = new List<string>();

        public IReadOnlyList<CutsceneSceneDto> Scenes => file.scenes;
        public IReadOnlyList<string> ImageKeys => imageKeys;

        CutsceneCatalog(CutsceneFileDto file) => this.file = file;

        public static string ImageKeyOf(string scene, string page) => $"cutscene.{scene}.{page}";

        public static CutsceneCatalog FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("cutscenes.json is empty");
            CutsceneFileDto dto;
            try { dto = JsonUtility.FromJson<CutsceneFileDto>(json); }
            catch (ArgumentException e) { throw new FormatException("cutscenes.json is not JSON: " + e.Message); }
            if (dto == null) throw new FormatException("cutscenes.json is not JSON");
            if (dto.version != Version) throw new FormatException($"cutscenes.json version {dto.version}, expected {Version}");
            var langs = dto.languages ?? new string[0];
            if (langs.Length == 0 || langs[0] != Languages.Code(Languages.Base)) throw new FormatException("cutscenes.json languages must start with \"ko\"");
            foreach (var lang in Languages.All)
                if (Array.IndexOf(langs, Languages.Code(lang)) < 0) throw new FormatException($"cutscenes.json languages has no \"{Languages.Code(lang)}\"");
            if (dto.scenes == null || dto.scenes.Length == 0) throw new FormatException("cutscenes.json has no scenes");
            var catalog = new CutsceneCatalog(dto);
            var ids = new HashSet<string>();
            foreach (var s in dto.scenes)
            {
                if (s == null || string.IsNullOrEmpty(s.id)) throw new FormatException("cutscenes.json: a scene without id");
                if (!ids.Add(s.id)) throw new FormatException($"cutscenes.json: scene \"{s.id}\" twice");
                if (s.when != WhenNewGame && s.when != WhenAfterEnding && s.when != WhenPlanetComplete && s.when != WhenPlanetArrival) throw new FormatException($"cutscenes.json {s.id}: when \"{s.when}\" is not {WhenNewGame}, {WhenAfterEnding}, {WhenPlanetComplete} or {WhenPlanetArrival}");
                if (s.when == WhenPlanetArrival != s.id.StartsWith(ArrivalPrefix, StringComparison.Ordinal)) throw new FormatException($"cutscenes.json {s.id}: a {WhenPlanetArrival} scene is \"{ArrivalPrefix}<chapter>\" and only it");
                if (s.when == WhenPlanetComplete != s.id.StartsWith(InterludePrefix, StringComparison.Ordinal)) throw new FormatException($"cutscenes.json {s.id}: a {WhenPlanetComplete} scene is \"{InterludePrefix}<chapter>\" and only it");
                if (s.replay != "" && s.replay != ReplayTitle) throw new FormatException($"cutscenes.json {s.id}: replay \"{s.replay}\" is not \"{ReplayTitle}\" or empty");
                if (s.pages == null || s.pages.Length == 0) throw new FormatException($"cutscenes.json {s.id}: no pages");
                foreach (var p in s.pages)
                {
                    string at = $"cutscenes.json {s.id}.{p?.id}";
                    if (p == null || string.IsNullOrEmpty(p.id)) throw new FormatException($"cutscenes.json {s.id}: a page without id");
                    if (p.image != ImageKeyOf(s.id, p.id)) throw new FormatException($"{at}: image \"{p.image}\", expected \"{ImageKeyOf(s.id, p.id)}\"");
                    if (catalog.imageKeys.Contains(p.image)) throw new FormatException($"{at}: page twice");
                    catalog.imageKeys.Add(p.image);
                    int n = p.lines?.Length ?? 0;
                    if (n < 1 || n > MaxLines) throw new FormatException($"{at}: {n} lines (1..{MaxLines})");
                    for (int i = 0; i < n; i++)
                        foreach (var lang in Languages.All)
                            if (string.IsNullOrEmpty(LineOrNull(p, i, lang))) throw new FormatException($"{at} line {i + 1}: no \"{Languages.Code(lang)}\" text");
                    if (HasRule(p))
                        foreach (var lang in Languages.All)
                            if (string.IsNullOrEmpty(TextOf(p.rule, lang))) throw new FormatException($"{at} rule: no \"{Languages.Code(lang)}\" text");
                }
            }
            return catalog;
        }

        /// The scene with this id, or null.
        public CutsceneSceneDto Scene(string id)
        {
            foreach (var s in file.scenes) if (s.id == id) return s;
            return null;
        }

        /// The planet interlude of a chapter ("interlude.<chapter>"), or null when it has none.
        public CutsceneSceneDto Interlude(string chapter) =>
            string.IsNullOrEmpty(chapter) ? null : Scene(InterludePrefix + chapter) is CutsceneSceneDto s && s.when == WhenPlanetComplete ? s : null;

        /// Story memory id that the interlude was shown (StoryProgress counts; cleared by a progress reset).
        public static string InterludeSeenId(string chapter) => "cutscene." + InterludePrefix + chapter;

        /// The planet arrival of a chapter ("arrival.<chapter>"), or null when it has none.
        public CutsceneSceneDto Arrival(string chapter) =>
            string.IsNullOrEmpty(chapter) ? null : Scene(ArrivalPrefix + chapter) is CutsceneSceneDto s && s.when == WhenPlanetArrival ? s : null;

        /// Story memory id that the arrival was shown (StoryProgress counts; cleared by a progress reset).
        public static string ArrivalSeenId(string chapter) => "cutscene." + ArrivalPrefix + chapter;

        /// The first scene that plays <paramref name="when"/> (WhenNewGame, WhenAfterEnding), or null.
        public CutsceneSceneDto SceneFor(string when)
        {
            foreach (var s in file.scenes) if (s.when == when) return s;
            return null;
        }

        /// The first scene the title's story entry replays, or null.
        public CutsceneSceneDto TitleReplay()
        {
            foreach (var s in file.scenes) if (s.replay == ReplayTitle) return s;
            return null;
        }

        /// Line <paramref name="index"/> of the page in a language (checked on load: never empty).
        public static string Line(CutscenePageDto page, int index, Language lang)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (index < 0 || index >= page.lines.Length) throw new ArgumentOutOfRangeException(nameof(index), index, $"page {page.id} has {page.lines.Length} lines");
            return LineOrNull(page, index, lang) ?? throw new KeyNotFoundException($"cutscenes.json page {page.id} line {index + 1}: no \"{Languages.Code(lang)}\" text");
        }

        /// Every line of the page in a language.
        public static string[] Lines(CutscenePageDto page, Language lang)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            var lines = new string[page.lines.Length];
            for (int i = 0; i < lines.Length; i++) lines[i] = Line(page, i, lang);
            return lines;
        }

        /// The page has a rule caption (its ko line is set).
        public static bool HasRule(CutscenePageDto page) =>
            page != null && page.rule != null && !string.IsNullOrEmpty(page.rule.line);

        /// The page's rule caption in a language, "" when the page has none (checked on load: never empty otherwise).
        public static string Rule(CutscenePageDto page, Language lang)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (!HasRule(page)) return "";
            return TextOf(page.rule, lang) ?? throw new KeyNotFoundException($"cutscenes.json page {page.id} rule: no \"{Languages.Code(lang)}\" text");
        }

        static string LineOrNull(CutscenePageDto page, int index, Language lang) => TextOf(page.lines[index], lang);

        static string TextOf(StoryLineDto l, Language lang)
        {
            if (l == null) return null;
            if (lang == Languages.Base) return l.line;
            string code = Languages.Code(lang);
            if (l.text != null) foreach (var t in l.text) if (t != null && t.lang == code) return t.line;
            return null;
        }
    }
}
