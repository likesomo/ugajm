using System;
using System.Collections;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.U5
{
    /// <summary>
    /// U5 in-game HUD (docs/ui-u5-kit.md 3, U5-hud / U5-choice): one top band - pause 48 at the top left; a small stage
    /// title with the three scarf dots under it at the top centre; hint, undo, restart 48 at the top right - and, at the
    /// 7th step, the KEEP / SWITCH glyph pair (56, 12 apart) beside the board near the brother with one focus plate.
    /// The current HUD (<see cref="HudView"/>) stays underneath for what U5 keeps as it was (board labels, message line,
    /// statue count, guide and story plates); <see cref="HideClassic"/> hides its own buttons, title, dots and cards.
    /// View only: buttons raise events, the scene root wires them to the same controller calls as the current HUD
    /// (docs 7). Reads State only.
    /// </summary>
    public sealed class U5GameHud : MonoBehaviour, IChoiceView
    {
        public const float Safe = 24, TitleY = 16, TitleH = 36, DotsY = 58, DotSize = 12, DotPitch = 22, GlyphGap = 12;
        /// Above the current HUD (0), under the overlays (10) so the menu, settings and result cover it.
        public const int SortingOrder = 5;

        IconButton pause, hint, undo, restart;
        GlyphButton keep, switchBody;
        RectTransform glyphs;
        Image keepRing, switchRing;
        FocusPlate plate;
        Text title;
        Image titlePlate;
        RectTransform dotsRoot;
        readonly List<(RectTransform root, Image ring, Image core)> dots = new List<(RectTransform, Image, Image)>();
        readonly List<int> statueOwners = new List<int>();
        TileSet bodyLook;
        Localizer loc;
        UiIconSet icons;

        public event Action PauseRequested, HintRequested, UndoRequested, RestartRequested, KeepRequested, SwitchRequested;
        public event Action<bool> ChoiceFocused;

        public IconButton Pause => pause;
        public IconButton Hint => hint;
        public IconButton Undo => undo;
        public IconButton Restart => restart;
        public GlyphButton Keep => keep;
        public GlyphButton Switch => switchBody;
        public FocusPlate Plate => plate;
        public Text Title => title;
        /// The glyph pair (top-left anchored on the canvas while shown).
        public RectTransform Glyphs => glyphs;

        /// The projected board area (canvas units, top-left origin) the focus plate and the glyphs keep clear of. Set by the
        /// scene root (BoardView.ProjectedArea); none: only the HUD buttons are avoided.
        public Func<Rect> BoardArea;
        /// Extra rects the glyphs keep clear of (the step-star sky band, the statue plate), canvas units, top-left origin.
        public Func<IList<Rect>> ChoiceAvoid;

        /// Builds the HUD on its own canvas (Edit Mode too). <paramref name="icons"/> may be null (line placeholders).
        public static U5GameHud Build(UiIconSet icons, TileSet bodyLook, Localizer loc)
        {
            if (bodyLook == null) throw new ArgumentNullException(nameof(bodyLook));
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            var go = new GameObject("U5 Hud", typeof(RectTransform)) { layer = 5 };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var h = go.AddComponent<U5GameHud>();
            h.bodyLook = bodyLook;
            h.icons = icons;
            h.loc = loc;
            h.Assemble();
            h.Apply(loc);
            return h;
        }

        void Assemble()
        {
            var root = transform;
            // Top band (touch-targets.csv U5 hud): pause left; hint, undo, restart right, 60 apart.
            pause = IconButton.Build(root, UiIcon.Pause, icons, "Esc", () => PauseRequested?.Invoke(), name: "Pause");
            pause.Rect.TopLeft(Safe, Safe, IconButton.Size, IconButton.Size);
            hint = IconButton.Build(root, UiIcon.Hint, icons, "H", () => HintRequested?.Invoke(), name: "Hint");
            undo = IconButton.Build(root, UiIcon.Undo, icons, "Z", () => UndoRequested?.Invoke(), name: "Undo");
            restart = IconButton.Build(root, UiIcon.Restart, icons, "R", () => RestartRequested?.Invoke(), name: "Restart");
            float x = 1280 - Safe;
            foreach (var b in new[] { restart, undo, hint })
            {
                x -= IconButton.Size;
                b.Rect.anchorMin = b.Rect.anchorMax = b.Rect.pivot = new Vector2(1, 1);
                b.Rect.anchoredPosition = new Vector2(x + IconButton.Size - 1280, -Safe); // right edge (pivot 1, 1)
                b.Rect.sizeDelta = new Vector2(IconButton.Size, IconButton.Size);
                x -= 12;
            }
            IconNav.Row(new[] { pause, hint, undo, restart });

            // Title on a small plate (14.4) and the scarf dots under it, top centre.
            titlePlate = Ui.Plate(root, "Title plate");
            titlePlate.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -TitleY), new Vector2(200, TitleH));
            title = Ui.Label(titlePlate.transform, "", 22, Ui.Ink, TextAnchor.MiddleCenter, "Title");
            title.rectTransform.Stretch();
            dotsRoot = Ui.Rect("Scarf dots", root);
            dotsRoot.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -DotsY), new Vector2(3 * DotPitch, DotSize));

            // KEEP / SWITCH: the glyph pair, a ring in the next brother's colour behind the focused one.
            glyphs = Ui.Rect("Choice glyphs", root);
            glyphs.anchorMin = glyphs.anchorMax = glyphs.pivot = new Vector2(0, 1);
            keepRing = Ring("Keep ring");
            switchRing = Ring("Switch ring");
            keep = GlyphButton.Build(glyphs, UiIcon.Keep, icons, "K", () => KeepRequested?.Invoke(), "ui5.choice.keep", "Keep");
            switchBody = GlyphButton.Build(glyphs, UiIcon.Switch, icons, "X", () => SwitchRequested?.Invoke(), "ui5.choice.switch", "Switch");
            foreach (var g in new IconButton[] { keep, switchBody })
                g.FocusChanged += (b, source, on) => { if (on) ChoiceFocused?.Invoke(b == keep); };
            IconNav.Row(new IconButton[] { keep, switchBody });
            glyphs.gameObject.SetActive(false);

            plate = FocusPlate.Build(root, loc, icons);
            plate.Avoid = PlateAvoid;
            foreach (var b in new IconButton[] { pause, hint, undo, restart, keep, switchBody }) plate.Track(b);
            // The choice's words go right over / under the pair, not under one glyph (docs 6).
            plate.AnchorTo(keep, glyphs);
            plate.AnchorTo(switchBody, glyphs);
        }

        Image Ring(string name)
        {
            var r = Ui.MakeImage(glyphs, Color.white, Ui.RingShape, name);
            r.rectTransform.anchorMin = r.rectTransform.anchorMax = r.rectTransform.pivot = new Vector2(0, 1);
            r.rectTransform.sizeDelta = new Vector2(GlyphButton.Size + 8, GlyphButton.Size + 8);
            return r;
        }

        readonly List<Rect> avoidRects = new List<Rect>();

        IReadOnlyList<Rect> PlateAvoid()
        {
            avoidRects.Clear();
            if (BoardArea != null) avoidRects.Add(BoardArea());
            if (glyphs.gameObject.activeSelf) avoidRects.Add(GlyphRect);
            return avoidRects;
        }

        Rect GlyphRect => new Rect(glyphs.anchoredPosition.x, -glyphs.anchoredPosition.y, glyphs.sizeDelta.x, glyphs.sizeDelta.y);

        /// Fixed words and fonts in the current language; redraws the last state.
        public void Apply(Localizer localizer)
        {
            loc = localizer ?? throw new ArgumentNullException(nameof(localizer));
            title.font = Ui.Theme.FontFor(loc.Current, FontRole.Title);
            if (shownText != null) SetTitle(shownText.name ?? "");
        }

        LevelTextDto shownText;

        /// Redraws from the state (BoardController.HudRefreshed): the stage name and the brothers' dots.
        public void Refresh(State s, int stage, LevelTextDto text)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            shownText = text ?? throw new ArgumentNullException(nameof(text));
            shownState = s;
            SetTitle(text.name ?? "");
            RefreshDots(s);
        }

        void SetTitle(string name)
        {
            title.text = name;
            titlePlate.gameObject.SetActive(name.Length > 0);
            if (name.Length == 0) return;
            var gen = title.cachedTextGeneratorForLayout;
            float w = gen.GetPreferredWidth(name, title.GetGenerationSettings(Vector2.zero)) / Mathf.Max(0.01f, title.pixelsPerUnit);
            titlePlate.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(w) + 24, TitleH);
        }

        void RefreshDots(State s)
        {
            int count = s.Level.Bodies;
            while (dots.Count < count)
            {
                var r = Ui.Rect($"Brother {dots.Count + 1}", dotsRoot);
                r.sizeDelta = new Vector2(DotSize, DotSize);
                var ring = Ui.MakeImage(r, Color.white, Ui.Circle, "Ring");
                ring.rectTransform.Stretch();
                var core = Ui.MakeImage(r, Color.white, Ui.Circle, "Core");
                core.rectTransform.Stretch();
                core.rectTransform.offsetMin = new Vector2(2.5f, 2.5f);
                core.rectTransform.offsetMax = new Vector2(-2.5f, -2.5f);
                dots.Add((r, ring, core));
            }
            statueOwners.Clear();
            foreach (int cell in s.Statues) statueOwners.Add(BodyIndex.OfStatue(s, cell));
            dotsRoot.sizeDelta = new Vector2(count * DotPitch, DotSize);
            for (int k = 0; k < dots.Count; k++)
            {
                var (r, ring, core) = dots[k];
                r.gameObject.SetActive(k < count);
                if (k >= count) continue;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2((k - (count - 1) / 2f) * DotPitch, 0);
                var c = bodyLook.BodyColor(k);
                // The current HUD's dot grammar (design/art/ui-minimal.md "형제 점"): shape as well as colour.
                var look = HudView.LookOf(s, k, statueOwners);
                r.localRotation = look == HudView.DotLook.Stone ? Quaternion.Euler(0, 0, 45) : Quaternion.identity;
                r.localScale = look switch
                {
                    HudView.DotLook.Walking => new Vector3(1.35f, 1.35f, 1),
                    HudView.DotLook.Spent => new Vector3(1f, 0.5f, 1),
                    _ => Vector3.one,
                };
                var sprite = look == HudView.DotLook.Stone ? null : (icons != null && icons.scarfDot != null ? icons.scarfDot : Ui.Circle);
                ring.sprite = core.sprite = sprite;
                ring.color = look switch
                {
                    HudView.DotLook.Stone => Ui.Stone,
                    HudView.DotLook.Spent => new Color(c.r, c.g, c.b, 0.45f),
                    _ => c,
                };
                core.color = look switch
                {
                    HudView.DotLook.Walking => c,
                    HudView.DotLook.Stone => Ui.Stone,
                    HudView.DotLook.Spent => new Color(c.r, c.g, c.b, 0.45f),
                    _ => new Color(0.06f, 0.08f, 0.12f, 1f),
                };
            }
        }

        /// The look of brother <paramref name="k"/>'s dot (tests): Walking, Waiting, Stone, Spent.
        public HudView.DotLook DotLookOf(int k) => HudView.LookOf(shownState ?? throw new InvalidOperationException("no state yet"), k, statueOwners);
        State shownState;

        // ---------- KEEP / SWITCH (IChoiceView) ----------

        HudView.ChoiceCards shownCards;
        public HudView.ChoiceCards ShownCards => shownCards;

        public void ShowChoice(HudView.ChoiceCards c)
        {
            bool was = shownCards.On;
            bool moved = was && c.On && c.FocusKeep != shownCards.FocusKeep;
            shownCards = c;
            if (!c.On)
            {
                if (was)
                {
                    keep.SetShown(false);
                    switchBody.SetShown(false);
                    plate.Model.Forget(keep); // the first-visit words were put there by this view, not by the glyph
                    plate.Model.Forget(switchBody);
                    glyphs.gameObject.SetActive(false);
                }
                return;
            }
            if (!was)
            {
                glyphs.gameObject.SetActive(true);
                keep.SetShown(true);
                switchBody.SetShown(true);
                IconNav.Row(new IconButton[] { keep, switchBody });
            }
            var color = bodyLook.BodyColor(c.NextBody);
            keepRing.color = switchRing.color = color;
            keepRing.gameObject.SetActive(c.FocusKeep);
            switchRing.gameObject.SetActive(!c.FocusKeep);
            // First choices (docs 4): the focused glyph explains itself from the start. Later: only when the player moves the
            // focus (keyboard, hover, a first tap) - hover and holds reach the plate through the glyph's own events.
            var focused = c.FocusKeep ? (IconButton)keep : switchBody;
            var other = c.FocusKeep ? (IconButton)switchBody : keep;
            plate.Model.Exit(TipSource.Focus, other);
            if (c.Explain || moved) plate.Model.Enter(TipSource.Focus, focused, focused.TipKey, focused.Shortcut);
            else plate.Model.Exit(TipSource.Focus, focused);
        }

        /// Places the glyph pair beside the board near the brother (R2 on 40a0561): of the 14.11 candidates
        /// (HudView.LayoutChoice: stacked left / right of the board area, side by side above / below it; the board area
        /// is the whole grid with its walls up to actor height) the one nearest to the brother's projected head that is
        /// clear of <paramref name="avoid"/> (the step-star sky band) and <see cref="ChoiceAvoid"/>. None fits: right over
        /// the brother on the wall / cells, marked UiLayout exempt Board with that reason.
        public bool PlaceChoice(Rect board, Vector2 brother, IList<Rect> avoid) =>
            PlaceChoice(board, brother, ((RectTransform)transform).rect.size, avoid);

        public bool PlaceChoice(Rect board, Vector2 brother, Vector2 canvas, IList<Rect> avoid = null)
        {
            float g = GlyphButton.Size, pair = 2 * g + GlyphGap;
            var all = new List<Rect>();
            if (avoid != null) all.AddRange(avoid);
            var extra = ChoiceAvoid?.Invoke();
            if (extra != null) all.AddRange(extra);
            var r = HudView.LayoutChoice(board, brother, canvas, new Vector2(g, pair), new Vector2(pair, g), all,
                out bool vertical, out bool fits);
            var mark = glyphs.GetComponent<UiLayout>();
            if (!fits)
            {
                vertical = false;
                r = new Rect(Mathf.Clamp(brother.x - pair / 2, Safe, canvas.x - Safe - pair),
                    Mathf.Clamp(brother.y - g - 24, Safe + IconButton.Size + 8, canvas.y - Safe - g), pair, g);
                if (mark == null || (mark.exempt & UiLayoutRule.Board) == 0)
                    Ui.Exempt(glyphs.gameObject, UiLayoutRule.Board, "U5 choice: no room beside the board, the glyphs stand over the brother (R2 on 40a0561)");
            }
            else if (mark != null) mark.exempt &= ~UiLayoutRule.Board;
            glyphs.anchoredPosition = new Vector2(r.x, -r.y);
            glyphs.sizeDelta = r.size;
            Put(keep.Rect, keepRing.rectTransform, 0, 0);
            Put(switchBody.Rect, switchRing.rectTransform, vertical ? 0 : g + GlyphGap, vertical ? g + GlyphGap : 0);
            placedVertical = vertical;
            return fits;
        }

        bool placedVertical;
        /// The pair is stacked (left / right of the board) rather than side by side.
        public bool Vertical => placedVertical;

        static void Put(RectTransform glyph, RectTransform ring, float x, float y)
        {
            glyph.anchorMin = glyph.anchorMax = glyph.pivot = new Vector2(0, 1);
            glyph.anchoredPosition = new Vector2(x, -y);
            glyph.sizeDelta = new Vector2(GlyphButton.Size, GlyphButton.Size);
            ring.anchoredPosition = new Vector2(x - 4, -(y - 4));
        }

        public void NudgeChoice()
        {
            if (glyphs.gameObject.activeInHierarchy) StartCoroutine(Shake(glyphs));
        }

        static IEnumerator Shake(RectTransform r)
        {
            Vector2 home = r.anchoredPosition;
            for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                r.anchoredPosition = home + new Vector2(Mathf.Sin(t * 60f) * 8f * (1 - t / 0.3f), 0);
                yield return null;
            }
            r.anchoredPosition = home;
        }

        // ---------- result, smoke ----------

        /// The clear result over the board (docs 14.7): the whole U5 HUD hides with the current HUD.
        public void Conceal(bool hidden)
        {
            if (!TryGetComponent<CanvasGroup>(out var group)) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = hidden ? 0f : 1f;
            group.blocksRaycasts = !hidden;
            group.interactable = !hidden;
            if (hidden) plate.Model.Clear();
        }

        /// Hides what U5 replaces in the current HUD (its menu, undo, restart buttons, title, brother dots, statue-count
        /// plate; its KEEP / SWITCH cards are never shown because the choice is on U5). CanvasGroups, not SetActive, where the current HUD switches the object itself on each refresh.
        public static void HideClassic(HudView hud)
        {
            if (hud == null) throw new ArgumentNullException(nameof(hud));
            foreach (var b in new Component[] { hud.menu, hud.undo, hud.restart }) b.gameObject.SetActive(false);
            // The statue-count plate too: U5 keeps board information on the board (the count door's sockets, TilePropView).
            foreach (var go in new[] { hud.title.transform.parent.gameObject, hud.bodyDots[0].root.parent.gameObject, hud.statues.transform.parent.gameObject })
            {
                if (!go.TryGetComponent<CanvasGroup>(out var g)) g = go.AddComponent<CanvasGroup>();
                g.alpha = 0;
                g.blocksRaycasts = false;
                g.interactable = false;
            }
        }
    }
}
