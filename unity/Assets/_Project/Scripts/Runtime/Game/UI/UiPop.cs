using UnityEngine;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// A one-time "rise" of a UI mark (star map carving, story-integration.md 0.5 #1): grows from nothing past full size and
    /// settles, in unscaled time, then removes itself. With reduced motion (SettingsData.reduceMotion) or outside play
    /// mode the mark just appears.
    /// </summary>
    public sealed class UiPop : MonoBehaviour
    {
        public const float Seconds = 0.6f;
        float t;

        public static void Play(RectTransform mark, bool reduceMotion)
        {
            if (mark == null) throw new System.ArgumentNullException(nameof(mark));
            if (reduceMotion || !Application.isPlaying) { mark.localScale = Vector3.one; return; }
            if (!mark.TryGetComponent<UiPop>(out var pop)) pop = mark.gameObject.AddComponent<UiPop>();
            pop.t = 0;
            mark.localScale = Vector3.zero;
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Seconds);
            // 0 -> 1.5 at 60 %, then back to 1.
            float s = k < 0.6f ? Mathf.SmoothStep(0, 1.5f, k / 0.6f) : Mathf.SmoothStep(1.5f, 1f, (k - 0.6f) / 0.4f);
            transform.localScale = new Vector3(s, s, 1);
            if (k >= 1) { transform.localScale = Vector3.one; Destroy(this); }
        }
    }
}
