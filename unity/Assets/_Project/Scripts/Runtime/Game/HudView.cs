using System.Collections.Generic;
using SevenSteps.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game
{
    /// <summary>
    /// Screen UI built from code: stage list, 7 step pips, bodies, message, buttons, KEEP/SWITCH choice, clear/fail panel.
    /// Layout is in 1280x720 reference pixels. Same information as the web prototype's HUD.
    /// </summary>
    public sealed class HudView
    {
        public readonly RectTransform LabelLayer;

        readonly Text stageTitle, nextText, message;
        readonly Image[] pips = new Image[Rules.Steps];
        readonly Text[] pipTexts = new Text[Rules.Steps];
        readonly RectTransform bodiesRow;
        readonly List<Image> bodyIcons = new List<Image>();
        readonly Image[] levelButtons;
        readonly Text[] levelTexts;
        readonly GameObject choicePanel, overlay;
        readonly Text overlayTitle, overlayBody;
        readonly GameObject overlayNext, overlayUndo, overlayRestart;

        public HudView(GameController game, int levelCount)
        {
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
                var b = Ui.MakeButton(levelRow, (k + 1).ToString(), 16, () => game.LoadLevel(idx), out levelTexts[k], $"Level {k + 1}");
                ((RectTransform)b.transform).sizeDelta = new Vector2(34, 34);
                levelButtons[k] = b.GetComponent<Image>();
            }

            // Status row
            stageTitle = Ui.Label(root, "", 22, Ui.Ink, TextAnchor.MiddleLeft, "StageTitle");
            stageTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -64), new Vector2(360, 40));

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

            // Message
            message = Ui.Label(root, "", 20, Ui.Ink, TextAnchor.MiddleCenter, "Message");
            message.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(1200, 30));

            // Footer buttons and key help
            var footer = Row(root, "Footer", 8, TextAnchor.MiddleLeft);
            footer.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 16), new Vector2(460, 40));
            FooterButton(footer, "되돌리기 Z", () => game.Send(Command.Undo));
            FooterButton(footer, "재시작 R", () => game.Send(Command.Restart));
            FooterButton(footer, "풀이 보기 H", game.ShowHint);
            var keys = Ui.Label(root, "이동: 방향키 / WASD / 옆 칸 클릭 · 7걸음 후: K KEEP / X SWITCH · 스테이지: [ ]", 14, Ui.Dim, TextAnchor.MiddleRight, "Keys");
            keys.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 24), new Vector2(700, 24));

            // KEEP / SWITCH
            var choice = Row(root, "Choice", 24, TextAnchor.MiddleCenter);
            choice.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 108), new Vector2(640, 88));
            choicePanel = choice.gameObject;
            ChoiceButton(choice, "KEEP (K)", "여기에 석상을 남긴다\n다음 몸은 지금 출발점에서", Ui.Stone, () => game.Send(Command.Keep));
            ChoiceButton(choice, "SWITCH (X)", "석상을 남기지 않는다\n여기가 새 출발점이 된다", Ui.Gold, () => game.Send(Command.Switch));

            // Clear / fail overlay
            var dim = Ui.MakeImage(root, new Color(0, 0, 0, 0.55f), null, "Overlay");
            dim.rectTransform.Stretch();
            dim.raycastTarget = true;
            overlay = dim.gameObject;
            var panel = Ui.MakeImage(dim.transform, Ui.PanelBg, null, "Panel");
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 300));
            overlayTitle = Ui.Label(panel.transform, "", 40, Ui.Gold, TextAnchor.MiddleCenter, "Title");
            overlayTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(420, 50));
            overlayBody = Ui.Label(panel.transform, "", 20, Ui.Ink, TextAnchor.MiddleCenter, "Body");
            overlayBody.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(420, 50));
            var col = Ui.Rect("Buttons", panel.transform);
            col.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(300, 140));
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10;
            v.childAlignment = TextAnchor.LowerCenter;
            v.childControlWidth = v.childControlHeight = false;
            v.childForceExpandWidth = v.childForceExpandHeight = false;
            overlayNext = OverlayButton(col, "다음 스테이지 (Enter)", game.NextLevel);
            overlayUndo = OverlayButton(col, "한 걸음 되돌리기 (Z)", () => game.Send(Command.Undo));
            overlayRestart = OverlayButton(col, "다시 하기 (R)", () => game.Send(Command.Restart));
            overlay.SetActive(false);
        }

        public void Refresh(State s, int levelIdx, int levelCount, string msg, ICollection<int> cleared)
        {
            stageTitle.text = $"{levelIdx + 1}. {s.Level.Name}";
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
            for (int k = 0; k < bodyIcons.Count; k++)
            {
                bodyIcons[k].gameObject.SetActive(k < s.Level.Bodies);
                bodyIcons[k].color = k < s.Bodies ? Ui.Ink : new Color(1, 1, 1, 0.15f);
            }

            for (int k = 0; k < levelCount; k++)
            {
                bool cur = k == levelIdx;
                levelButtons[k].color = cur ? Ui.Gold : Ui.ButtonBg;
                levelTexts[k].color = cur ? new Color32(0x22, 0x22, 0x22, 0xff) : cleared.Contains(k) ? Ui.Gold : Ui.Ink;
            }

            message.text = msg ?? "";
            choicePanel.SetActive(s.Status == Status.Choose);

            bool last = levelIdx == levelCount - 1;
            overlay.SetActive(s.Status == Status.Win || s.Status == Status.Lose);
            if (s.Status == Status.Win)
            {
                overlayTitle.text = "클리어";
                overlayBody.text = $"몸 {s.Level.Bodies - s.Bodies + 1}개를 썼다." + (last ? "\n모든 스테이지를 끝냈다. 고맙습니다!" : "");
                overlayNext.SetActive(!last);
                overlayUndo.SetActive(false);
                overlayRestart.SetActive(true);
            }
            else if (s.Status == Status.Lose)
            {
                overlayTitle.text = "몸을 모두 잃었다";
                overlayBody.text = "되돌리거나 처음부터 다시.";
                overlayNext.SetActive(false);
                overlayUndo.SetActive(true);
                overlayRestart.SetActive(true);
            }
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
            ((RectTransform)b.transform).sizeDelta = new Vector2(140, 40);
        }

        static GameObject OverlayButton(Transform parent, string label, System.Action onClick)
        {
            var b = Ui.MakeButton(parent, label, 18, onClick, out _, label);
            ((RectTransform)b.transform).sizeDelta = new Vector2(300, 40);
            return b.gameObject;
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
