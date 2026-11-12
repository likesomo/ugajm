using System;
using SevenSteps.Contracts;

namespace SevenSteps.Game.UI.U5
{
    /// <summary>
    /// The U5 settings row's controller (docs/ui-u5-kit.md 7): sound mutes / restores the master volume, motion toggles
    /// reduce motion (both saved at once through SaveSystem, which raises SettingsChanged for audio, the board and the
    /// star trail), language and "more" open what the scene root gives (the language picker, the full settings panel).
    /// </summary>
    public sealed class U5SettingsController : IDisposable
    {
        public const float DefaultVolume = 1f;

        readonly SaveSystem save;
        readonly U5SettingsView view;
        readonly Action openLanguage, openMore;
        readonly Action<SettingsData> onChanged;
        float lastVolume = DefaultVolume;

        public U5SettingsController(SaveSystem save, U5SettingsView view, Action openLanguage, Action openMore)
        {
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.openLanguage = openLanguage ?? throw new ArgumentNullException(nameof(openLanguage));
            this.openMore = openMore ?? throw new ArgumentNullException(nameof(openMore));
            view.SoundToggled += ToggleSound;
            view.MotionToggled += ToggleMotion;
            view.LanguageRequested += openLanguage;
            view.MoreRequested += openMore;
            onChanged = _ => Redraw();
            save.SettingsChanged += onChanged;
            Redraw();
        }

        public void Dispose()
        {
            view.SoundToggled -= ToggleSound;
            view.MotionToggled -= ToggleMotion;
            view.LanguageRequested -= openLanguage;
            view.MoreRequested -= openMore;
            save.SettingsChanged -= onChanged;
        }

        public bool SoundOn => save.Settings.masterVolume > 0f;

        public void ToggleSound()
        {
            if (SoundOn)
            {
                lastVolume = save.Settings.masterVolume;
                save.SetMasterVolume(0f);
            }
            else save.SetMasterVolume(lastVolume > 0f ? lastVolume : DefaultVolume);
            GameAudio.ApplyMaster(save.Settings);
            Redraw();
        }

        public void ToggleMotion()
        {
            save.SetReduceMotion(!save.Settings.reduceMotion);
            Redraw();
        }

        void Redraw() => view.SetState(SoundOn, save.Settings.reduceMotion);
    }
}
