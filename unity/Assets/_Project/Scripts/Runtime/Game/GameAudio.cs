using System;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>
    /// Sound for the Game scene: one AudioSource on the host, effects chosen from the events Core produced, and the volume
    /// settings (<see cref="SaveSystem.SettingsChanged"/>). <see cref="ApplyMaster"/> is the one place that sets the master
    /// volume, also for scenes without a board (Title, settings preview).
    /// </summary>
    public sealed class GameAudio : IDisposable
    {
        readonly AudioSource sfx;
        readonly GameAssets assets;
        readonly SaveSystem save;

        public GameAudio(GameObject host, GameAssets assets, SaveSystem save)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            this.assets = assets != null ? assets : throw new ArgumentNullException(nameof(assets));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            sfx = host.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            Apply(save.Settings);
            save.SettingsChanged += Apply;
        }

        public void Dispose() => save.SettingsChanged -= Apply;

        /// Master volume (AudioListener) from the settings.
        public static void ApplyMaster(SettingsData s) => AudioListener.volume = Mathf.Clamp01(s.masterVolume);

        /// A refused move (wall, statue, mud with one step left).
        public void Bump() => Play(assets.sfxBump);

        /// Effects for one command's events, in event order.
        public void Play(StepResult r)
        {
            foreach (var e in r.Events)
            {
                if (e.StepIdx == Rules.Steps) Play(assets.sfxLucky);
                switch (e.Type)
                {
                    case GameEventType.Step: Play(assets.sfxStep, 0.9f + 0.06f * e.StepIdx); break;
                    case GameEventType.Bump: Play(assets.sfxBump); break;
                    case GameEventType.Crumble: Play(assets.sfxCrumble); break;
                    case GameEventType.Stone:
                        Play(assets.sfxStone);
                        if (e.At >= 0 && e.At < r.State.Level.Count && IsPlate(Rules.TileAt(r.State, e.At))) GameSound.Sfx(SoundIds.PlatePress);
                        break;
                    case GameEventType.DoorOpen: GameSound.Sfx(SoundIds.DoorOpen); break;
                    case GameEventType.Respawn: GameSound.Sfx(SoundIds.Wake); break;
                    case GameEventType.Switch:
                    case GameEventType.Flip: Play(assets.sfxSwitch); break; // lever: switch sound until art makes one
                    case GameEventType.Bridge:
                    case GameEventType.Seal: Play(assets.sfxBridge); break;
                    case GameEventType.Fall: Play(assets.sfxFall); break;
                    case GameEventType.Win: Play(assets.sfxWin); break;
                    case GameEventType.Lose: Play(assets.sfxLose); break;
                    case GameEventType.Undo:
                    case GameEventType.Restart: Play(assets.sfxUndo); break;
                }
            }
        }

        static bool IsPlate(char t) => t >= 'a' && t <= 'c';

        void Play(AudioClip clip, float pitch = 1f)
        {
            if (clip == null) return; // shared audio folder not linked: silent, not an error
            sfx.pitch = pitch;
            sfx.PlayOneShot(clip);
        }

        void Apply(SettingsData s)
        {
            ApplyMaster(s);
            sfx.volume = Mathf.Clamp01(s.sfxVolume);
        }
    }
}
