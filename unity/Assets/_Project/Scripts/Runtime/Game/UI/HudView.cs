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
    /// Game HUD (view only, docs/scene-flow.md 4.7, S6 pattern). Prefab UI/Prefabs/Hud.prefab (its own canvas), referenced by
    /// UiTheme.hud; first built by Seven Steps > Gameplay > Build HUD (HudBuilder), then edited as a prefab.
    /// Layout follows the art UI kit (design/art/ui-theme.md, game-hud-v2): stage box and hint top left, the Big Dipper step
    /// counter top centre (stars 1-6 light up; the lines turn gold only when the 7th step completes), the three travellers
    /// top right (state written out: 사용 중 / 남음 / 선돌, not by colour alone), message, KEEP/SWITCH and the footer.
    /// Reads State only; raises button events, controllers decide.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [Header("Board labels (step numbers, gate digits) are placed here by BoardView")]
        public RectTransform labelLayer;

        [Header("Stage")]
        public Text stageNumber;
        public Text stageName, stageHint;

        [Header("Big Dipper step counter: index 0..6 = step 1..7")]
        public Image[] stars = new Image[Rules.Steps];
        public Text[] starNumbers = new Text[Rules.Steps];
        [Tooltip("Lines 1-2, 2-3, 3-4, 4-1 (bowl), 4-5, 5-6, 6-7 (handle)")]
        public Image[] lines = new Image[Rules.Steps];
        public Sprite starEmpty, starFilled;
        public Color lineDim = new Color(0.79f, 0.67f, 0.42f, 0.35f);
        public Color lineGold = new Color(0.95f, 0.78f, 0.36f, 1f);
        public Text nextStep;

        [Header("Travellers (one slot per body; extra bodies clone the first slot)")]
        public BodySlot[] bodySlots = new BodySlot[3];
        [Tooltip("Body A, B, C portraits (cycled for more bodies)")]
        public Sprite[] bodySprites = new Sprite[3];
        public Sprite bodyStone;

        [Header("Messages and choice")]
        public Text statues;
        public Text message;
        public RectTransform choice;
        public Button keep, switchBody;

        [Header("Footer")]
        public Button undo;
        public Button restart, hint, stageSelect, settings;

        // View events (docs/scene-flow.md 4.7): the HUD only reports; controllers decide.
        /// Footer "스테이지" button. Flow controller.
        public event Action StageSelectRequested;
        /// Footer "설정" button. Scene root (opens the settings overlay).
        public event Action SettingsRequested;
        /// Board actions. Board controller.
        public event Action UndoRequested, RestartRequested, HintRequested, KeepRequested, SwitchRequested;

        static readonly string[] TravellerNames = { "청록 여행자", "황토 여행자", "적갈 여행자" };
        const string Playing = "사용 중", Waiting = "남음", Stone = "선돌", Spent = "다 씀";

        /// Body colors (TileSet.BodyColor, docs 12.6); the same three in every chapter set, so the one from Create is enough.
        TileSet bodyLook;
        readonly List<BodySlot> slots = new List<BodySlot>();
        readonly List<int> statueOwners = new List<int>();
        Level countLevel;
        bool levelHasCountDoor, dipperComplete, wired;

        public RectTransform LabelLayer => labelLayer;

        [Serializable]
        public sealed class BodySlot
        {
            public GameObject root;
            public Image portrait;
            public Text name, state;
            public Image stateBg;
        }

        /// Places the theme's HUD prefab (its own canvas) in the current scene. LabelLayer is valid on return.
        public static HudView Create(TileSet bodyLook)
        {
            if (bodyLook == null) throw new ArgumentNullException(nameof(bodyLook));
            var prefab = Ui.Theme.hud != null ? Ui.Theme.hud
                : throw new MissingReferenceException($"[SevenSteps] UiTheme {Ui.Theme.name}: hud is not set (Seven Steps > Gameplay > Build HUD)");
            var view = Instantiate(prefab);
            view.name = prefab.name;
            view.bodyLook = bodyLook;
            view.Wire();
            return view;
        }

        /// Required references missing in the prefab, one line each (OnValidate, Wire, SceneFlowValidator via UiTheme).
        public void Validate(List<string> issues)
        {
            void Need(Object o, string field) { if (o == null) issues.Add($"HudView {name}: {field} is not set"); }
            Need(labelLayer, nameof(labelLayer));
            Need(stageNumber, nameof(stageNumber)); Need(stageName, nameof(stageName)); Need(stageHint, nameof(stageHint));
            if (stars == null || stars.Length != Rules.Steps) issues.Add($"HudView {name}: stars needs {Rules.Steps} entries");
            else for (int k = 0; k < Rules.Steps; k++) Need(stars[k], $"stars[{k}]");
            if (starNumbers == null || starNumbers.Length != Rules.Steps) issues.Add($"HudView {name}: starNumbers needs {Rules.Steps} entries");
            else for (int k = 0; k < Rules.Steps; k++) Need(starNumbers[k], $"starNumbers[{k}]");
            if (lines == null || lines.Length != Rules.Steps) issues.Add($"HudView {name}: lines needs {Rules.Steps} entries (1-2, 2-3, 3-4, 4-1, 4-5, 5-6, 6-7)");
            else for (int k = 0; k < Rules.Steps; k++) Need(lines[k], $"lines[{k}]");
            Need(starEmpty, nameof(starEmpty)); Need(starFilled, nameof(starFilled)); Need(nextStep, nameof(nextStep));
            if (bodySlots == null || bodySlots.Length == 0) issues.Add($"HudView {name}: bodySlots needs at least one slot");
            else for (int k = 0; k < bodySlots.Length; k++)
            {
                var b = bodySlots[k];
                if (b == null) { issues.Add($"HudView {name}: bodySlots[{k}] is not set"); continue; }
                Need(b.root, $"bodySlots[{k}].root"); Need(b.portrait, $"bodySlots[{k}].portrait");
                Need(b.name, $"bodySlots[{k}].name"); Need(b.state, $"bodySlots[{k}].state"); Need(b.stateBg, $"bodySlots[{k}].stateBg");
            }
            if (bodySprites == null || bodySprites.Length == 0) issues.Add($"HudView {name}: bodySprites needs at least one sprite");
            else for (int k = 0; k < bodySprites.Length; k++) Need(bodySprites[k], $"bodySprites[{k}]");
            Need(bodyStone, nameof(bodyStone));
            Need(statues, nameof(statues)); Need(message, nameof(message)); Need(choice, nameof(choice));
            Need(keep, nameof(keep)); Need(switchBody, nameof(switchBody));
            Need(undo, nameof(undo)); Need(restart, nameof(restart)); Need(hint, nameof(hint));
            Need(stageSelect, nameof(stageSelect)); Need(settings, nameof(settings));
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
            undo.onClick.AddListener(() => UndoRequested?.Invoke());
            restart.onClick.AddListener(() => RestartRequested?.Invoke());
            hint.onClick.AddListener(() => HintRequested?.Invoke());
            stageSelect.onClick.AddListener(() => StageSelectRequested?.Invoke());
            settings.onClick.AddListener(() => SettingsRequested?.Invoke());
            keep.onClick.AddListener(() => KeepRequested?.Invoke());
            switchBody.onClick.AddListener(() => SwitchRequested?.Invoke());
            slots.AddRange(bodySlots);
            wired = true;
        }

        /// A move was pressed while KEEP/SWITCH is pending: shake the choice buttons.
        public void NudgeChoice()
        {
            if (choice.gameObject.activeInHierarchy) StartCoroutine(Shake(choice));
        }

        /// Redraws from the state. <paramref name="msg"/> is the message line ("" = empty, MessagePresenter).
        public void Refresh(State s, int levelIdx, string msg)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (bodyLook == null) throw new InvalidOperationException("HudView.Refresh before Create");
            stageNumber.text = $"스테이지 {levelIdx + 1:00}";
            stageName.text = s.Level.Name;
            stageHint.text = s.Level.Hint; // levels.js hint as written (level design); the solution (H) is the message line
            RefreshDipper(s);
            RefreshBodies(s);
            message.text = msg ?? throw new ArgumentNullException(nameof(msg));
            choice.gameObject.SetActive(s.Status == Status.Choose);
            RefreshStatues(s);
        }

        /// Stars 1..Used filled, the next one larger; the 7th step (choice or clear) turns the lines gold with a short flash.
        void RefreshDipper(State s)
        {
            int next = s.Used + 1;
            bool complete = s.Used >= Rules.Steps;
            for (int k = 0; k < Rules.Steps; k++)
            {
                int n = k + 1;
                bool lit = n <= s.Used;
                bool isNext = n == next && s.Status == Status.Play;
                stars[k].sprite = lit ? starFilled : starEmpty;
                stars[k].rectTransform.localScale = Vector3.one * (isNext ? 1.25f : 1f);
                starNumbers[k].color = lit || isNext ? Ui.Gold : Ui.Dim;
            }
            foreach (var l in lines) l.color = complete ? lineGold : lineDim;
            if (complete && !dipperComplete && isActiveAndEnabled) StartCoroutine(Flash());
            dipperComplete = complete;

            nextStep.text = s.Status == Status.Choose ? "KEEP 또는 SWITCH"
                : s.Status == Status.Play ? (next == Rules.Steps ? "다음 걸음 7 · 행운" : $"다음 걸음 {next}")
                : "";
        }

        /// One slot per body in play order (A, B, C). State in words, portrait in the body's look, KEEP statues as 선돌.
        void RefreshBodies(State s)
        {
            int count = s.Level.Bodies;
            while (slots.Count < count) slots.Add(CloneSlot(slots[0], slots.Count));
            int playing = s.Level.Bodies - s.Bodies; // index of the body now playing; all spent after Lose (Bodies = 0)

            // Owners of the statues standing now (walks the Undo chain; Refresh runs once per state change, docs 12.6).
            statueOwners.Clear();
            foreach (int cell in s.Statues) statueOwners.Add(BodyIndex.OfStatue(s, cell));

            for (int k = 0; k < slots.Count; k++)
            {
                var slot = slots[k];
                slot.root.SetActive(k < count);
                if (k >= count) continue;
                var color = bodyLook.BodyColor(k);
                slot.name.text = TravellerNames[k % TravellerNames.Length];
                bool current = k == playing && s.Status != Status.Lose;
                bool stone = k < playing && statueOwners.Contains(k);
                slot.state.text = current ? Playing : k > playing ? Waiting : stone ? Stone : Spent;
                slot.portrait.sprite = stone ? bodyStone : bodySprites[k % bodySprites.Length];
                slot.portrait.color = k < playing && !stone ? new Color(1, 1, 1, 0.35f) : Color.white;
                slot.stateBg.color = current ? color : new Color(color.r, color.g, color.b, 0.25f);
                slot.state.color = current ? Ui.Ink : Ui.Dim;
            }
        }

        BodySlot CloneSlot(BodySlot template, int index)
        {
            var go = Instantiate(template.root, template.root.transform.parent);
            go.name = $"Body {index + 1}";
            // Same child paths as the template.
            Transform Find(Component c) => go.transform.Find(PathFrom(template.root.transform, c.transform));
            return new BodySlot
            {
                root = go,
                portrait = Find(template.portrait).GetComponent<Image>(),
                name = Find(template.name).GetComponent<Text>(),
                state = Find(template.state).GetComponent<Text>(),
                stateBg = Find(template.stateBg).GetComponent<Image>(),
            };
        }

        static string PathFrom(Transform root, Transform t)
        {
            if (t == root) throw new ArgumentException("body slot parts must be children of the slot root");
            string path = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        /// "석상 2 / 3" on stages with statue-count doors (Q). Values come from Rules.StatueCount and Level.StatueCount.
        void RefreshStatues(State s)
        {
            if (!ReferenceEquals(countLevel, s.Level))
            {
                countLevel = s.Level;
                levelHasCountDoor = false;
                for (int i = 0; i < s.Level.Count && !levelHasCountDoor; i++)
                    levelHasCountDoor = TileId.At(s, i).Kind == TileKind.CountDoor;
            }
            statues.gameObject.SetActive(levelHasCountDoor);
            if (!levelHasCountDoor) return;
            int now = Rules.StatueCount(s), need = s.Level.StatueCount;
            bool open = Rules.DoorOpen(s, 'Q');
            statues.text = $"석상 <color={(open ? "#f2c14e" : "#ece6d6")}>{now}</color> / {need}" + (open ? "  문 열림" : "");
        }

        /// 7th step: the lines and the 7th star pop once.
        IEnumerator Flash()
        {
            var seventh = stars[Rules.Steps - 1].rectTransform;
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Sin(t / 0.35f * Mathf.PI);
                seventh.localScale = Vector3.one * (1f + 0.5f * k);
                foreach (var l in lines) l.color = Color.Lerp(lineGold, Color.white, k * 0.6f);
                yield return null;
            }
            seventh.localScale = Vector3.one;
            foreach (var l in lines) l.color = dipperComplete ? lineGold : lineDim;
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
