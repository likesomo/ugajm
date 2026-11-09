using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// Language picker (view only, art layout concepts/ui language; docs/architecture.md 13.3): English / 한국어 buttons,
    /// each in its own language's body font, a note and Continue. A language button previews the language at once
    /// (<see cref="LanguagePicked"/>; the root sets the Localizer, every view redraws), Continue confirms
    /// (<see cref="ContinueRequested"/>; the root saves it). Shown over the Title on the first run and from the title's
    /// language line. An overlay: Esc closes it without saving.
    /// </summary>
    public sealed class LanguagePanel : IOverlay
    {
        readonly GameObject panel;
        readonly Text head, both, note, cont;
        readonly Button[] buttons;
        readonly Text[] names;
        readonly Button continueButton;
        Language current;

        public event Action<Language> LanguagePicked;
        public event Action ContinueRequested;
        public event Action CloseRequested;

        public LanguagePanel(Transform root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            panel = Ui.Rect("Language", root).Stretch().gameObject;
            var blocker = Ui.Wash(panel.transform, 0.94f, "Blocker");
            blocker.raycastTarget = true;
            var s = panel.transform;
            head = Ui.Label(s, "", 48, Ui.Ink, TextAnchor.MiddleLeft, "Head");
            head.rectTransform.TopLeft(40, 30, 1070, 72);
            var frame = Ui.Panel(s, "Frame").rectTransform.TopLeft(305, 191, 670, 367);
            both = Ui.Label(frame, "", 42, Ui.Gold, TextAnchor.MiddleCenter, "Both");
            both.rectTransform.TopLeft(45, 33, 580, 72);
            buttons = new Button[Languages.All.Length];
            names = new Text[Languages.All.Length];
            for (int i = 0; i < Languages.All.Length; i++)
            {
                var lang = Languages.All[i];
                buttons[i] = Ui.MenuButton(frame, 25, () => LanguagePicked?.Invoke(lang), out names[i], "Lang " + Languages.Code(lang));
                float x = Languages.All.Length == 2 ? 45 + i * 302 : 45 + i * (580f / Languages.All.Length);
                ((RectTransform)buttons[i].transform).TopLeft(x, 134, 270, 55);
            }
            note = Ui.Label(frame, "", 27, Ui.Ink, TextAnchor.MiddleCenter, "Note");
            note.rectTransform.TopLeft(48, 227, 568, 70);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            continueButton = Ui.MenuButton(s, 25, () => ContinueRequested?.Invoke(), out cont, "Continue");
            ((RectTransform)continueButton.transform).TopLeft(482, 585, 316, 55);
            cont.color = Ui.Gold;
            panel.SetActive(false);
        }

        /// Text for the current language; the language buttons always show their own name in their own font (13.4).
        public void Apply(Localizer loc)
        {
            if (loc == null) throw new ArgumentNullException(nameof(loc));
            current = loc.Current;
            UiText.Set(head, loc, "language.head", FontRole.Title);
            UiText.Set(note, loc, "language.note", FontRole.Body);
            UiText.Set(cont, loc, "language.continue", FontRole.Body);
            var parts = new string[Languages.All.Length];
            for (int i = 0; i < Languages.All.Length; i++)
            {
                var lang = Languages.All[i];
                parts[i] = SettingsPanel.NativeName(lang);
                names[i].text = parts[i];
                names[i].font = Ui.Theme.FontFor(lang, FontRole.Body);
                names[i].color = lang == current ? Ui.Gold : Ui.Ink;
                buttons[i].interactable = loc.Has(lang);
            }
            both.text = string.Join(" / ", parts);
            both.font = Ui.Theme.FontFor(current, FontRole.Number); // Jua: Latin and Hangul
        }

        public void Show()
        {
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
            continueButton.Select();
        }

        public void Hide() => panel.SetActive(false);
    }
}
