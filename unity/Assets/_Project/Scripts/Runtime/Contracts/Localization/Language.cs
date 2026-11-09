using System;
using UnityEngine;

namespace SevenSteps.Contracts
{
    /// <summary>UI languages (docs/architecture.md 13). Append-only. Ko is the base language: every string key exists in ko.</summary>
    public enum Language { Ko, En }

    /// <summary>Language codes as stored and passed around ("ko", "en": SettingsData.language, -lang / ?lang, data "lang").</summary>
    public static class Languages
    {
        public const Language Base = Language.Ko;
        public static readonly Language[] All = { Language.Ko, Language.En };

        public static string Code(Language lang) => lang switch
        {
            Language.Ko => "ko",
            Language.En => "en",
            _ => throw new ArgumentOutOfRangeException(nameof(lang), lang, "unknown language"),
        };

        /// "ko" / "en" (case-insensitive). False for anything else (external input: the caller decides the fallback).
        public static bool TryParse(string code, out Language lang)
        {
            switch ((code ?? "").Trim().ToLowerInvariant())
            {
                case "ko": lang = Language.Ko; return true;
                case "en": lang = Language.En; return true;
                default: lang = Base; return false;
            }
        }

        /// First run (13.3): Korean system → ko, anything else → en.
        public static Language FromSystem(SystemLanguage system) => system == SystemLanguage.Korean ? Language.Ko : Language.En;
    }
}
