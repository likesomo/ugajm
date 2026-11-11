using System;
using System.Collections;
using System.Collections.Generic;
using SevenSteps.Core;
using SevenSteps.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game
{
    /// <summary>
    /// Screen-space marks that follow the 3D board (on the HUD's world label layer, behind the HUD plates), drawn by
    /// BoardView from the State it draws (design/art/ui-minimal.md v3, docs/architecture.md 4.4 / 15.6):
    /// - the seven stars at the walking brother's feet: filled = steps walked, empty = steps left, no numbers; after the 6th
    ///   step the last empty star pulses 17-23 px (reduce motion: a steady double outline instead);
    /// - while choosing (cards shown): the ring around the brother, and for the focused choice the KEEP dotted line to the
    ///   next start or the SWITCH knot on this cell (the 3D ghosts are BoardView's);
    /// - the short confirm burst of a KEEP / SWITCH Present.
    /// Screen space so the stars keep their pixel size and are never hidden by the raised front tiles (the world-quad ring
    /// of the interim star trail was, 2026-11-10). View only: everything comes in through the setters.
    /// </summary>
    public sealed class BoardMarksView : MonoBehaviour
    {
        /// Star size at the feet, canvas units at 1280 x 720, and the pulse range of the last star. v3 had 16 / 17-23 in a
        /// row; on the real 1280 x 720 board that read small (lead 2026-11-11), so 22 / 23-30 on an arc round the feet.
        public const float FootStar = 22f, PulseMin = 23f, PulseMax = 30f, PulsePeriod = 1.4f;
        /// The arc: half-ellipse in front of the feet, radii in cells (x wide enough for seven stars side by side).
        const float ArcX = 0.95f, ArcY = 0.34f, ArcFrom = 200f, ArcTo = 340f;
        /// Inner star (fill) and the dark outline behind each star (contrast on light stone, ui-minimal "암부 외곽").
        const float Inner = 0.62f, Shadow = 1.3f;
        static readonly Color EmptyFill = new Color(0.06f, 0.10f, 0.13f, 1f);
        static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.8f);
        /// A soft dark ellipse under the arc: contrast on every planet's stone (frost and altar are near white).
        static readonly Color BackingColor = new Color(0.02f, 0.04f, 0.06f, 0.5f);
        Image backing;

        RectTransform layer;
        Func<Vector3, Vector2> toCanvas;
        readonly Image[] shadow = new Image[Rules.Steps], outer = new Image[Rules.Steps], inner = new Image[Rules.Steps];
        Image calm; // reduce motion: the steady second outline round the last star
        Image ring;
        Image goal, goalShadow; // the staged hint's goal square (gold ring, QA-1 (A))
        bool goalOn;
        Vector3 goalAt;
        Image knotA, knotB;
        readonly List<Image> dots = new List<Image>();

        // Feet
        bool feetOn, pulse, reduceMotion;
        Transform follow;
        Vector3 footOffset;
        int used;
        Color body;
        float cellPx = 77f;

        // Choice
        bool ringOn;
        Vector3 brother, keepStart;
        Color next, current;
        ChoiceFocus focus;

        public enum ChoiceFocus { None, Keep, Switch }

        /// The canvas point of each foot star (tests, captures): star k = step k + 1.
        public Vector2 StarAt(int k) => outer[k].rectTransform.anchoredPosition;
        public float StarSize(int k) => outer[k].rectTransform.sizeDelta.x;
        public bool StarFilled(int k) => inner[k].color == Ui.Ink;
        public bool FeetShown => outer[0].enabled;
        public bool RingShown => ring.enabled;
        public bool GoalShown => goal.enabled;
        public bool KnotShown => knotA.enabled;
        public int DotsShown { get { int n = 0; foreach (var d in dots) if (d.enabled) n++; return n; } }

        /// <param name="toCanvas">World point → anchored position on <paramref name="layer"/> (canvas units).</param>
        public void Init(RectTransform layer, Func<Vector3, Vector2> toCanvas)
        {
            this.layer = layer != null ? layer : throw new ArgumentNullException(nameof(layer));
            this.toCanvas = toCanvas ?? throw new ArgumentNullException(nameof(toCanvas));
            backing = Make("Foot Stars Backing", Ui.Circle);
            ring = Make("Choice Ring", Ui.RingShape);
            goalShadow = Make("Hint Goal Shadow", Ui.RingShape);
            goal = Make("Hint Goal", Ui.RingShape);
            dots.Capacity = 64;
            knotA = Make("Knot A", null);
            knotB = Make("Knot B", null);
            for (int k = 0; k < Rules.Steps; k++)
            {
                shadow[k] = Make($"Foot Star {k + 1} Shadow", Ui.StarShape);
                outer[k] = Make($"Foot Star {k + 1}", Ui.StarShape);
                inner[k] = Make($"Foot Star {k + 1} Fill", Ui.StarShape);
            }
            calm = Make("Foot Star 7 Calm", Ui.StarShape);
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

        /// The seven stars at the feet of <paramref name="follow"/> (the player), or none.
        public void SetFeet(bool on, Transform follow, Vector3 footOffset, Color body, int used, bool reduceMotion, float cellPx)
        {
            feetOn = on && follow != null;
            this.follow = follow;
            this.footOffset = footOffset;
            this.body = body;
            this.used = Mathf.Clamp(used, 0, Rules.Steps);
            this.reduceMotion = reduceMotion;
            this.cellPx = cellPx > 0 ? cellPx : 77f;
            pulse = feetOn && this.used == Rules.Steps - 1; // after the 6th step: the last empty star (7th) calls
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
            feetOn = ringOn = false;
            focus = ChoiceFocus.None;
            Refresh();
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            if (layer == null) return;
            float scale = Mathf.Clamp(cellPx / 77f, 0.85f, 1.25f);
            // Feet: a shallow smile of seven stars under the brother.
            for (int k = 0; k < Rules.Steps; k++) shadow[k].enabled = outer[k].enabled = inner[k].enabled = feetOn;
            backing.enabled = feetOn;
            calm.enabled = feetOn && pulse && reduceMotion;
            if (feetOn)
            {
                Vector2 c = toCanvas(follow.position + footOffset);
                float rx = Mathf.Max(70f, ArcX * cellPx), ry = Mathf.Max(24f, ArcY * cellPx);
                float star = FootStar * scale;
                Put(backing, c + new Vector2(0, -0.67f * ry), 0, BackingColor, new Vector2(1.9f * rx + 1.6f * star, 0.7f * ry + 1.8f * star));
                for (int k = 0; k < Rules.Steps; k++)
                {
                    // Left to right along the front of the feet (step 1 on the left), canvas y up.
                    float a = Mathf.Lerp(ArcFrom, ArcTo, k / (Rules.Steps - 1f)) * Mathf.Deg2Rad;
                    var p = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                    float size = FootStar * scale;
                    bool calling = pulse && k == Rules.Steps - 1;
                    if (calling)
                        size = reduceMotion ? (PulseMin + PulseMax) * 0.5f * scale
                            : Mathf.Lerp(PulseMin, PulseMax, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / PulsePeriod)) * scale;
                    Put(shadow[k], p, size * Shadow, ShadowColor);
                    // The last step's star calls in gold (a dark empty star alone was weak on moss, lead 2026-11-11).
                    Put(outer[k], p, size, calling ? Ui.Gold : body);
                    Put(inner[k], p, size * Inner, k < used ? Ui.Ink : EmptyFill);
                    if (k == Rules.Steps - 1 && calm.enabled) Put(calm, p, size * 1.6f, new Color(body.r, body.g, body.b, 0.7f));
                }
                calm.transform.SetSiblingIndex(shadow[Rules.Steps - 1].transform.GetSiblingIndex());
            }

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
