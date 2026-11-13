using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SevenSteps.Tests.Game
{
    /// <summary>Music ids, the catalog's fallback and the player's same-id rule (user request 2026-11-13: BGM + SFX). Owner: gameplay-ui.</summary>
    public sealed class GameSoundTests
    {
        SoundCatalog catalog;
        AudioClip title, stage;

        [SetUp]
        public void Build()
        {
            title = AudioClip.Create("title", 4410, 1, 44100, false);
            stage = AudioClip.Create("stage", 4410, 1, 44100, false);
            catalog = ScriptableObject.CreateInstance<SoundCatalog>();
            catalog.entries = new[]
            {
                new SoundCatalog.Entry { id = SoundIds.BgmTitle, clip = title },
                new SoundCatalog.Entry { id = SoundIds.BgmStage, clip = stage },
                new SoundCatalog.Entry { id = SoundIds.UiClick },
            };
        }

        [TearDown]
        public void Clean()
        {
            if (GameSound.Instance != null) Object.DestroyImmediate(GameSound.Instance.gameObject);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(title);
            Object.DestroyImmediate(stage);
        }

        [Test]
        public void TheAppStatePicksTheMusic()
        {
            Assert.AreEqual(SoundIds.BgmTitle, SoundIds.MusicFor(AppState.Title));
            Assert.AreEqual(SoundIds.BgmTitle, SoundIds.MusicFor(AppState.StageSelect));
            Assert.AreEqual(SoundIds.BgmStage, SoundIds.MusicFor(AppState.Game));
            Assert.IsNull(SoundIds.MusicFor(AppState.Boot), "keeps what plays");
            Assert.AreEqual(new[] { "star.chime.1", "star.chime.2", "star.chime.3" },
                new[] { SoundIds.StarChime(1), SoundIds.StarChime(2), SoundIds.StarChime(3) });
        }

        [Test]
        public void TheEndingFallsBackToTheTitleAndMissingClipsAreNull()
        {
            Assert.AreSame(title, catalog.Music(SoundIds.BgmEnding), "no bgm.ending file: the title music");
            Assert.AreSame(stage, catalog.Music(SoundIds.BgmStage));
            Assert.IsNull(catalog.Get(SoundIds.UiClick), "an id without a file");
            Assert.IsNull(catalog.Get("no.such.id"));
        }

        [Test]
        public void TheSameIdDoesNotRestartAndNothingPlaysWithoutAPlayer()
        {
            GameSound.Music(SoundIds.BgmTitle); // no player yet (Edit Mode, no bootstrap): nothing, no throw
            GameSound.Sfx(SoundIds.UiClick);
            var save = new SaveSystem(new MemoryStore());
            var p = GameSound.Create(catalog, save);
            GameSound.Music(SoundIds.BgmTitle);
            Assert.AreSame(title, p.MusicClip);
            var playing = p.MusicClip;
            GameSound.Music(SoundIds.BgmTitle);
            Assert.AreSame(playing, p.MusicClip, "asked again: the same source keeps playing");
            GameSound.Music(SoundIds.BgmEnding);
            Assert.AreEqual(SoundIds.BgmEnding, p.MusicId);
            GameSound.Music(SoundIds.BgmStage);
            Assert.AreSame(stage, p.MusicClip, "a new id fades the new clip in");
            GameSound.Sfx(SoundIds.UiClick); // no clip: silent
            Assert.AreSame(p, GameSound.Create(catalog, save), "one player per Play");
        }
    }
}
