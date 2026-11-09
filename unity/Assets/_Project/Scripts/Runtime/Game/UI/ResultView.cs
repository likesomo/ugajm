using System;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// What the result panel shows (docs/scene-flow.md 4.7). Read-only; the constructor checks the ranges.
    /// A clear (AppState.Result) and a loss (board status Lose, AppState stays Game) use the same panel.
    /// </summary>
    public sealed class ResultModel
    {
        public int Stage { get; }
        public bool Cleared { get; }
        /// Bodies the level gives (levels.json bodies). The other body counts are checked against it.
        public int LevelBodies { get; }
        /// Bodies used for the clear, 1..LevelBodies. 0 for a loss.
        public int BodiesUsed { get; }
        /// Fewest bodies the solver needs, 0 = unknown.
        public int MinBodies { get; }
        /// Player's best for this stage, 0 = no record.
        public int BestBodies { get; }
        public bool IsLast { get; }

        public ResultModel(int stage, bool cleared, int levelBodies, int bodiesUsed, int minBodies, int bestBodies, bool isLast)
        {
            if (stage < 0) throw new ArgumentOutOfRangeException(nameof(stage), stage, "0-based stage");
            if (levelBodies < 1) throw new ArgumentOutOfRangeException(nameof(levelBodies), levelBodies, "at least one body");
            if (cleared && (bodiesUsed < 1 || bodiesUsed > levelBodies))
                throw new ArgumentOutOfRangeException(nameof(bodiesUsed), bodiesUsed, $"a clear uses 1..{levelBodies} bodies");
            if (!cleared && bodiesUsed != 0) throw new ArgumentOutOfRangeException(nameof(bodiesUsed), bodiesUsed, "a loss has no body count");
            if (minBodies < 0 || minBodies > levelBodies) throw new ArgumentOutOfRangeException(nameof(minBodies), minBodies, $"0 (unknown) or 1..{levelBodies}");
            if (bestBodies < 0 || bestBodies > levelBodies) throw new ArgumentOutOfRangeException(nameof(bestBodies), bestBodies, $"0 (no record) or 1..{levelBodies}");
            Stage = stage;
            Cleared = cleared;
            LevelBodies = levelBodies;
            BodiesUsed = bodiesUsed;
            MinBodies = minBodies;
            BestBodies = bestBodies;
            IsLast = isLast;
        }

        public static ResultModel Clear(int stage, int levelBodies, int bodiesUsed, int minBodies, int bestBodies, bool isLast) =>
            new ResultModel(stage, true, levelBodies, bodiesUsed, minBodies, bestBodies, isLast);

        public static ResultModel Loss(int stage, int levelBodies, bool isLast) =>
            new ResultModel(stage, false, levelBodies, 0, 0, 0, isLast);
    }

    /// <summary>
    /// Clear / loss panel (view only, docs/scene-flow.md 4.7). Shows a <see cref="ResultModel"/> and raises button events;
    /// the flow controller decides what they do. Built from code on its own canvas above the HUD (S6 moves it to a prefab).
    /// </summary>
    public sealed class ResultView
    {
        readonly GameObject root;
        readonly Text title, body;
        readonly GameObject next, again, select, undo, restart;

        public event Action NextRequested, RetryRequested, StageSelectRequested;
        /// Loss only: the board actions stay available on the panel (Z / R also work, the loss is not an overlay).
        public event Action UndoRequested, RestartRequested;

        public bool IsShown => root.activeSelf;

        public ResultView()
        {
            var canvasGo = new GameObject("Result", typeof(RectTransform));
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // above the HUD canvas (0)
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var dim = Ui.MakeImage(canvasGo.transform, new Color(0, 0, 0, 0.55f), null, "Overlay");
            dim.rectTransform.Stretch();
            dim.raycastTarget = true;
            root = dim.gameObject;
            var panel = Ui.MakeImage(dim.transform, Ui.PanelBg, null, "Panel");
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 330));
            title = Ui.Label(panel.transform, "", 40, Ui.Gold, TextAnchor.MiddleCenter, "Title");
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(420, 50));
            body = Ui.Label(panel.transform, "", 20, Ui.Ink, TextAnchor.MiddleCenter, "Body");
            body.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(420, 50));
            var col = Ui.Rect("Buttons", panel.transform);
            col.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(300, 150));
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10;
            v.childAlignment = TextAnchor.LowerCenter;
            v.childControlWidth = v.childControlHeight = false;
            v.childForceExpandWidth = v.childForceExpandHeight = false;
            next = Button(col, "다음 스테이지 (Enter)", () => NextRequested?.Invoke());
            again = Button(col, "다시 하기 (R)", () => RetryRequested?.Invoke());
            select = Button(col, "스테이지 선택 (Esc)", () => StageSelectRequested?.Invoke());
            undo = Button(col, "한 걸음 되돌리기 (Z)", () => UndoRequested?.Invoke());
            restart = Button(col, "다시 시작 (R)", () => RestartRequested?.Invoke());
            root.SetActive(false);
        }

        public void Show(ResultModel m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (m.Cleared)
            {
                // "몸 2개 (최소 1개 · 최고 기록 2개)". Minimum = JS solver's expected.minBodies.
                string detail = m.MinBodies > 0 ? $"최소 {m.MinBodies}개" : "";
                if (m.BestBodies > 0) detail += (detail.Length > 0 ? " · " : "") + $"최고 기록 {m.BestBodies}개";
                title.text = "클리어";
                body.text = $"몸 {m.BodiesUsed}개" + (detail.Length > 0 ? $" ({detail})" : "") + (m.IsLast ? "\n모든 스테이지를 끝냈다. 고맙습니다!" : "");
            }
            else
            {
                title.text = "몸을 모두 잃었다";
                body.text = "되돌리거나 처음부터 다시.";
            }
            next.SetActive(m.Cleared && !m.IsLast);
            again.SetActive(m.Cleared);
            select.SetActive(true);
            undo.SetActive(!m.Cleared);
            restart.SetActive(!m.Cleared);
            root.SetActive(true);
        }

        public void Hide() => root.SetActive(false);

        static GameObject Button(Transform parent, string label, Action onClick)
        {
            var b = Ui.MakeButton(parent, label, 18, onClick, out _, label);
            ((RectTransform)b.transform).sizeDelta = new Vector2(300, 40);
            return b.gameObject;
        }
    }
}
