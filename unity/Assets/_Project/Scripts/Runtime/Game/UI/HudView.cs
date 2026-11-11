using System;
using System.Collections;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// Game HUD, minimal (production/ui-minimal-plan.md "승인된 결정" 2026-11-11, design/art/ui-minimal.md): always on are
    /// only the menu button, a small stage title, undo, restart and the three brothers as muffler-colour dots. Everything
    /// else appears only while it has something to say: the message line, the statue count on statue-door stages, the
    /// guide (controls, shown by the flow on a first play) and the passing story line (<see cref="ShowLine"/>: the stage's
    /// start line or a planet's finale line, Story.StoryDirector decides when). Steps are read from the star trail and the stars at the brother's feet, not from numbers; the old numeric
    /// footprints come back with <see cref="StepNumbers"/> (accessibility, off by default).
    /// Prefab UI/Prefabs/Hud.prefab (its own canvas), referenced by UiTheme.hud, built by Seven Steps > Gameplay > Build HUD
    /// (HudBuilder). Reads State only and raises button events; controllers decide. Every word comes from the string table
    /// (hud.*, docs/architecture.md 13.2) in the role fonts of the current language; <see cref="Apply"/> redraws on a switch.
    /// </summary>
    public sealed class HudView : MonoBehaviour, Story.ITransientLine
    {
        [Header("Board labels (step numbers, gate digits) are placed here by BoardView")]
        public RectTransform labelLayer;

        [Header("Always on")]
        public Button menu;
        [Tooltip("Stage name only (no number), on its plate")]
        public Text title;
        public Button undo, restart;
        [Tooltip("One dot per body (A, B, C), muffler colour; extra bodies clone the first")]
        public BodyDot[] bodyDots = new BodyDot[3];

        [Header("Only while they have something to say")]
        [Tooltip("Message line (bumps, the H solution, ...) on its plate; the plate is the text's parent")]
        public Text message;
        [Tooltip("\"Statues 2 / 3\" on stages with statue-count doors; the plate is the text's parent")]
        public Text statues;
        [Tooltip("Left-margin plate with the stage story and the controls guide")]
        public GameObject guidePanel;
        public Text story, guide;

        [Header("KEEP / SWITCH (docs 4.4, 14.11): shown by the choice controller after the 7th step's Present")]
        [Tooltip("Top-left anchored group placed beside the projected board, near the brother")]
        public RectTransform choice;
        public Button keep, switchBody;
        [Tooltip("One-line result under each choice's name")]
        public Text keepDesc, switchDesc;
        [Tooltip("Card pictures: statue (KEEP), the next brother (SWITCH)")]
        public Image keepPicture, switchPicture;
        [Tooltip("Thin border lines of each card, in the next brother's muffler colour")]
        public Image[] keepEdges = new Image[4], switchEdges = new Image[4];
        [Tooltip("\"How will you pass it on?\" on its plate (the plate is the text's parent): the first choice only")]
        public Text choiceGuide;
        [Tooltip("After the first explained choices: the focused card's result line, on one plate under the compact cards")]
        public Text focusLine;
        [Tooltip("Full-screen 30% dim behind the HUD while choosing (exempt Decoration | Board, docs 14.11)")]
        public Image chooseDim;
        [Tooltip("Body A, B, C portraits (cycled), and the statue")]
        public Sprite[] bodySprites = new Sprite[3];
        public Sprite stoneSprite;

        [Serializable]
        public sealed class BodyDot
        {
            public RectTransform root;
            [Tooltip("Outer shape in the muffler colour")]
            public Image ring;
            [Tooltip("Inner fill: the colour while walking, dark while waiting")]
            public Image core;
        }

        // View events (docs/scene-flow.md 4.7): the HUD only reports; controllers decide.
        /// The menu button (settings, stage select, solution live in the menu). Scene root.
        public event Action MenuRequested;
        /// Board actions. Board controller.
        public event Action UndoRequested, RestartRequested, KeepRequested, SwitchRequested;
        /// A choice card got the pointer or the selection: true = KEEP. Choice controller (ghost of that choice only).
        public event Action<bool> ChoiceFocused;

        Localizer loc;
        State shownState;
        int shownStage = -1;
        LevelTextDto shownText;
        string shownMessage = "";
        bool guideOn, storyOn, stepNumbers;
        float storyUntil;

        TileSet bodyLook;
        readonly List<BodyDot> dots = new List<BodyDot>();
        readonly List<int> statueOwners = new List<int>();
        Level countLevel;
        bool levelHasCountDoor, wired;

        public RectTransform LabelLayer => labelLayer;

        /// Where the board goes (docs/architecture.md 14.6): the part of the 1280 x 720 reference canvas the minimal HUD
        /// leaves free, top-left origin. Above it the menu, title and brother dots (ending at 72), left and right the margins
        /// (252 each) for the guide and the KEEP / SWITCH cards, below it the message and the undo / restart buttons (docs 14.11).
        /// Sized so BoardView.Fit gives the 8 x 6 board the proposed framing (ortho about 4.65, cells about 77 px, padding .7).
        public static readonly Rect BoardFrame = new Rect(276, 80, 728, 496);

        /// <see cref="BoardFrame"/> as viewport fractions (x right, y up) on a screen of <paramref name="aspect"/> (width / height).
        /// The canvas (ScaleWithScreenSize 1280 x 720, match 0.5) is sqrt(921600 x aspect) x sqrt(921600 / aspect) units; the
        /// margins are pinned to the left / right edges, the top band to the top and the bottom band to the bottom, so the
        /// frame keeps its margins in units and stretches with the canvas (16:9 gives BoardFrame itself; architect N2).
        public static Rect BoardViewport(float aspect)
        {
            if (!(aspect > 0)) throw new ArgumentOutOfRangeException(nameof(aspect), aspect, "width / height");
            float w = Mathf.Sqrt(1280f * 720f * aspect), h = Mathf.Sqrt(1280f * 720f / aspect);
            float right = 1280f - BoardFrame.xMax, bottom = 720f - BoardFrame.yMax;
            return Rect.MinMaxRect(BoardFrame.xMin / w, bottom / h, 1f - right / w, 1f - BoardFrame.yMin / h);
        }

        /// Places the theme's HUD prefab (its own canvas) in the current scene, worded in <paramref name="loc"/>'s current
        /// language. LabelLayer is valid on return. Call <see cref="Apply"/> again on Localizer.Changed.
        public static HudView Create(TileSet bodyLook, Localizer loc)
        {
            if (bodyLook == null) throw new ArgumentNullException(nameof(bodyLook));
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            var prefab = Ui.Theme.hud != null ? Ui.Theme.hud
                : throw new MissingReferenceException($"[SevenSteps] UiTheme {Ui.Theme.name}: hud is not set (Seven Steps > Gameplay > Build HUD)");
            var view = Instantiate(prefab);
            view.name = prefab.name;
            view.bodyLook = bodyLook;
            view.Wire();
            view.Apply(loc);
            return view;
        }

        /// Required references missing in the prefab, one line each (OnValidate, Wire, SceneFlowValidator via UiTheme).
        public void Validate(List<string> issues)
        {
            void Need(Object o, string field) { if (o == null) issues.Add($"HudView {name}: {field} is not set"); }
            Need(labelLayer, nameof(labelLayer));
            Need(menu, nameof(menu)); Need(title, nameof(title)); Need(undo, nameof(undo)); Need(restart, nameof(restart));
            if (bodyDots == null || bodyDots.Length == 0) issues.Add($"HudView {name}: bodyDots needs at least one dot");
            else for (int k = 0; k < bodyDots.Length; k++)
            {
                var d = bodyDots[k];
                if (d == null) { issues.Add($"HudView {name}: bodyDots[{k}] is not set"); continue; }
                Need(d.root, $"bodyDots[{k}].root"); Need(d.ring, $"bodyDots[{k}].ring"); Need(d.core, $"bodyDots[{k}].core");
            }
            Need(message, nameof(message)); Need(statues, nameof(statues));
            Need(guidePanel, nameof(guidePanel)); Need(story, nameof(story)); Need(guide, nameof(guide));
            Need(choice, nameof(choice)); Need(keep, nameof(keep)); Need(switchBody, nameof(switchBody));
            Need(keepDesc, nameof(keepDesc)); Need(switchDesc, nameof(switchDesc));
            Need(keepPicture, nameof(keepPicture)); Need(switchPicture, nameof(switchPicture));
            foreach (var (edges, field) in new[] { (keepEdges, nameof(keepEdges)), (switchEdges, nameof(switchEdges)) })
                if (edges == null || edges.Length != 4) issues.Add($"HudView {name}: {field} needs 4 lines");
                else for (int k = 0; k < 4; k++) Need(edges[k], $"{field}[{k}]");
            Need(choiceGuide, nameof(choiceGuide)); Need(chooseDim, nameof(chooseDim)); Need(stoneSprite, nameof(stoneSprite));
            Need(focusLine, nameof(focusLine));
            if (focusLine != null && focusLine.transform.parent == transform) issues.Add($"HudView {name}: focusLine needs its plate as parent");
            if (bodySprites == null || bodySprites.Length == 0) issues.Add($"HudView {name}: bodySprites needs at least one sprite");
            else for (int k = 0; k < bodySprites.Length; k++) Need(bodySprites[k], $"bodySprites[{k}]");
            if (choiceGuide != null && choiceGuide.transform.parent == transform) issues.Add($"HudView {name}: choiceGuide needs its plate as parent");
            if (message != null && message.transform.parent == transform) issues.Add($"HudView {name}: message needs its plate as parent");
            if (statues != null && statues.transform.parent == transform) issues.Add($"HudView {name}: statues needs its plate as parent");
        }

        void OnValidate()
        {
            var issues = new List<string>();
            Validate(issues);
            foreach (var i in issues) Debug.LogError("[SevenSteps] " + i, this);
        }

        void Awake() => Wire();

        /// Checks the references and connects the buttons once (Create calls it: Awake does not run in edit-mode tests).
        void Wire()
        {
            if (wired) return;
            var issues = new List<string>();
            Validate(issues);
            if (issues.Count > 0) throw new MissingReferenceException("[SevenSteps] " + string.Join("; ", issues));
            menu.onClick.AddListener(() => MenuRequested?.Invoke());
            undo.onClick.AddListener(() => UndoRequested?.Invoke());
            restart.onClick.AddListener(() => RestartRequested?.Invoke());
            keep.onClick.AddListener(() => KeepRequested?.Invoke());
            switchBody.onClick.AddListener(() => SwitchRequested?.Invoke());
            Focusable(keep, true);
            Focusable(switchBody, false);
            dots.AddRange(bodyDots);
            wired = true;
        }

        /// Fixed words and fonts in the current language (Create, then on Localizer.Changed); redraws the last state.
        public void Apply(Localizer localizer)
        {
            loc = localizer ?? throw new ArgumentNullException(nameof(localizer));
            Set(keepDesc, "hud.keep_desc", FontRole.Body);
            Set(switchDesc, "hud.switch_desc", FontRole.Body);
            Set(guide, "hud.keys", FontRole.Body);
            Set(Label(keep), "hud.keep", FontRole.Title);
            Set(Label(switchBody), "hud.switch", FontRole.Title);
            Set(choiceGuide, "hud.choose_guide", FontRole.Body);
            focusLine.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            title.font = Ui.Theme.FontFor(loc.Current, FontRole.Title);
            Font body = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            foreach (var t in new[] { story, message, statues }) t.font = body;
            if (shownState != null) Refresh(shownState, shownStage, shownText, shownMessage);
        }

        void Set(Text text, string key, FontRole role)
        {
            text.text = loc.Get(key);
            text.font = Ui.Theme.FontFor(loc.Current, role);
        }

        static Text Label(Button b) => Array.Find(b.GetComponentsInChildren<Text>(true), t => t.name == "Label")
            ?? throw new MissingReferenceException($"[SevenSteps] HudView: button {b.name} has no \"Label\" Text");

        /// The controls guide (first play, new controls). The flow decides when (scene-flow saves what was seen).
        public bool Guide
        {
            get => guideOn;
            set { guideOn = value; RefreshGuide(); }
        }

        /// Accessibility "show step numbers" (off by default): the board shows numeric footprints and steps left again.
        /// The HUD itself has no numbers either way; BoardView reads the same switch.
        public bool StepNumbers
        {
            get => stepNumbers;
            set => stepNumbers = value;
        }

        /// Smoke checks (LaunchOptions.SmokeView): every HUD graphic off except the board's world labels (labelLayer, which
        /// BoardView shows or hides itself), so only the 3D board and its numbers are measured. Re-applied each frame
        /// because the HUD toggles its pieces while playing; smoke runs only.
        public void HideForSmoke() => smokeHidden = true;

        /// Clear result over the board (docs 14.7): a CanvasGroup on the HUD root makes every HUD graphic invisible and out
        /// of raycasts, the WorldLabels layer under it included (step numbers, foot stars, choice marks). The 3D board,
        /// its trail and its actors are not touched. false brings the HUD back as it was.
        public void Conceal(bool hidden)
        {
            if (!TryGetComponent<CanvasGroup>(out var group)) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = hidden ? 0f : 1f;
            group.blocksRaycasts = !hidden;
            group.interactable = !hidden;
        }

        public bool Concealed => TryGetComponent<CanvasGroup>(out var g) && g.alpha == 0f;

        bool smokeHidden;

        void LateUpdate()
        {
            if (storyOn && Time.unscaledTime >= storyUntil) { storyOn = false; RefreshGuide(); }
            if (!smokeHidden) return;
            foreach (var g in GetComponentsInChildren<Graphic>(true))
                if (!g.transform.IsChildOf(labelLayer)) g.enabled = false;
        }

        /// A move was pressed while KEEP / SWITCH is pending: shake the choice.
        public void NudgeChoice()
        {
            if (choice.gameObject.activeInHierarchy) StartCoroutine(Shake(choice));
        }

        /// Redraws from the state. <paramref name="text"/> is the stage's name, hint and story in the current language
        /// (LevelCatalog.TextOf), <paramref name="msg"/> the message line ("" = empty, MessagePresenter).
        public void Refresh(State s, int levelIdx, LevelTextDto text, string msg)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (bodyLook == null || loc == null) throw new InvalidOperationException("HudView.Refresh before Create");
            shownState = s;
            shownStage = levelIdx;
            shownText = text ?? throw new ArgumentNullException(nameof(text));
            shownMessage = msg ?? throw new ArgumentNullException(nameof(msg));
            title.text = text.name ?? "";
            title.transform.parent.gameObject.SetActive(title.text.Length > 0);

            RefreshGuide();

            RefreshBodies(s);
            message.text = msg;
            RefreshMessage();
            RefreshStatues(s);
        }

        /// The passing line (story-integration.md 0.2, the story plate in the left margin): the stage's start line or a
        /// planet's finale line for <paramref name="seconds"/> (unscaled), replacing one already up.
        public void ShowLine(string text, float seconds)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("empty line", nameof(text));
            if (!(seconds > 0)) throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "> 0");
            story.text = text;
            storyOn = true;
            storyUntil = Time.unscaledTime + seconds;
            RefreshGuide();
        }

        public void HideLine()
        {
            if (!storyOn) return;
            storyOn = false;
            RefreshGuide();
        }

        /// The passing line is up.
        public bool LineShown => storyOn;

        // ---------- KEEP / SWITCH cards (docs 4.4, 14.11) ----------

        /// What the choice cards show. Built by the choice controller from ChoicePreview and the tutorial state.
        public struct ChoiceCards
        {
            public bool On;
            /// The next brother (ChoicePreview.NextBody): border colour and the SWITCH picture.
            public int NextBody;
            /// Both result lines (the first 3 confirmed choices, SaveSystem.ExplainChoice); else only the focused card's.
            public bool Explain;
            /// The KEEP / SWITCH guide line above the cards (the first time a choice is asked).
            public bool Guide;
            public bool FocusKeep;
        }

        ChoiceCards shownCards;
        public ChoiceCards ShownCards => shownCards;

        public void ShowChoice(ChoiceCards c)
        {
            bool was = shownCards.On;
            shownCards = c;
            choice.gameObject.SetActive(c.On);
            chooseDim.gameObject.SetActive(c.On);
            // One keyboard confirm path (review R1): Enter belongs to the choice controller (focused card); a card left
            // selected by an earlier click would also Submit. Cleared whenever the cards appear or go.
            if (c.On != was) ClearSelection();
            if (!c.On) { RefreshMessage(); return; }
            var color = bodyLook.BodyColor(c.NextBody);
            foreach (var e in keepEdges) e.color = color;
            foreach (var e in switchEdges) e.color = color;
            Thick(keepEdges, c.FocusKeep ? 4f : 2f);
            Thick(switchEdges, c.FocusKeep ? 2f : 4f);
            keepPicture.sprite = stoneSprite;
            switchPicture.sprite = bodySprites[c.NextBody % bodySprites.Length];
            // First choices: both cards explain themselves. Later: compact cards (picture, name, badge) of equal height and
            // the focused card's result line on one plate under them (lead 2026-11-11: no empty card body).
            keepDesc.gameObject.SetActive(c.Explain);
            switchDesc.gameObject.SetActive(c.Explain);
            focusLine.transform.parent.gameObject.SetActive(!c.Explain);
            focusLine.text = c.FocusKeep ? keepDesc.text : switchDesc.text;
            choiceGuide.transform.parent.gameObject.SetActive(c.Guide);
            ArrangeChoice(vertical);
        }

        static void ClearSelection()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null) es.SetSelectedGameObject(null);
        }

        /// Puts the cards beside the board: <paramref name="board"/> = the projected board area (14.6) and
        /// <paramref name="brother"/> = the brother's head, both on this canvas with a top-left origin. Returns false when no
        /// candidate fits (then the cards sit in the right margin; UiLayoutCheck reports the overlap).
        public bool PlaceChoice(Rect board, Vector2 brother) => PlaceChoice(board, brother, ((RectTransform)transform).rect.size);

        /// <see cref="PlaceChoice(Rect, Vector2)"/> on a canvas of <paramref name="canvas"/> units (Edit Mode checks).
        public bool PlaceChoice(Rect board, Vector2 brother, Vector2 canvas)
        {

            // docs 14.11 (29ae00f): clear of the HUD plates shown right now (story / move guide, statues, message); the
            // always-on buttons and dots are outside every candidate already. Cards first: if only the message plate is in
            // the way, the message hides while the cards show and comes back with them gone.
            var avoid = new List<Rect>();
            foreach (var go in new[] { guidePanel, statues.transform.parent.gameObject })
                if (go.activeSelf) avoid.Add(CanvasRect((RectTransform)go.transform, canvas));
            var msgPlate = (RectTransform)message.transform.parent;
            bool msgOn = shownMessage.Length > 0;
            Rect msgRect = msgOn ? CanvasRect(msgPlate, canvas) : default;
            if (msgOn) avoid.Add(msgRect);
            var v = GroupSize(true); var h = GroupSize(false);
            var r = LayoutChoice(board, brother, canvas, v, h, avoid, out vertical, out bool fits);
            if (!fits && msgOn)
            {
                avoid.Remove(msgRect);
                r = LayoutChoice(board, brother, canvas, v, h, avoid, out vertical, out fits);
            }
            choice.anchorMin = choice.anchorMax = choice.pivot = new Vector2(0, 1);
            choice.anchoredPosition = new Vector2(r.x, -r.y);
            choice.sizeDelta = r.size;
            ArrangeChoice(vertical);
            choiceRect = r;
            placedCanvas = canvas;
            RefreshMessage();
            return fits;
        }

        Rect choiceRect;

        /// The message plate shows its line unless the choice cards are up and sit where it is (docs 14.11).
        void RefreshMessage()
        {
            var plate = (RectTransform)message.transform.parent;
            bool on = shownMessage.Length > 0;
            if (on && shownCards.On && CanvasRect(plate, placedCanvas).Overlaps(choiceRect)) on = false;
            plate.gameObject.SetActive(on);
        }

        /// <paramref name="r"/> (a plate placed directly on the HUD root with a point anchor) on a canvas of
        /// <paramref name="canvas"/> units, top-left origin, after its content size fitter ran. From the anchors, not world
        /// corners, so it is the same in the game and under UiLayoutCheck's canvas.
        static Rect CanvasRect(RectTransform r, Vector2 canvas)
        {
            bool was = r.gameObject.activeSelf;
            if (!was) r.gameObject.SetActive(true);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(r);
            var size = r.sizeDelta;
            float x = r.anchorMin.x * canvas.x + r.anchoredPosition.x - r.pivot.x * size.x;
            float yUp = r.anchorMin.y * canvas.y + r.anchoredPosition.y - r.pivot.y * size.y;
            if (!was) r.gameObject.SetActive(false);
            return new Rect(x, canvas.y - yUp - size.y, size.x, size.y);
        }

        /// The canvas size the cards were last placed on (RefreshMessage measures the message plate on the same).
        Vector2 placedCanvas = new Vector2(1280, 720);

        bool vertical = true;
        public const float CardW = 216, CardH = 170, CompactH = 72, CardGap = 12, GuideH = 84;
        /// Clear of the top band (menu, title: to 76), the statue plate on the right (to 132), the undo / restart buttons
        /// (bottom left, from 648) and the message plate (bottom centre, x 280..1000).
        const float TopLimit = 84, RightTopLimit = 140, Safe = 24, ButtonsTop = 636, ButtonsRight = 148;

        /// Choice group placement (docs 14.11): of the four sides of the board area, the candidate nearest to the brother that
        /// stays inside the safe area and clear of the HUD; left / right stack the cards, top / bottom put them side by side.
        public static Rect LayoutChoice(Rect board, Vector2 brother, Vector2 canvas, bool guide, out bool vertical, out bool fits)
        {
            float g = guide ? GuideH + CardGap : 0;
            return LayoutChoice(board, brother, canvas, new Vector2(CardW, 2 * CardH + CardGap + g), new Vector2(2 * CardW + CardGap, CardH + g),
                out vertical, out fits);
        }

        public static Rect LayoutChoice(Rect board, Vector2 brother, Vector2 canvas, Vector2 v, Vector2 h, out bool vertical, out bool fits) =>
            LayoutChoice(board, brother, canvas, v, h, null, out vertical, out fits);

        /// <param name="v">Group size stacked (left / right).</param> <param name="h">Group size side by side (top / bottom).</param>
        /// <param name="avoid">HUD plates shown now (canvas, top-left origin): a candidate crossing one (+ 8) is left out.</param>
        public static Rect LayoutChoice(Rect board, Vector2 brother, Vector2 canvas, Vector2 v, Vector2 h, IList<Rect> avoid,
            out bool vertical, out bool fits)
        {
            var candidates = new List<(Rect r, bool vert)>();
            {
                float x = board.xMin - CardGap - v.x, y = Mathf.Clamp(brother.y - v.y * 0.5f, TopLimit, ButtonsTop - v.y);
                if (x >= Safe && TopLimit <= ButtonsTop - v.y) candidates.Add((new Rect(x, y, v.x, v.y), true));
            }
            {
                float x = board.xMax + CardGap, y = Mathf.Clamp(brother.y - v.y * 0.5f, RightTopLimit, canvas.y - Safe - v.y);
                if (x + v.x <= canvas.x - Safe && RightTopLimit <= canvas.y - Safe - v.y) candidates.Add((new Rect(x, y, v.x, v.y), true));
            }
            {
                float y = board.yMin - CardGap - h.y, x = Mathf.Clamp(brother.x - h.x * 0.5f, Safe, canvas.x - Safe - h.x);
                if (y >= TopLimit) candidates.Add((new Rect(x, y, h.x, h.y), false));
            }
            {
                float y = board.yMax + CardGap, x = Mathf.Clamp(brother.x - h.x * 0.5f, ButtonsRight, canvas.x - Safe - h.x);
                if (y + h.y <= canvas.y - Safe) candidates.Add((new Rect(x, y, h.x, h.y), false));
            }
            if (avoid != null)
                candidates.RemoveAll(c =>
                {
                    var grown = new Rect(c.r.x - 8, c.r.y - 8, c.r.width + 16, c.r.height + 16);
                    foreach (var a in avoid) if (a.width > 0 && grown.Overlaps(a)) return true;
                    return false;
                });
            fits = candidates.Count > 0;
            if (!fits)
            {
                vertical = true;
                return new Rect(canvas.x - Safe - v.x, Mathf.Max(RightTopLimit, canvas.y * 0.5f - v.y * 0.5f), v.x, v.y);
            }
            var best = candidates[0];
            float bestD = float.MaxValue;
            foreach (var c in candidates)
            {
                var near = new Vector2(Mathf.Clamp(brother.x, c.r.xMin, c.r.xMax), Mathf.Clamp(brother.y, c.r.yMin, c.r.yMax));
                float d = Vector2.Distance(near, brother);
                if (d < bestD) { bestD = d; best = c; }
            }
            vertical = best.vert;
            return best.r;
        }

        /// Children of the choice group: the guide plate on top, then the cards stacked (vertical) or side by side.
        void ArrangeChoice(bool vert)
        {
            float g = shownCards.Guide ? GuideH + CardGap : 0, w = vert ? CardW : 2 * CardW + CardGap;
            float ch = shownCards.Explain ? CardH : CompactH;
            var guidePlate = (RectTransform)choiceGuide.transform.parent;
            TopLeft(guidePlate, 0, 0, w, GuideH);
            TopLeft((RectTransform)keep.transform, 0, g, CardW, ch);
            TopLeft((RectTransform)switchBody.transform, vert ? 0 : CardW + CardGap, vert ? g + ch + CardGap : g, CardW, ch);
            float cardsBottom = g + (vert ? 2 * ch + CardGap : ch);
            TopLeft((RectTransform)focusLine.transform.parent, 0, cardsBottom + CardGap, w, FocusLineH(w));
        }

        /// The focus plate's height at width <paramref name="w"/>: the taller of the two result lines (so it does not jump
        /// when the focus moves) + 12 inside above and below.
        float FocusLineH(float w)
        {
            var settings = focusLine.GetGenerationSettings(new Vector2(w - 24, 0));
            var gen = focusLine.cachedTextGeneratorForLayout;
            float ppu = focusLine.pixelsPerUnit > 0 ? focusLine.pixelsPerUnit : 1f;
            float a = gen.GetPreferredHeight(keepDesc.text, settings) / ppu, b = gen.GetPreferredHeight(switchDesc.text, settings) / ppu;
            return Mathf.Ceil(Mathf.Max(a, b)) + 24;
        }

        /// The whole choice group (guide, cards, focus plate) stacked or side by side, for the current cards.
        Vector2 GroupSize(bool vert)
        {
            float g = shownCards.Guide ? GuideH + CardGap : 0, w = vert ? CardW : 2 * CardW + CardGap;
            float ch = shownCards.Explain ? CardH : CompactH;
            float cards = vert ? 2 * ch + CardGap : ch;
            float line = shownCards.Explain ? 0 : CardGap + FocusLineH(w);
            return new Vector2(w, g + cards + line);
        }

        static void TopLeft(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
        }

        static void Thick(Image[] edges, float t)
        {
            // top, bottom, left, right
            edges[0].rectTransform.sizeDelta = new Vector2(edges[0].rectTransform.sizeDelta.x, t);
            edges[1].rectTransform.sizeDelta = new Vector2(edges[1].rectTransform.sizeDelta.x, t);
            edges[2].rectTransform.sizeDelta = new Vector2(t, edges[2].rectTransform.sizeDelta.y);
            edges[3].rectTransform.sizeDelta = new Vector2(t, edges[3].rectTransform.sizeDelta.y);
        }

        /// Pointer enter / select on a card focuses that choice (keyboard Tab and Enter go through the controller).
        void Focusable(Button b, bool isKeep)
        {
            b.navigation = new Navigation { mode = Navigation.Mode.None }; // Tab / Enter are the controller's (review R1)
            var trigger = b.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>() ?? b.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            foreach (var type in new[] { UnityEngine.EventSystems.EventTriggerType.PointerEnter, UnityEngine.EventSystems.EventTriggerType.Select })
            {
                var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
                entry.callback.AddListener(_ => ChoiceFocused?.Invoke(isKeep));
                trigger.triggers.Add(entry);
            }
        }

        void RefreshGuide()
        {
            story.gameObject.SetActive(storyOn);
            guide.gameObject.SetActive(guideOn);
            guidePanel.SetActive(storyOn || guideOn);
        }

        /// Body dot looks (design/art/ui-minimal.md "형제 점"): bright fill = walking, ring = waiting, grey diamond = statue,
        /// flat dim = spent (fell, bridged, vanished). Shape as well as colour, so colour alone never carries it.
        public enum DotLook { Walking, Waiting, Stone, Spent }

        /// The look of body <paramref name="k"/> in <paramref name="s"/> (pure; tests).
        public static DotLook LookOf(State s, int k, IList<int> statueOwners)
        {
            int playing = s.Level.Bodies - s.Bodies; // index of the body now playing; all spent after Lose (Bodies = 0)
            if (k == playing && s.Status != Status.Lose) return DotLook.Walking;
            if (k > playing) return DotLook.Waiting;
            return statueOwners.Contains(k) ? DotLook.Stone : DotLook.Spent;
        }

        void RefreshBodies(State s)
        {
            int count = s.Level.Bodies;
            while (dots.Count < count) dots.Add(CloneDot(dots[0], dots.Count));
            statueOwners.Clear();
            foreach (int cell in s.Statues) statueOwners.Add(BodyIndex.OfStatue(s, cell));
            for (int k = 0; k < dots.Count; k++)
            {
                var d = dots[k];
                d.root.gameObject.SetActive(k < count);
                if (k >= count) continue;
                var c = bodyLook.BodyColor(k);
                var look = LookOf(s, k, statueOwners);
                d.root.localRotation = look == DotLook.Stone ? Quaternion.Euler(0, 0, 45) : Quaternion.identity;
                d.root.localScale = look switch
                {
                    DotLook.Walking => new Vector3(1.35f, 1.35f, 1),
                    DotLook.Spent => new Vector3(1f, 0.5f, 1),
                    _ => Vector3.one,
                };
                var sprite = look == DotLook.Stone ? null : Ui.Circle;
                d.ring.sprite = d.core.sprite = sprite;
                d.ring.color = look switch
                {
                    DotLook.Stone => Ui.Stone,
                    DotLook.Spent => new Color(c.r, c.g, c.b, 0.45f),
                    _ => c,
                };
                d.core.color = look switch
                {
                    DotLook.Walking => c,
                    DotLook.Stone => Ui.Stone,
                    DotLook.Spent => new Color(c.r, c.g, c.b, 0.45f),
                    _ => new Color(Ui.PanelBg.r, Ui.PanelBg.g, Ui.PanelBg.b, 1f),
                };
            }
        }

        BodyDot CloneDot(BodyDot template, int index)
        {
            var go = Instantiate(template.root.gameObject, template.root.parent);
            go.name = $"Body {index + 1}";
            var r = (RectTransform)go.transform;
            return new BodyDot
            {
                root = r,
                ring = r.Find(template.ring.name).GetComponent<Image>(),
                core = r.Find(template.core.name).GetComponent<Image>(),
            };
        }

        /// "Statues 2 / 3" on stages with statue-count doors (Q). Values come from Rules.StatueCount and Level.StatueCount.
        void RefreshStatues(State s)
        {
            if (!ReferenceEquals(countLevel, s.Level))
            {
                countLevel = s.Level;
                levelHasCountDoor = false;
                for (int i = 0; i < s.Level.Count && !levelHasCountDoor; i++)
                    levelHasCountDoor = TileId.At(s, i).Kind == TileKind.CountDoor;
            }
            statues.transform.parent.gameObject.SetActive(levelHasCountDoor);
            if (!levelHasCountDoor) return;
            int now = Rules.StatueCount(s), need = s.Level.StatueCount;
            bool open = Rules.DoorOpen(s, 'Q');
            statues.text = loc.Format(open ? "hud.statues_open" : "hud.statues", $"<color={(open ? "#f2c14e" : "#ece6d6")}>{now}</color>", need);
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
    }
}
