using SevenSteps.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace SevenSteps.Art.Review
{
    public sealed class ChoicePanelView
    {
        readonly GameObject[] targets = new GameObject[2];
        readonly ReviewChoiceController v; public Rect Bounds
        {
            get
            {
                var board = v.boardRect;
                bool right = 1280 - board.xMax >= board.xMin;
                float x = right ? board.xMax + 48 : board.xMin - 438;
                float y = Mathf.Clamp(v.Cell(v.Session.State.Pos).y - 155, 220, 265);
                return new Rect(x, y, 390, 310);
            }
        }
        public ChoicePanelView(ReviewChoiceController view)
        {
            v = view;
        }
        public void Hide()
        {
            foreach (var target in targets)
            if (target)
                target.SetActive(false);
        }
        GameObject Target(int index, bool keep, Rect rect)
        {
            var go = targets[index];
            if (!go)
            {
                go = v.Box(keep ? "Keep choice" : "Switch choice", rect, v.Next);
                v.drawn.Remove(go);
                targets[index] = go;
                go.GetComponent<Image>().raycastTarget = true;
                var button = go.AddComponent<Button>();
                button.onClick.AddListener(() => v.Choose(keep));
                var trigger = go.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => v.Focus(keep));
                trigger.triggers.Add(enter);
            }
            go.SetActive(true);
            go.transform.SetAsLastSibling();
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = rect.position;
            rt.sizeDelta = rect.size;
            go.GetComponent<Image>().color = v.Next;
            return go;
        }
        public void Draw(State s)
        {
            var bounds = Bounds;
            float x = bounds.x;
            v.Label(v.language == "ko" ? "다음 형제에게 어떻게 이을까요?" : "How will you pass the baton?", new Rect(x, bounds.y + 289, 405, 50), 24);
            for (int i = 0; i < 2; i++)
            {
                bool keep = i == 0;
                float y = bounds.y + (keep ? 144 : 0);
                bool focus = v.focusKeep == keep;
                Target(i, keep, new Rect(x, y, 390, 132));
                v.Box("Panel fill", new Rect(x + 4, y + 4, 382, 124), focus ? new Color(.19f, .29f, .32f) : new Color(.10f, .17f, .20f));
                v.Box("Picture body", new Rect(x + 23, y + 41, 26, 48), keep ? new Color(.57f, .61f, .56f) : v.Next);
                v.Star(new Vector2(x + 37, y + 95), 15, keep ? v.Ink : v.Next);
                if (!keep)
                {
                    v.Line(new Vector2(x + 17, y + 60), new Vector2(x + 57, y + 86), v.Next, 6);
                    v.Line(new Vector2(x + 17, y + 86), new Vector2(x + 57, y + 60), v.Next, 6);
                }
                v.Label(v.language == "ko" ? (keep ? "남기기 KEEP" : "넘기기 SWITCH") : (keep ? "Keep" : "Switch"), new Rect(x + 74, y + 70, 252, 45), 25);
                v.Box("Key badge", new Rect(x + 335, y + 83, 36, 32), v.Next);
                v.Label(keep ? "K" : "X", new Rect(x + 343, y + 82, 32, 34), 22, new Color(.06f, .12f, .15f));
                if (v.encounters < 3 || focus)
                    v.Label(v.language == "ko" ? (keep ? "선돌을 남기고 출발점에서 깨어나요" : "이 칸에서 다음 형제가 깨어나요") : (keep ? "Leave stone; wake at the start" : "Next sibling wakes here"), new Rect(x + 74, y + 17, 304, 48), 18);

            }
        }
    }
}
