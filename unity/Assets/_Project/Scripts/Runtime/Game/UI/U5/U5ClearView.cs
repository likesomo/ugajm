using System;
using System.Collections;
using System.Collections.Generic;
using SevenSteps.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.U5
{
    /// <summary>
    /// U5 clear (docs/ui-u5-kit.md 3, U5-result): the board stays as it is, both HUDs hide (<see cref="HudHidden"/>), three
    /// star stones stand over the brother on the exit (lit = earned, what each asks on hover / focus / hold), Next (56) sits
    /// beside the board next to the exit, Map at the top left and Retry at the top right; the planet / journey line on a
    /// plate at the bottom centre when this clear ends one. A loss still goes to the current panel (<see cref="Flow.IResultPanel"/>
    /// fallback) until the U5 failure step. View only: events, the flow controller decides.
    /// </summary>
    public sealed class U5ClearView : MonoBehaviour, Flow.IResultPanel
    {
        public const float StoneSize = 40, StoneGap = 8, StoneLift = 36, NextSize = 56, NextGap = 16, Safe = 24;
        public const int SortingOrder = 6;

        IconButton map, retry, next;
        readonly IconButton[] stones = new IconButton[Core.Stars.Max];
        Image[] stoneStars;
        RectTransform stoneRow;
        Image finalePlate;
        Text finaleText;
        FocusPlate plate;
        Localizer loc;
        UiIconSet icons;
        Flow.IResultPanel lossPanel;
        ResultModel shown;
        Coroutine reveal;

        public event Action NextRequested, RetryRequested, StageSelectRequested;
        /// true while a clear is up (both HUDs hide), false when it goes.
        public event Action<bool> HudHidden;

        /// The brother on the exit (his head), canvas units, top-left origin (BoardView.BrotherOnCanvas after the win).
        public Func<Vector2> ExitPoint;
        /// The board area (BoardView.ProjectedArea): Next goes beside it.
        public Func<Rect> BoardArea;

        public IconButton Map => map;
        public IconButton Retry => retry;
        public IconButton Next => next;
        public IReadOnlyList<IconButton> Stones => stones;
        public bool IsShown => gameObject.activeSelf && shown != null;
        public FocusPlate Plate => plate;
        /// Earned stones drawn lit right now (the reveal fills them in order).
        public int LitStones { get; private set; }

        /// Builds the clear on its own canvas (Edit Mode too). <paramref name="lossPanel"/> shows losses (the current panel).
        public static U5ClearView Build(UiIconSet icons, Localizer loc, Flow.IResultPanel lossPanel)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            if (lossPanel == null || (lossPanel is UnityEngine.Object o && o == null)) throw new ArgumentNullException(nameof(lossPanel));
            var go = new GameObject("U5 Clear", typeof(RectTransform)) { layer = 5 };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var v = go.AddComponent<U5ClearView>();
            v.icons = icons;
            v.loc = loc;
            v.lossPanel = lossPanel;
            v.Assemble();
            go.SetActive(false);
            return v;
        }

        void Assemble()
        {
            var root = transform;
            map = IconButton.Build(root, UiIcon.Map, icons, "Esc", () => StageSelectRequested?.Invoke(), name: "Map");
            map.Rect.TopLeft(Safe, Safe, IconButton.Size, IconButton.Size);
            retry = IconButton.Build(root, UiIcon.Retry, icons, "R", () => RetryRequested?.Invoke(), name: "Retry");
            retry.Rect.anchorMin = retry.Rect.anchorMax = retry.Rect.pivot = new Vector2(1, 1);
            retry.Rect.anchoredPosition = new Vector2(-Safe, -Safe);
            retry.Rect.sizeDelta = new Vector2(IconButton.Size, IconButton.Size);
            next = IconButton.Build(root, UiIcon.Next, icons, "Enter", () => NextRequested?.Invoke(), name: "Next", size: NextSize);
            next.Rect.anchorMin = next.Rect.anchorMax = next.Rect.pivot = new Vector2(0, 1);

            // Star stones: a stone with a star on it (art: star-stone-lit / unlit in UiIconSet); tip-only buttons.
            stoneRow = Ui.Rect("Star stones", root);
            stoneRow.anchorMin = stoneRow.anchorMax = stoneRow.pivot = new Vector2(0, 1);
            stoneRow.sizeDelta = new Vector2(3 * StoneSize + 2 * StoneGap, StoneSize);
            stoneStars = new Image[stones.Length];
            for (int k = 0; k < stones.Length; k++)
            {
                var s = IconButton.Build(stoneRow, UiIcon.Credits, icons, null, null, "result.star1", $"Stone {k + 1}");
                s.Rect.anchorMin = s.Rect.anchorMax = s.Rect.pivot = new Vector2(0, 1);
                s.Rect.anchoredPosition = new Vector2(k * (StoneSize + StoneGap) - (IconButton.Size - StoneSize) / 2, (IconButton.Size - StoneSize) / 2);
                var body = Ui.MakeImage(s.transform, Color.white, null, "Stone");
                body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                body.rectTransform.sizeDelta = new Vector2(StoneSize, StoneSize);
                body.transform.SetAsFirstSibling();
                stoneStars[k] = body;
                s.Glyph.rectTransform.offsetMin = new Vector2(12, 12);
                s.Glyph.rectTransform.offsetMax = new Vector2(-12, -12);
                stones[k] = s;
            }

            finalePlate = Ui.Plate(root, "Finale plate");
            // Top centre, between Map and Retry: the band the hidden HUD title leaves free (Next's words go by the exit, below).
            finalePlate.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -Safe), new Vector2(560, 48));
            finaleText = Ui.Label(finalePlate.transform, "", 22, Ui.Ink, TextAnchor.MiddleCenter, "Finale");
            finaleText.rectTransform.Stretch();

            plate = FocusPlate.Build(root, loc, icons);
            plate.Avoid = () =>
            {
                avoid.Clear();
                if (BoardArea != null) avoid.Add(BoardArea());
                avoid.Add(CanvasRect(stoneRow));
                if (finalePlate.gameObject.activeSelf) avoid.Add(CanvasRect(finalePlate.rectTransform));
                return avoid;
            };
            foreach (var b in new[] { map, retry, next }) plate.Track(b);
            foreach (var s in stones) plate.Track(s);
            IconNav.Row(new[] { map, stones[0], stones[1], stones[2], next, retry });
        }

        readonly List<Rect> avoid = new List<Rect>();

        /// A point-anchored child of this canvas as a canvas rect, top-left origin.
        Rect CanvasRect(RectTransform r)
        {
            var canvas = ((RectTransform)transform).rect.size;
            var size = r.sizeDelta;
            float x = r.anchorMin.x * canvas.x + r.anchoredPosition.x - r.pivot.x * size.x;
            float yUp = r.anchorMin.y * canvas.y + r.anchoredPosition.y - r.pivot.y * size.y;
            return new Rect(x, canvas.y - yUp - size.y, size.x, size.y);
        }

        /// Words and fonts in the current language; a shown clear redraws.
        public void Apply(Localizer localizer)
        {
            loc = localizer ?? throw new ArgumentNullException(nameof(localizer));
            finaleText.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            if (IsShown) Words(shown);
        }

        public void Show(ResultModel m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (!m.Cleared) { lossPanel.Show(m); return; } // the U5 failure screen is the next step
            bool redraw = ReferenceEquals(m, shown) && IsShown;
            shown = m;
            gameObject.SetActive(true);
            Words(m);
            Layout();
            if (redraw) return;
            HudHidden?.Invoke(true);
            ShowStones(m.Stars);
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null) es.SetSelectedGameObject(next.gameObject); // Enter = Next; its words show with it
        }

        void Words(ResultModel m)
        {
            next.SetTip(m.IsLast ? "result.ending" : "ui5.icon.next");
            stones[0].SetTip("result.star1");
            stones[1].SetTip("result.star2");
            stones[2].SetTip(m.LevelBodies == 1 ? "result.star3_one" : "result.star3_many", new object[] { m.LevelBodies });
            string line = m.IsLast ? loc.Get("result.journey_done")
                : m.FinishedChapter != null ? loc.Format("result.chapter_done", m.FinishedChapter.Planet(loc.Current)) : "";
            finaleText.text = line.Replace("\n", " ");
            finaleText.font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            finalePlate.gameObject.SetActive(line.Length > 0);
            if (line.Length > 0)
            {
                float w = finaleText.cachedTextGeneratorForLayout.GetPreferredWidth(finaleText.text, finaleText.GetGenerationSettings(Vector2.zero))
                          / Mathf.Max(0.01f, finaleText.pixelsPerUnit);
                finalePlate.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(w) + 32, 48);
            }
        }

        /// Stones over the brother on the exit; Next beside the board at the exit's height (or width), nearest side.
        public void Layout()
        {
            var canvas = ((RectTransform)transform).rect.size;
            if (canvas.x <= 0 || canvas.y <= 0) canvas = new Vector2(1280, 720);
            var exit = ExitPoint != null ? ExitPoint() : canvas / 2;
            var board = BoardArea != null ? BoardArea() : new Rect(canvas.x / 2, canvas.y / 2, 0, 0);
            var row = StoneRow(exit, canvas);
            stoneRow.anchoredPosition = new Vector2(row.x, -row.y);
            var n = NextSpot(board, exit, canvas);
            next.Rect.anchoredPosition = new Vector2(n.x, -n.y);
            next.Rect.sizeDelta = n.size;
        }

        /// The stone row (canvas, top-left origin): centred over <paramref name="head"/>, <see cref="StoneLift"/> above it,
        /// under the top button band, inside the safe area (pure; tests).
        public static Rect StoneRow(Vector2 head, Vector2 canvas)
        {
            float w = 3 * StoneSize + 2 * StoneGap;
            float x = Mathf.Clamp(head.x - w / 2, Safe, canvas.x - Safe - w);
            float y = Mathf.Clamp(head.y - StoneLift - StoneSize, Safe + IconButton.Size + 8, canvas.y - Safe - StoneSize);
            return new Rect(x, y, w, StoneSize);
        }

        /// Next (56) beside <paramref name="board"/> on the side nearest to <paramref name="exit"/>, level with it, inside
        /// the safe area and below the top buttons; no side fits: just right of the exit (pure; tests).
        public static Rect NextSpot(Rect board, Vector2 exit, Vector2 canvas)
        {
            float top = Safe + IconButton.Size + 8;
            var options = new List<Rect>
            {
                new Rect(board.xMax + NextGap, exit.y - NextSize / 2, NextSize, NextSize),
                new Rect(board.xMin - NextGap - NextSize, exit.y - NextSize / 2, NextSize, NextSize),
                new Rect(exit.x - NextSize / 2, board.yMax + NextGap, NextSize, NextSize),
                new Rect(exit.x - NextSize / 2, board.yMin - NextGap - NextSize, NextSize, NextSize),
            };
            Rect best = new Rect(exit.x + NextGap, exit.y - NextSize / 2, NextSize, NextSize);
            float bestD = float.MaxValue;
            foreach (var o in options)
            {
                var r = new Rect(o.x, Mathf.Clamp(o.y, top, canvas.y - Safe - NextSize), o.width, o.height);
                r.x = Mathf.Clamp(r.x, Safe, canvas.x - Safe - NextSize);
                if (r.Overlaps(board)) continue;
                float d = Vector2.Distance(r.center, exit);
                if (d < bestD) { bestD = d; best = r; }
            }
            return best;
        }

        void ShowStones(int earned)
        {
            if (reveal != null) { StopCoroutine(reveal); reveal = null; }
            for (int k = 0; k < stones.Length; k++) DrawStone(k, false);
            LitStones = 0;
            if (Application.isPlaying && isActiveAndEnabled) reveal = StartCoroutine(Reveal(earned));
            else for (int k = 0; k < earned; k++) DrawStone(k, true);
        }

        void DrawStone(int k, bool lit)
        {
            var art = icons != null ? (lit ? icons.starStoneLit : icons.starStoneUnlit) : null;
            var body = stoneStars[k];
            body.sprite = art != null ? art : Ui.Circle;
            body.color = art != null ? Color.white : (lit ? new Color(0.36f, 0.33f, 0.27f, 1) : new Color(0.2f, 0.21f, 0.24f, 0.9f));
            stones[k].Glyph.enabled = art == null; // the art stone carries its own star
            stones[k].Glyph.color = lit ? Ui.Gold : Ui.Dim;
            if (lit) LitStones = Mathf.Max(LitStones, k + 1);
        }

        IEnumerator Reveal(int earned)
        {
            for (int k = 0; k < earned; k++)
            {
                for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime) yield return null;
                DrawStone(k, true);
                var r = stones[k].Rect;
                for (float t = 0; t < 0.22f; t += Time.unscaledDeltaTime)
                {
                    r.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(t / 0.22f * Mathf.PI));
                    yield return null;
                }
                r.localScale = Vector3.one;
            }
            reveal = null;
        }

        public void Hide()
        {
            lossPanel.Hide();
            if (reveal != null) { StopCoroutine(reveal); reveal = null; }
            bool was = IsShown;
            shown = null;
            plate.Model.Clear();
            gameObject.SetActive(false);
            if (was) HudHidden?.Invoke(false);
        }
    }
}
