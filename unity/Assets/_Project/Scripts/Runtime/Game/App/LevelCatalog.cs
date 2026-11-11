using System;
using System.Linq;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>Stages from levels.json (docs/architecture.md 6). Parsed once per app run and shared by the Title and Game scenes.</summary>
    public static class LevelCatalog
    {
        static Level[] cached;
        static TextAsset source;
        static string[] chapterIds;
        static LevelDto[] dtos; // stage texts per language (LevelDto.TextFor, 13.5)
        static ChapterFileDto chapters; // null = no chapters.json yet

        /// Domain reload is disabled in the editor: parse levels.json again on every Play so edits to it show up.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetForPlay()
        {
            cached = null;
            source = null;
            chapterIds = null;
            dtos = null;
            chapters = null;
        }

        public static Level[] Load(GameAssets assets)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            if (assets.levelsJson == null) throw new MissingReferenceException("[SevenSteps] GameAssets.levelsJson is not set");
            if (cached != null && source == assets.levelsJson) return cached;
            var file = JsonUtility.FromJson<LevelFileDto>(assets.levelsJson.text);
            var parsed = file.levels.Select(d => d.ToLevel()).ToArray();
            // The stage name is the progress save key (docs/architecture.md 11.3.1): it must be unique.
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (var l in parsed)
                if (!names.Add(l.Name)) throw new InvalidOperationException($"[SevenSteps] levels.json: stage name \"{l.Name}\" is used twice (it is the save key)");
            cached = parsed;
            dtos = file.levels;
            chapterIds = file.levels.Select(d => d.chapter ?? "").ToArray(); // LevelDto.chapter (6.4), not part of Core Level
            chapters = assets.chaptersJson != null ? JsonUtility.FromJson<ChapterFileDto>(assets.chaptersJson.text) : null;
            if (chapters != null && (chapters.chapters == null || chapters.chapters.Length == 0)) chapters = null;
            if (chapters != null)
                for (int k = 0; k < chapterIds.Length; k++) chapters.Get(chapterIds[k]); // unknown id throws: levels.json and chapters.json disagree
            source = assets.levelsJson;
            return cached;
        }

        /// The stored solution of a stage (levels.json expected.path, the JS solver's fewest-bodies route checked by
        /// verify), replay letters; "" when there is none. The staged hint follows it (HintPlan). Call after Load.
        public static string PathOf(int stage)
        {
            if (dtos == null) throw new InvalidOperationException("LevelCatalog.PathOf before Load");
            if (stage < 0 || stage >= dtos.Length) throw new ArgumentOutOfRangeException(nameof(stage), stage, $"stage must be 0..{dtos.Length - 1}");
            return dtos[stage].expected?.path ?? "";
        }

        /// Which line a stage starts with (levels.json startLine, docs/architecture.md 16.7): "story", "hint" or "". Call after Load.
        public static string StartLineOf(int stage)
        {
            if (dtos == null) throw new InvalidOperationException("LevelCatalog.StartLineOf before Load");
            if (stage < 0 || stage >= dtos.Length) throw new ArgumentOutOfRangeException(nameof(stage), stage, $"stage must be 0..{dtos.Length - 1}");
            return dtos[stage].StartLine;
        }

        /// Name, hint and story of a stage in a language (levels.json text, docs/architecture.md 13.5): ko = the base fields,
        /// a missing translation throws KeyNotFoundException (the export check catches it before a build). Call after Load.
        public static LevelTextDto TextOf(int stage, Language lang)
        {
            if (dtos == null) throw new InvalidOperationException("LevelCatalog.TextOf before Load");
            if (stage < 0 || stage >= dtos.Length) throw new ArgumentOutOfRangeException(nameof(stage), stage, $"stage must be 0..{dtos.Length - 1}");
            return dtos[stage].TextFor(Languages.Code(lang));
        }

        /// The stage at a 0-based index. Call after Load; a stage outside levels.json throws.
        public static Level At(int stage)
        {
            if (cached == null) throw new InvalidOperationException("LevelCatalog.At before Load");
            if (stage < 0 || stage >= cached.Length) throw new ArgumentOutOfRangeException(nameof(stage), stage, $"stage must be 0..{cached.Length - 1}");
            return cached[stage];
        }

        /// Chapter id of a stage (levels.json "chapter", e.g. "moss"), "" when the stage has none (= first chapter, 6.4).
        /// Call after Load; a stage outside levels.json throws.
        public static string ChapterIdOf(int stage)
        {
            if (chapterIds == null) throw new InvalidOperationException("LevelCatalog.ChapterIdOf before Load");
            if (stage < 0 || stage >= chapterIds.Length) throw new ArgumentOutOfRangeException(nameof(stage), stage, $"stage must be 0..{chapterIds.Length - 1}");
            return chapterIds[stage];
        }

        /// The chapter of a stage from chapters.json ("" in levels.json = the first chapter); null when there is no chapters.json.
        public static ChapterInfo ChapterOf(int stage)
        {
            string id = ChapterIdOf(stage);
            return chapters?.Get(id);
        }

        /// Chapter key for comparisons: the chapters.json id ("" and the first chapter's id are the same chapter), or the raw
        /// levels.json value when there is no chapters.json. Used by GameFlowController.ChapterEntering.
        public static string ChapterKeyOf(int stage) => ChapterOf(stage)?.id ?? ChapterIdOf(stage);

        /// chapters.json chapters in order (planet select). Empty when there is no chapters.json. Call after Load.
        public static System.Collections.Generic.IReadOnlyList<ChapterInfo> Chapters
        {
            get
            {
                if (cached == null) throw new InvalidOperationException("LevelCatalog.Chapters before Load");
                return chapters != null ? chapters.chapters : new ChapterInfo[0];
            }
        }

        /// 0-based stages of a chapter key (ChapterKeyOf), in levels.json order. Call after Load.
        public static int[] StagesOf(string chapterKey)
        {
            if (chapterKey == null) throw new ArgumentNullException(nameof(chapterKey));
            if (cached == null) throw new InvalidOperationException("LevelCatalog.StagesOf before Load");
            return Enumerable.Range(0, cached.Length).Where(k => ChapterKeyOf(k) == chapterKey).ToArray();
        }

        /// What the chapter card shows for a chapter key, in a language (ChapterInfo.Planet/Intro(lang), 13.5); null when there
        /// is no chapters.json or no planet name. A missing translation throws (export and LocalizationCheck catch it).
        public static Flow.ChapterCardModel ChapterCard(string chapterKey, Language lang)
        {
            if (chapters == null) return null;
            var info = chapters.Get(chapterKey);
            if (info == null || string.IsNullOrEmpty(info.planet)) return null;
            int number = Array.IndexOf(chapters.chapters, info) + 1;
            var planets = chapters.chapters.Select(c => c.Planet(lang)).ToArray();
            return new Flow.ChapterCardModel(info.id, number, info.Planet(lang), info.Intro(lang), planets);
        }
    }
}
