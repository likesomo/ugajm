using System;
using SevenSteps.Contracts;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SevenSteps.Game
{
    /// <summary>
    /// The persistent music player and the shared sound effects (user request 2026-11-13: BGM + SFX). One
    /// DontDestroyOnLoad object made by the bootstrap (<see cref="Create"/>); scenes and views call the static
    /// <see cref="Music"/> / <see cref="Sfx"/>, which do nothing while there is none (Edit Mode tests) or the clip is missing.
    /// Music: two looping sources, a <see cref="FadeSeconds"/> crossfade on a change, the same id again keeps playing.
    /// Volumes: music = settings.musicVolume, effects = settings.sfxVolume; the master volume is AudioListener.volume
    /// (<see cref="GameAudio.ApplyMaster"/>), so both are scaled by it. WebGL: the browser keeps audio suspended until a
    /// click or key, so the music starts again on the first one.
    /// </summary>
    public sealed class GameSound : MonoBehaviour
    {
        public const string ObjectName = "Game Sound";
        public const float FadeSeconds = 1f;

        static GameSound instance;
        /// The player, or null (no bootstrap yet, Edit Mode).
        public static GameSound Instance => instance != null ? instance : null;

        SoundCatalog catalog;
        SaveSystem save;
        AudioSource[] music;
        AudioSource sfx;
        int current;
        float fade = 1f; // 0..1 into the crossfade
        float oldFrom;
        string musicId = "";
        bool unlocked;

        /// The music id asked for last ("" = none).
        public string MusicId => musicId;
        /// The clip playing (or fading in), null = silence.
        public AudioClip MusicClip => music != null ? music[current].clip : null;
        public float MusicVolume => music != null ? music[current].volume : 0f;

        /// Makes the player (once per Play; again: it takes the new catalog and settings).
        public static GameSound Create(SoundCatalog catalog, SaveSystem save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (instance == null)
            {
                var go = new GameObject(ObjectName);
                if (Application.isPlaying) DontDestroyOnLoad(go);
                instance = go.AddComponent<GameSound>();
                instance.Build();
            }
            instance.Bind(catalog, save);
            return instance;
        }

        void Build()
        {
            music = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                music[i] = gameObject.AddComponent<AudioSource>();
                music[i].playOnAwake = false;
                music[i].loop = true;
                music[i].volume = 0f;
            }
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
        }

        void Bind(SoundCatalog c, SaveSystem s)
        {
            if (save != null) save.SettingsChanged -= Apply;
            catalog = c;
            save = s;
            save.SettingsChanged += Apply;
            if (catalog == null) Debug.LogWarning("[SevenSteps] GameAssets.sounds is not set: no music or new effects (unity.sh setup)");
            Apply(save.Settings);
        }

        void OnDestroy()
        {
            if (save != null) save.SettingsChanged -= Apply;
            if (instance == this) instance = null;
        }

        void Apply(SettingsData s)
        {
            GameAudio.ApplyMaster(s);
            sfx.volume = Mathf.Clamp01(s.sfxVolume);
            if (fade >= 1f) music[current].volume = Target;
        }

        float Target => save != null ? Mathf.Clamp01(save.Settings.musicVolume) : 0f;

        /// Plays music <paramref name="id"/> (SoundIds.Bgm*); null / "" keeps what plays.
        public static void Music(string id)
        {
            if (instance != null && !string.IsNullOrEmpty(id)) instance.PlayMusic(id);
        }

        /// One effect (SoundIds); nothing while its clip is missing.
        public static void Sfx(string id, float pitch = 1f)
        {
            if (instance != null) instance.PlaySfx(id, pitch);
        }

        public void PlayMusic(string id)
        {
            if (id == musicId) return; // asked again (a scene reload, the same state): no restart
            musicId = id;
            var clip = catalog != null ? catalog.Music(id) : null;
            var now = music[current];
            if (clip != null && now.clip == clip && now.isPlaying) return; // bgm.ending on the title clip: keep it going
            oldFrom = now.volume;
            current = 1 - current;
            var next = music[current];
            next.Stop();
            next.clip = clip;
            next.volume = 0f;
            if (clip != null) next.Play();
            fade = 0f;
        }

        public void PlaySfx(string id, float pitch = 1f)
        {
            var clip = catalog != null ? catalog.Get(id) : null;
            if (clip == null) return;
            sfx.pitch = pitch;
            sfx.PlayOneShot(clip);
        }

        void Update()
        {
            if (fade < 1f)
            {
                fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / FadeSeconds);
                music[current].volume = Target * fade;
                var old = music[1 - current];
                old.volume = oldFrom * (1f - fade);
                if (fade >= 1f) { old.Stop(); old.clip = null; }
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!unlocked && AnyPress())
            {
                unlocked = true; // the browser resumed audio on this gesture: start the music from its top
                var src = music[current];
                if (src.clip != null) { src.Stop(); src.Play(); }
            }
#endif
        }

        /// A click, tap or key this frame (the WebGL audio unlock gesture).
        static bool AnyPress()
        {
#if ENABLE_INPUT_SYSTEM
            return (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
#else
            return Input.anyKeyDown;
#endif
        }

        /// The WebGL unlock happened (always false outside WebGL players).
        public bool Unlocked => unlocked;
    }
}
