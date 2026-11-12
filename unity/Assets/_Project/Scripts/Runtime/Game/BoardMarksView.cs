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
    /// - the short confirm burst of a KEEP / SWITCH Present.
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
            Refresh();
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
        }

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
