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
    /// guide (controls, shown by the flow on a first play) and the stage story (on entering a stage, until the first move
    /// or 3 s). Steps are read from the star trail and the stars at the brother's feet, not from numbers; the old numeric
    /// footprints come back with <see cref="StepNumbers"/> (accessibility, off by default).
    /// Prefab UI/Prefabs/Hud.prefab (its own canvas), referenced by UiTheme.hud, built by Seven Steps > Gameplay > Build HUD
    /// (HudBuilder). Reads State only and raises button events; controllers decide. Every word comes from the string table
    /// (hud.*, docs/architecture.md 13.2) in the role fonts of the current language; <see cref="Apply"/> redraws on a switch.
    /// </summary>
    public sealed class HudView : MonoBehaviour
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

        [Header("KEEP / SWITCH (Status.Choose only)")]
        public RectTransform choice;
        public Button keep, switchBody;
        [Tooltip("One-line result under each choice's name")]
        public Text keepDesc, switchDesc;

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

        /// Seconds the stage story stays after entering a stage when no move is made (design/art/ui-minimal.md).
        public const float StorySeconds = 3f;

        Localizer loc;
        State shownState;
        int shownStage = -1;
        LevelTextDto shownText;
        string shownMessage = "";
        bool guideOn, storyOn, stepNumbers;
        float storyUntil;
        int storyStage = -1;

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

            // The story: once per stage entry (not again on restart), until the first move or StorySeconds.
            story.text = text.story ?? "";
            if (levelIdx != storyStage)
            {
                storyStage = levelIdx;
                storyOn = story.text.Length > 0;
                storyUntil = Time.unscaledTime + StorySeconds;
            }
            if (s.Prev != null) storyOn = false;
            RefreshGuide();

            RefreshBodies(s);
            message.text = msg;
            message.transform.parent.gameObject.SetActive(msg.Length > 0);
            choice.gameObject.SetActive(s.Status == Status.Choose);
            RefreshStatues(s);
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
