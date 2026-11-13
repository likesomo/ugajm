using System;
using System.Collections;
using System.Collections.Generic;
using SevenSteps.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game
{
    /// <summary>
    /// Screen-space marks that follow the 3D board (on the HUD's world label layer, behind the HUD plates), drawn by
    /// BoardView from the State it draws (design/art/ui-minimal.md v3, docs/architecture.md 4.4 / 15.6):
    /// - while choosing (cards shown): the ring around the brother, and for the focused choice the KEEP dotted line to the
    ///   next start or the SWITCH knot on this cell (the 3D ghosts are BoardView's);
    ///   and, for both choices at once, where the next brother wakes (KEEP: a ring on the start; SWITCH: here) with a tag
    ///   under each cell (user request 2026-11-13);
    /// - the short confirm burst of a KEEP / SWITCH Present, then the wake cell's pulse and the new stone's ring.
    /// - the staged hint's goal ring.
    /// Screen space so the marks keep their pixel size and are never hidden by the raised front tiles. The step stars
    /// moved from the feet to the sky above the board (StepSkyView, user decision 2026-11-12 "C").
    /// View only: everything comes in through the setters.
    /// </summary>
    public sealed class BoardMarksView : MonoBehaviour
    {
        static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.8f);

        RectTransform layer;
        Func<Vector3, Vector2> toCanvas;
        Image ring;
        Image goal, goalShadow; // the staged hint's goal square (gold ring, QA-1 (A))
        bool goalOn;
        Vector3 goalAt;
        Image knotA, knotB;
        readonly List<Image> dots = new List<Image>();
        // Wake points (user request 2026-11-13): where the next brother wakes for each choice, both at once while choosing.
        Image keepWake; // ring on the current start (KEEP); SWITCH wakes on the brother's own cell, inside his ring
        WakeTag keepTag, switchTag;

        sealed class WakeTag
        {
            public Image Plate;
            public Text Text;
            public CanvasGroup Group;
        }

        /// The wake tags' words and font (true = KEEP), set by the scene root from the localizer; null = no tags.
        public Func<bool, (string text, Font font)> WakeWords;
        public const int WakeTagSize = 18;
        /// Tag centre under its cell (cells), and the second tag's extra drop when both choices wake on one cell.
        public const float WakeTagDrop = 0.62f, WakeTagStack = 0.42f, WakeOtherAlpha = 0.6f;

        bool reduceMotion;
        float cellPx = 77f;

        // Choice
        bool ringOn;
        Vector3 brother, keepStart;
        Color next, current;
        ChoiceFocus focus;

        public enum ChoiceFocus { None, Keep, Switch }

        public bool RingShown => ring.enabled;
        public bool GoalShown => goal.enabled;
        public bool KnotShown => knotA.enabled;
        /// The KEEP wake ring on the start and the two wake tags (tests, captures).
        public bool KeepWakeShown => keepWake.enabled;
        public bool WakeTagsShown => keepTag.Plate.gameObject.activeSelf && switchTag.Plate.gameObject.activeSelf;
        public Text KeepTagText => keepTag.Text;
        public Text SwitchTagText => switchTag.Text;
        /// Canvas positions of the tags (tests).
        public Vector2 KeepTagAt => keepTag.Plate.rectTransform.anchoredPosition;
        public Vector2 SwitchTagAt => switchTag.Plate.rectTransform.anchoredPosition;
        public int DotsShown { get { int n = 0; foreach (var d in dots) if (d.enabled) n++; return n; } }

        /// <param name="toCanvas">World point → anchored position on <paramref name="layer"/> (canvas units).</param>
        public void Init(RectTransform layer, Func<Vector3, Vector2> toCanvas)
        {
            this.layer = layer != null ? layer : throw new ArgumentNullException(nameof(layer));
            this.toCanvas = toCanvas ?? throw new ArgumentNullException(nameof(toCanvas));
            ring = Make("Choice Ring", Ui.RingShape);
            goalShadow = Make("Hint Goal Shadow", Ui.RingShape);
            goal = Make("Hint Goal", Ui.RingShape);
            dots.Capacity = 64;
            knotA = Make("Knot A", null);
            knotB = Make("Knot B", null);
            keepWake = Make("Keep Wake", Ui.RingShape);
            keepTag = Tag("Keep Wake Tag");
            switchTag = Tag("Switch Wake Tag");
            Refresh();
        }

        WakeTag Tag(string name)
        {
            var plate = Ui.Plate(layer, name);
            var r = plate.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            var t = Ui.Label(r, "", WakeTagSize, Ui.Ink, TextAnchor.MiddleCenter, "Text");
            t.rectTransform.Stretch();
            t.raycastTarget = false;
            var g = plate.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = g.interactable = false;
            plate.gameObject.SetActive(false);
            return new WakeTag { Plate = plate, Text = t, Group = g };
        }

        Image Make(string name, Sprite sprite)
        {
            var img = Ui.MakeImage(layer, Color.white, sprite, name);
            img.raycastTarget = false;
            img.enabled = false;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        /// Look shared by the marks: reduce motion (steady instead of breathing) and a cell's size on the canvas.
        public void SetLook(bool reduceMotion, float cellPx)
        {
            this.reduceMotion = reduceMotion;
            this.cellPx = cellPx > 0 ? cellPx : 77f;
            Refresh();
        }

        /// The choice marks: ring round the brother (next brother's colour), and the focused choice's dotted line / knot.
        public void SetChoice(bool on, Vector3 brother, Vector3 keepStart, Color next, Color current, ChoiceFocus focus)
        {
            ringOn = on;
            this.brother = brother;
            this.keepStart = keepStart;
            this.next = next;
            this.current = current;
            this.focus = on ? focus : ChoiceFocus.None;
            Refresh();
        }

        /// The staged hint's goal: a breathing gold ring on the square where a body's run should end, or none.
        public void SetGoal(bool on, Vector3 world)
        {
            goalOn = on;
            goalAt = world;
            Refresh();
        }

        public void Clear()
        {
            ringOn = false;
            focus = ChoiceFocus.None;
            Refresh();
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            if (layer == null) return;
            float scale = Mathf.Clamp(cellPx / 77f, 0.85f, 1.25f);
            // Hint goal: gold on a dark ring, readable on light and dark stone.
            goal.enabled = goalShadow.enabled = goalOn;
            if (goalOn)
            {
                float breathe = reduceMotion ? 1f : 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 3f);
                var at = toCanvas(goalAt);
                Put(goalShadow, at, 0.98f * cellPx * breathe, ShadowColor);
                Put(goal, at, 0.9f * cellPx * breathe, Ui.Gold);
            }

            // Choice
            ring.enabled = ringOn;
            if (ringOn)
            {
                float breathe = reduceMotion ? 1f : 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 4f);
                Put(ring, toCanvas(brother), 1.15f * cellPx * breathe, next);
            }
            int shown = 0;
            if (focus == ChoiceFocus.Keep)
            {
                Vector2 a = toCanvas(brother), b = toCanvas(keepStart);
                float len = Vector2.Distance(a, b);
                int n = Mathf.Min(64, Mathf.FloorToInt(len / 14f));
                for (int i = 1; i < n; i++)
                {
                    if (shown == dots.Count) dots.Add(Make("Keep Dot", Ui.Circle));
                    Put(dots[shown++], Vector2.Lerp(a, b, i / (float)n), 6f * scale, next);
                }
            }
            for (int i = shown; i < dots.Count; i++) dots[i].enabled = false;
            knotA.enabled = knotB.enabled = focus == ChoiceFocus.Switch;
            if (knotA.enabled)
            {
                Vector2 p = toCanvas(brother) + new Vector2(0, -0.28f * cellPx);
                Put(knotA, p, 0, current, new Vector2(0.5f * cellPx, 6f * scale), 35f);
                Put(knotB, p, 0, next, new Vector2(0.5f * cellPx, 6f * scale), -35f);
            }

            // Wake points: KEEP wakes on the start (a ring there), SWITCH right here (the brother's ring); a tag under each
            // cell names the choice. The focused one is full, the other dimmed.
            Vector2 here = ringOn ? toCanvas(brother) : default, start = ringOn ? toCanvas(keepStart) : default;
            bool same = ringOn && Vector2.Distance(here, start) < 0.5f * cellPx;
            keepWake.enabled = ringOn && !same;
            if (keepWake.enabled)
                Put(keepWake, start, 0.85f * cellPx, new Color(next.r, next.g, next.b, focus == ChoiceFocus.Switch ? 0.45f : 0.9f));
            bool tags = ringOn && WakeWords != null;
            float drop = WakeTagDrop * cellPx;
            PutTag(keepTag, tags, true, start + new Vector2(0, -drop), focus != ChoiceFocus.Switch);
            PutTag(switchTag, tags, false, here + new Vector2(0, -drop - (same ? WakeTagStack * cellPx : 0)), focus == ChoiceFocus.Switch);
        }

        void PutTag(WakeTag tag, bool on, bool keep, Vector2 at, bool focused)
        {
            tag.Plate.gameObject.SetActive(on);
            if (!on) return;
            var (words, font) = WakeWords(keep);
            if (font != null) tag.Text.font = font;
            if (tag.Text.text != words) tag.Text.text = words;
            float w = tag.Text.cachedTextGeneratorForLayout.GetPreferredWidth(words, tag.Text.GetGenerationSettings(Vector2.zero))
                      / Mathf.Max(0.01f, tag.Text.pixelsPerUnit);
            var r = tag.Plate.rectTransform;
            r.sizeDelta = new Vector2(Mathf.Ceil(w) + 16, WakeTagSize + 10);
            r.anchoredPosition = at;
            tag.Group.alpha = focused ? 1f : WakeOtherAlpha;
        }

        /// After a KEEP / SWITCH (user request 2026-11-13): the cell the next brother woke on pulses in his colour, and a
        /// KEEP's new stone gets a steady light ring, for <paramref name="seconds"/>. Does not hold the session (started,
        /// not awaited); a later Present / Undo / Snap drops it (StopBursts). Reduce motion: steady rings.
        public IEnumerator Wake(Vector3 wake, Color c, Vector3? stone, float seconds)
        {
            if (seconds <= 0f) yield break;
            var ringImg = Make("Wake Ring", Ui.RingShape);
            bursts.Add(ringImg);
            Image stoneImg = null;
            if (stone.HasValue)
            {
                stoneImg = Make("Stone Ring", Ui.RingShape);
                bursts.Add(stoneImg);
            }
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (ringImg == null) yield break;
                float k = t / seconds, fade = k > 0.75f ? 1f - (k - 0.75f) / 0.25f : 1f;
                float pulse = reduceMotion ? 1f : 1f + 0.35f * Mathf.Abs(Mathf.Cos(k * 2f * Mathf.PI));
                Put(ringImg, toCanvas(wake), 0.95f * cellPx * pulse, new Color(c.r, c.g, c.b, fade));
                if (stoneImg != null) Put(stoneImg, toCanvas(stone.Value), 1.05f * cellPx, new Color(1f, 1f, 1f, 0.85f * fade));
                yield return null;
            }
            foreach (var img in new[] { ringImg, stoneImg })
            {
                if (img == null) continue;
                bursts.Remove(img);
                Destroy(img.gameObject);
            }
        }

        /// Wake marks of the last confirm still on screen (tests).
        public int WakeRingsShown { get { int n = 0; foreach (var b in bursts) if (b != null && (b.name == "Wake Ring" || b.name == "Stone Ring")) n++; return n; } }

        static void Put(Image img, Vector2 p, float size, Color c, Vector2? rect = null, float angle = 0f)
        {
            img.enabled = true;
            var r = img.rectTransform;
            r.anchoredPosition = p;
            r.sizeDelta = rect ?? new Vector2(size, size);
            r.localRotation = Quaternion.Euler(0, 0, angle);
            img.color = c;
        }

        /// Confirm presentation of KEEP / SWITCH (inside that command's Present): a star that opens and fades on
        /// <paramref name="world"/> over <paramref name="seconds"/> (0 = nothing).
        public IEnumerator Burst(Vector3 world, Color c, float seconds)
        {
            if (seconds <= 0f) yield break;
            var star = Make("Confirm Burst", Ui.StarShape);
            bursts.Add(star);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (star == null) yield break;
                float k = t / seconds;
                Put(star, toCanvas(world), (0.35f + 0.75f * k) * cellPx, new Color(c.r, c.g, c.b, 1f - k * k));
                yield return null;
            }
            bursts.Remove(star);
            if (star != null) Destroy(star.gameObject);
        }

        readonly List<Image> bursts = new List<Image>();

        /// The presenter stopped (Undo, Restart, Snap): a stopped coroutine does not clean up, so drop its bursts here.
        public void StopBursts()
        {
            foreach (var b in bursts) if (b != null) Destroy(b.gameObject);
            bursts.Clear();
        }
    }
}
