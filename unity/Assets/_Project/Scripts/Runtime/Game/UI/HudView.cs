using System;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// Screen UI built from code: stage list, 7 step pips, bodies, message, buttons, KEEP/SWITCH choice, (the clear/loss panel is ResultView).
    /// Layout is in 1280x720 reference pixels. Same information as the web prototype's HUD.
    /// </summary>
    public sealed class HudView
    {
        public readonly RectTransform LabelLayer;

        readonly Text stageTitle, stageHint, nextText, message, statueText;
        Level countLevel;
        bool levelHasCountDoor;
        readonly Image[] pips = new Image[Rules.Steps];
        readonly Text[] pipTexts = new Text[Rules.Steps];
        readonly RectTransform bodiesRow;
        readonly List<Image> bodyIcons = new List<Image>();
        readonly Image[] levelButtons;
        readonly Text[] levelTexts;
        readonly GameObject choicePanel;
        readonly RectTransform choiceRect;

        /// Body colors for the HUD icons (TileSet.BodyColor, docs 12.6). Every chapter set uses the same three colors, so
        /// the set given at construction is enough (docs/scene-flow.md 4.7).
        readonly TileSet bodyLook;
        readonly MonoBehaviour coroutines;

        // View events (docs/scene-flow.md 4.7): the HUD only reports; controllers decide.
        /// Stage list buttons (0-based). Flow controller.
        public event Action<int> StageClicked;
        /// Footer "스테이지" button. Flow controller.
        public event Action StageSelectRequested;
        /// Footer "설정" button. Scene root (opens the settings overlay).
        public event Action SettingsRequested;
        /// Board actions. Board controller.
        public event Action UndoRequested, RestartRequested, HintRequested, KeepRequested, SwitchRequested;


        /// <param name="coroutines">Runs the KEEP/SWITCH shake (the HUD is not a MonoBehaviour).</param>
        public HudView(MonoBehaviour coroutines, TileSet bodyLook, int levelCount)
        {
            this.coroutines = coroutines != null ? coroutines : throw new ArgumentNullException(nameof(coroutines));
            this.bodyLook = bodyLook != null ? bodyLook : throw new ArgumentNullException(nameof(bodyLook));
            if (levelCount < 1) throw new ArgumentOutOfRangeException(nameof(levelCount), levelCount, "at least one stage");
            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.transform;

            // World-anchored labels (step numbers, gate digits) live behind the HUD.
            LabelLayer = Ui.Rect("WorldLabels", root).Stretch();

            // Header
            var title = Ui.Label(root, "일곱 걸음", 30, Ui.Ink, TextAnchor.UpperLeft, "Title");
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -12), new Vector2(200, 40));
            var sub = Ui.Label(root, "Seven Steps", 14, Ui.Dim, TextAnchor.UpperLeft, "Subtitle");
            sub.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(170, -26), new Vector2(160, 20));

            var levelRow = Row(root, "Levels", 4, TextAnchor.MiddleRight);
            levelRow.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -14), new Vector2(levelCount * 38, 34));
            levelButtons = new Image[levelCount];
            levelTexts = new Text[levelCount];
            for (int k = 0; k < levelCount; k++)
            {
                int idx = k;
                var b = Ui.MakeButton(levelRow, (k + 1).ToString(), 16, () => StageClicked?.Invoke(idx), out levelTexts[k], $"Level {k + 1}");
                ((RectTransform)b.transform).sizeDelta = new Vector2(34, 34);
                levelButtons[k] = b.GetComponent<Image>();
            }

            // Status row
            stageTitle = Ui.Label(root, "", 22, Ui.Ink, TextAnchor.MiddleLeft, "StageTitle");
            stageTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -64), new Vector2(360, 40));
            // The stage hint stays under the title for the whole stage (the bottom message changes with every step).
            stageHint = Ui.Label(root, "", 15, Ui.Dim, TextAnchor.UpperLeft, "StageHint");
            stageHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            // StageTitle spans y -64..-104 (top-left pivot), so the hint starts below it at -106.
            // 400 wide x 60 high = 3 wrapped lines at size 15, enough for the longest levels.js hint (~56 chars).
            stageHint.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -106), new Vector2(400, 60));

            var pipRow = Row(root, "Pips", 8, TextAnchor.MiddleCenter);
            pipRow.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(Rules.Steps * 48, 42));
            for (int k = 0; k < Rules.Steps; k++)
            {
                var pip = Ui.MakeImage(pipRow, Color.white, Ui.Circle, $"Pip {k + 1}");
                pip.rectTransform.sizeDelta = new Vector2(40, 40);
                pips[k] = pip;
                pipTexts[k] = Ui.Label(pip.transform, (k + 1).ToString(), 20, Ui.Ink);
                pipTexts[k].rectTransform.Stretch();
            }
            nextText = Ui.Label(root, "", 17, Ui.Dim, TextAnchor.MiddleCenter, "NextStep");
            nextText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -106), new Vector2(400, 24));

            bodiesRow = Row(root, "Bodies", 5, TextAnchor.MiddleRight);
            bodiesRow.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -66), new Vector2(300, 36));
            var bodiesLabel = Ui.Label(bodiesRow, "몸", 18, Ui.Dim);
            bodiesLabel.rectTransform.sizeDelta = new Vector2(26, 30);

            // Statue-count door stages: statues standing now / the N that opens the Q doors.
            statueText = Ui.Label(root, "", 18, Ui.Ink, TextAnchor.MiddleRight, "Statues");
            statueText.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -100), new Vector2(300, 28));

            // Message
            message = Ui.Label(root, "", 20, Ui.Ink, TextAnchor.MiddleCenter, "Message");
            message.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(1200, 30));

            // Footer buttons and key help
            var footer = Row(root, "Footer", 8, TextAnchor.MiddleLeft);
            footer.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 16), new Vector2(720, 40));
            FooterButton(footer, "되돌리기 Z", () => UndoRequested?.Invoke());
            FooterButton(footer, "재시작 R", () => RestartRequested?.Invoke());
            FooterButton(footer, "풀이 보기 H", () => HintRequested?.Invoke());
            FooterButton(footer, "스테이지 Esc", () => StageSelectRequested?.Invoke());
            FooterButton(footer, "설정", () => SettingsRequested?.Invoke());
            var keys = Ui.Label(root, "이동: 방향키 / WASD / 옆 칸 클릭 · 7걸음 후: K / X · 스테이지: [ ]", 14, Ui.Dim, TextAnchor.MiddleRight, "Keys");
            keys.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 24), new Vector2(520, 24));

            // KEEP / SWITCH
            choiceRect = Row(root, "Choice", 24, TextAnchor.MiddleCenter);
            choiceRect.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 108), new Vector2(640, 88));
            choicePanel = choiceRect.gameObject;
            ChoiceButton(choiceRect, "KEEP (K)", "여기에 석상을 남긴다\n다음 몸은 지금 출발점에서", Ui.Stone, () => KeepRequested?.Invoke());
            ChoiceButton(choiceRect, "SWITCH (X)", "석상을 남기지 않는다\n여기가 새 출발점이 된다", Ui.Gold, () => SwitchRequested?.Invoke());

        }

        /// A move was pressed while KEEP/SWITCH is pending: shake the choice buttons.
        public void NudgeChoice()
        {
            if (choicePanel.activeInHierarchy) coroutines.StartCoroutine(Shake(choiceRect));
        }

        static System.Collections.IEnumerator Shake(RectTransform r)
        {
            Vector2 home = r.anchoredPosition;
            for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                r.anchoredPosition = home + new Vector2(Mathf.Sin(t * 60f) * 8f * (1 - t / 0.3f), 0);
                yield return null;
            }
            r.anchoredPosition = home;
        }

        public void Refresh(State s, int levelIdx, int levelCount, string msg, ProgressData progress)
        {
            stageTitle.text = $"{levelIdx + 1}. {s.Level.Name}";
            stageHint.text = s.Level.Hint; // levels.js hint as written (level design); the solution (H) stays separate
            int next = s.Used + 1;
            for (int k = 0; k < Rules.Steps; k++)
            {
                int n = k + 1;
                bool used = n <= s.Used;
                bool isNext = n == next && s.Status == Status.Play;
                bool lucky = n == Rules.Steps;
                pips[k].color = used ? (lucky ? Ui.Gold : Ui.Dim) : isNext ? (lucky ? Ui.Gold : Ui.Ink) : new Color(1, 1, 1, 0.12f);
                pipTexts[k].color = used || isNext ? new Color32(0x22, 0x1f, 0x2b, 0xff) : lucky ? Ui.Gold : Ui.Ink;
            }
            nextText.text = s.Status == Status.Choose ? "<color=#f2c14e>KEEP</color> 또는 <color=#f2c14e>SWITCH</color>"
                : s.Status == Status.Play ? (next == Rules.Steps ? "다음 걸음이 <color=#f2c14e>행운의 7번째</color>" : $"다음 걸음: <color=#f2c14e>{next}번째</color>")
                : "";

            while (bodyIcons.Count < s.Level.Bodies)
            {
                var icon = Ui.MakeImage(bodiesRow, Ui.Ink, Ui.Circle, "Body");
                icon.rectTransform.sizeDelta = new Vector2(18, 18);
                bodyIcons.Add(icon);
            }
            // One icon per body in play order (A, B, C), in that body's color (TileSet.BodyColor, docs 12.6):
            // spent bodies faded, the body now playing larger, bodies still waiting at normal size.
            int spent = s.Level.Bodies - s.Bodies; // index of the body now playing; all spent after Lose (Bodies = 0)
            for (int k = 0; k < bodyIcons.Count; k++)
            {
                bodyIcons[k].gameObject.SetActive(k < s.Level.Bodies);
                var c = bodyLook.BodyColor(k);
                if (k < spent) c.a = 0.25f;
                bodyIcons[k].color = c;
                bodyIcons[k].rectTransform.sizeDelta = k == spent && s.Status != Status.Lose ? new Vector2(26, 26) : new Vector2(18, 18);
            }

            for (int k = 0; k < levelCount; k++)
            {
                bool cur = k == levelIdx;
                levelButtons[k].color = cur ? Ui.Gold : Ui.ButtonBg;
                levelTexts[k].color = cur ? new Color32(0x22, 0x22, 0x22, 0xff) : progress.IsCleared(k) ? Ui.Gold : Ui.Ink;
            }

            message.text = msg ?? throw new System.ArgumentNullException(nameof(msg)); // "" = empty line (MessagePresenter)
            choicePanel.SetActive(s.Status == Status.Choose);
            RefreshStatues(s);
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
            statueText.gameObject.SetActive(levelHasCountDoor);
            if (!levelHasCountDoor) return;
            int now = Rules.StatueCount(s), need = s.Level.StatueCount;
            bool open = Rules.DoorOpen(s, 'Q');
            statueText.text = $"석상 <color={(open ? "#f2c14e" : "#ece6d6")}>{now}</color> / {need}" + (open ? "  문 열림" : "");
        }

        static RectTransform Row(Transform parent, string name, float spacing, TextAnchor align)
        {
            var r = Ui.Rect(name, parent);
            var h = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            return r;
        }

        static void FooterButton(Transform parent, string label, System.Action onClick)
        {
            var b = Ui.MakeButton(parent, label, 16, onClick, out _, label);
            ((RectTransform)b.transform).sizeDelta = new Vector2(132, 40);
        }

        static void ChoiceButton(Transform parent, string title, string desc, Color accent, System.Action onClick)
        {
            var b = Ui.MakeButton(parent, title, 24, onClick, out var titleText, title);
            ((RectTransform)b.transform).sizeDelta = new Vector2(300, 88);
            titleText.color = accent;
            titleText.alignment = TextAnchor.UpperCenter;
            titleText.rectTransform.offsetMax = new Vector2(0, -8);
            var d = Ui.Label(b.transform, desc, 15, Ui.Dim, TextAnchor.LowerCenter, "Desc");
            d.rectTransform.Stretch();
            d.rectTransform.offsetMin = new Vector2(0, 8);
            d.lineSpacing = 1.1f;
        }
    }
}
