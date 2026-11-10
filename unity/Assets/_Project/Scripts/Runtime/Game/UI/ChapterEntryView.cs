using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// Chapter entry screen (view only, art concept chapter-entry; docs/scene-flow.md S7b): "Chapter 05", the planet name,
    /// the row of every planet with this one lit, and the intro line, over the menu background. The code-built default
    /// <see cref="IChapterCard"/> used when the theme has no card prefab (UiTheme.chapterCard). Asks to close after
    /// <see cref="Seconds"/> (unscaled) or on a click; Esc closes it through the BackNavigator.
    /// </summary>
    public sealed class ChapterEntryView : MonoBehaviour, IChapterCard
    {
        public const float Seconds = 2.5f;
        const int MaxPlanets = 12;

        Localizer loc;
        Text head, planet, intro;
        Image[] chips;
        Text[] chipNames;
        float shownAt;

        public event Action CloseRequested;

        /// Builds the screen under an overlay canvas, hidden.
        public static ChapterEntryView Create(Transform parent, Localizer localizer)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var root = Ui.Rect("ChapterEntry", parent).Stretch();
            var view = root.gameObject.AddComponent<ChapterEntryView>();
            view.Build(root, localizer ?? throw new ArgumentNullException(nameof(localizer)));
            root.gameObject.SetActive(false);
            return view;
        }

        void Build(RectTransform root, Localizer localizer)
        {
            loc = localizer;
            var block = Ui.MakeImage(root, Ui.PanelBg, null, "Solid"); // behind the background prefab: the board never shows through
            block.rectTransform.Stretch();
            Ui.MenuBackground(root);
            block.transform.SetAsFirstSibling();
            var wash = Ui.Wash(root, 0.76f);
            wash.raycastTarget = true;
            wash.gameObject.AddComponent<Button>().onClick.AddListener(() => CloseRequested?.Invoke());
            head = Ui.Label(root, "", 48, Ui.Ink, TextAnchor.MiddleLeft, "Head");
            head.rectTransform.TopLeft(40, 30, 1070, 72);
            planet = Ui.Label(root, "", 64, Ui.Gold, TextAnchor.MiddleCenter, "Planet");
            planet.rectTransform.TopLeft(80, 205, 1120, 100);
            chips = new Image[MaxPlanets];
            chipNames = new Text[MaxPlanets];
            for (int i = 0; i < MaxPlanets; i++)
            {
                chips[i] = Ui.Card(root, "Planet " + (i + 1));
                chipNames[i] = Ui.Label(chips[i].transform, "", 21, Ui.Ink, TextAnchor.MiddleCenter, "Name");
                chipNames[i].rectTransform.Stretch();
                // between the card sprite's two rules; long names (en) shrink to one line instead of crossing them
                chipNames[i].rectTransform.offsetMin = new Vector2(7, 22);
                chipNames[i].rectTransform.offsetMax = new Vector2(-7, -22);
                chipNames[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                chipNames[i].verticalOverflow = VerticalWrapMode.Truncate;
                chipNames[i].resizeTextForBestFit = true;
                chipNames[i].resizeTextMinSize = 14;
                chipNames[i].resizeTextMaxSize = 21;
            }
            intro = Ui.Label(root, "", 28, Ui.Ink, TextAnchor.MiddleCenter, "Intro");
            intro.rectTransform.TopLeft(150, 543, 980, 90);
            intro.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        public void Bind(ChapterCardModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.Planets.Count > MaxPlanets) throw new ArgumentException($"{model.Planets.Count} planets, the row holds {MaxPlanets}", nameof(model));
            var lang = loc.Current;
            head.text = loc.Format("chapter.number", model.Number);
            head.font = Ui.Theme.FontFor(lang, FontRole.Title);
            planet.text = model.Planet;
            planet.font = Ui.Theme.FontFor(lang, FontRole.Title);
            intro.text = model.Intro;
            intro.font = Ui.Theme.FontFor(lang, FontRole.Body);
            int n = model.Planets.Count;
            float step = n > 0 ? Mathf.Min(174f, 1220f / n) : 0;
            float width = Mathf.Min(145f, step - 12);
            float x0 = (1280 - (n - 1) * step - width) / 2;
            var body = Ui.Theme.FontFor(lang, FontRole.Body);
            for (int i = 0; i < MaxPlanets; i++)
            {
                bool used = i < n;
                chips[i].gameObject.SetActive(used);
                if (!used) continue;
                chips[i].rectTransform.TopLeft(x0 + i * step, 397, width, 85);
                bool current = i == model.Number - 1;
                chips[i].color = Ui.Theme.card != null ? (current ? Ui.Gold : Color.white) : (current ? Ui.Gold : Ui.ButtonBg);
                chipNames[i].text = model.Planets[i];
                chipNames[i].font = body;
                chipNames[i].color = current && Ui.Theme.card == null ? Ui.PanelBg : Ui.Ink;
            }
        }

        public void Show()
        {
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            shownAt = Time.unscaledTime;
        }

        public void Hide() => gameObject.SetActive(false);

        void Update()
        {
            if (Time.unscaledTime - shownAt >= Seconds) CloseRequested?.Invoke();
        }
    }
}
