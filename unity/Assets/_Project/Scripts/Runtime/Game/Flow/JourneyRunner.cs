using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SevenSteps.Game.Flow
{
    /// <summary>
    /// Plays one journey transition at a time (docs/stage-select-transitions.md 4). Each frame it ticks the clock with
    /// unscaled time and hands the eased progress to the transition. Any press (key, mouse button, touch) skips to the
    /// settled end. Esc cancels instead when the transition can be cancelled (the dive). The press is consumed: input
    /// handlers ask <see cref="Blocks"/> and ignore it, so a skip never also makes a puzzle move. Presses on the frame the
    /// transition starts belong to the choice that started it and are ignored. A scene object (one per scene root).
    /// </summary>
    public sealed class JourneyRunner : MonoBehaviour
    {
        TransitionClock clock;
        Action<float> apply;
        Action done, cancel;
        int startFrame = -1, consumedFrame = -1;

        public static JourneyRunner Create(Transform parent)
        {
            var go = new GameObject("Journey Runner");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<JourneyRunner>();
        }

        /// A transition is playing.
        public bool Busy => clock != null;
        /// Input must be ignored now: a transition is playing, or this frame's press skipped or cancelled one.
        public bool Blocks => Busy || consumedFrame == Time.frameCount;
        /// SettingsData.reduceMotion: every transition settles at once (on the next frame).
        public bool ReduceMotion { get; set; }

        /// <param name="seconds">Length at normal motion.</param>
        /// <param name="apply">Called with eased progress 0..1: 0 now, then each frame, and 1 before <paramref name="done"/>.</param>
        /// <param name="done">The settled end (also after a skip).</param>
        /// <param name="cancel">Esc: called instead of the end (the transition restores its own pose). Null = Esc skips.</param>
        public void Play(float seconds, Action<float> apply, Action done, Action cancel = null)
        {
            if (Busy) throw new InvalidOperationException("JourneyRunner: a transition is already playing");
            if (apply == null) throw new ArgumentNullException(nameof(apply));
            if (done == null) throw new ArgumentNullException(nameof(done));
            this.apply = apply;
            this.done = done;
            this.cancel = cancel;
            clock = new TransitionClock(ReduceMotion ? 0f : seconds);
            startFrame = Time.frameCount;
            apply(0f);
        }

        /// Settle the playing transition now (tests, tools). Does nothing when no transition is playing.
        public void SkipNow()
        {
            if (!Busy) return;
            clock.Skip();
            Finish();
        }

        void Update()
        {
            if (!Busy) return;
            if (Time.frameCount != startFrame)
            {
                if (cancel != null && EscPressed()) { consumedFrame = Time.frameCount; Cancel(); return; }
                if (AnyPressed()) { consumedFrame = Time.frameCount; clock.Skip(); }
            }
            if (!clock.Done) clock.Tick(Time.unscaledDeltaTime);
            if (clock.Done) Finish();
            else apply(clock.Eased);
        }

        void Finish()
        {
            var a = apply;
            var d = done;
            Clear();
            a(1f);
            d();
        }

        void Cancel()
        {
            var c = cancel;
            Clear();
            c();
        }

        void Clear()
        {
            clock = null;
            apply = null;
            done = cancel = null;
        }

        static bool EscPressed() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        static bool AnyPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            var m = Mouse.current;
            if (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame || m.middleButton.wasPressedThisFrame)) return true;
            var t = Touchscreen.current;
            return t != null && t.primaryTouch.press.wasPressedThisFrame;
        }
    }
}
