using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>
    /// Music and sound ids (user request 2026-11-13: BGM + SFX). Each id is a file in the shared audio folder,
    /// Assets/Shared/Audio/&lt;id&gt;.ogg (or .wav), wired into Data/Sounds.asset by setup / sound-wire (SoundWire).
    /// A missing file is a warning, never an error: its hook is silent.
    /// </summary>
    public static class SoundIds
    {
        /// Title, story map, stage select, prologue cutscene.
        public const string BgmTitle = "bgm.title";
        /// In a stage, tutorial boards included.
        public const string BgmStage = "bgm.stage";
        /// The ending cards and the epilogue (optional: falls back to <see cref="BgmTitle"/>).
        public const string BgmEnding = "bgm.ending";

        /// A U5 icon / glyph button press.
        public const string UiClick = "ui.click";
        /// A cutscene page turns.
        public const string PageTurn = "page.turn";
        /// Core DoorOpen event.
        public const string DoorOpen = "door.open";
        /// A statue (KEEP) lands on a plate.
        public const string PlatePress = "plate.press";
        /// The result screen's 1st / 2nd / 3rd star appears (<see cref="StarChime"/>).
        public const string StarChime1 = "star.chime.1", StarChime2 = "star.chime.2", StarChime3 = "star.chime.3";
        /// The result screen opens on a clear.
        public const string ClearJingle = "clear.jingle";
        /// The next brother wakes (Core Respawn event).
        public const string Wake = "wake";

        public static readonly IReadOnlyList<string> Music = new[] { BgmTitle, BgmStage, BgmEnding };
        public static readonly IReadOnlyList<string> Sfx = new[] { UiClick, PageTurn, DoorOpen, PlatePress, StarChime1, StarChime2, StarChime3, ClearJingle, Wake };

        /// Every id, music first.
        public static IEnumerable<string> All
        {
            get
            {
                foreach (var m in Music) yield return m;
                foreach (var s in Sfx) yield return s;
            }
        }

        /// The chime for star <paramref name="n"/> (1..3; clamped).
        public static string StarChime(int n) => n <= 1 ? StarChime1 : n == 2 ? StarChime2 : StarChime3;

        /// The music an app state plays: Title / StageSelect -> title, Game -> stage, others keep what plays (null).
        public static string MusicFor(Contracts.AppState state) =>
            state == Contracts.AppState.Title || state == Contracts.AppState.StageSelect ? BgmTitle
            : state == Contracts.AppState.Game ? BgmStage : null;
    }

    /// <summary>id -> clip (Data/Sounds.asset). Filled by the editor from Assets/Shared/Audio; clips not delivered yet stay null.</summary>
    [CreateAssetMenu(menuName = "Seven Steps/Sound Catalog", fileName = "Sounds")]
    public sealed class SoundCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id = "";
            public AudioClip clip;
        }

        public Entry[] entries = new Entry[0];

        /// The clip for <paramref name="id"/>, or null (unknown id or no file yet).
        public AudioClip Get(string id)
        {
            if (string.IsNullOrEmpty(id) || entries == null) return null;
            foreach (var e in entries) if (e != null && e.id == id) return e.clip;
            return null;
        }

        /// The music clip for <paramref name="id"/>: bgm.ending falls back to bgm.title.
        public AudioClip Music(string id)
        {
            var c = Get(id);
            return c == null && id == SoundIds.BgmEnding ? Get(SoundIds.BgmTitle) : c;
        }
    }
}
