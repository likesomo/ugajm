using System;
using SevenSteps.Contracts;
using SevenSteps.Game.Flow;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SevenSteps.Game.UI
{
    /// <summary>
    /// The in-game popup before the first hint of a try (user request 2026-11-14): the tier title ("Hint 1/3"), that this
    /// try then earns 1 star at most (hint.popup_body), [Show hint] [Cancel]. Enter shows, Esc (the overlay stack's back)
    /// and Cancel close it; Cancel is the marked default. Never a browser dialog. Opened by BoardController.ConfirmHint.
    /// </summary>
    public sealed class HintConfirmPanel : IOverlay
    {
        readonly GameObject root;
        readonly Text title, body;
        readonly Button show, cancel;
        readonly Text showLabel, cancelLabel;
        readonly OverlayStack overlays;
        readonly Localizer loc;
        Action onShow;

        public event Action CloseRequested;

        public bool Visible => root.activeSelf;
        public Button ShowButton => show;
        public Button CancelButton => cancel;
        public string TitleText => title.text;
        public RectTransform Box { get; }

        public HintConfirmPanel(Transform parent, OverlayStack overlays, Localizer loc)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.overlays = overlays ?? throw new ArgumentNullException(nameof(overlays));
            this.loc = loc ?? throw new ArgumentNullException(nameof(loc));
            var dim = Ui.MakeImage(Ui.Rect("HintConfirm", parent).Stretch(), new Color(0, 0, 0, 0.6f), null, "Dim");
            dim.rectTransform.Stretch();
            dim.raycastTarget = true; // the board and HUD under it take no clicks
            root = dim.transform.parent.gameObject;
            var box = Ui.Plate(dim.transform, "Box");
            box.raycastTarget = true;
            Box = box.rectTransform;
            Box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 300));
            title = Ui.Label(box.transform, "", 30, Ui.Gold, TextAnchor.MiddleCenter, "Title");
            title.rectTransform.TopLeft(40, 28, 560, 44);
            body = Ui.Label(box.transform, "", 24, Ui.Ink, TextAnchor.MiddleCenter, "Body");
            body.rectTransform.TopLeft(40, 80, 560, 110);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            show = Ui.MenuButton(box.transform, 24, () => Choose(true), out showLabel, "Show");
            cancel = Ui.MenuButton(box.transform, 24, () => Choose(false), out cancelLabel, "Cancel");
            ((RectTransform)show.transform).TopLeft(56, 208, 252, 64);
            ((RectTransform)cancel.transform).TopLeft(332, 208, 252, 64);
            Ui.FitButtonSkin(show);
            Ui.FitButtonSkin(cancel);
            // Cancel is the safe default: a gold outline marks it (Enter still shows, the user's spec).
            Ui.AddOutline(cancel.targetGraphic, Ui.Gold, 2f);
            root.SetActive(false);
            root.AddComponent<KeyReader>().Owner = this;
        }

        /// Opens the popup for <paramref name="tierTitle"/>; <paramref name="then"/> runs on [Show hint] or Enter.
        public void Open(string tierTitle, Action then)
        {
            onShow = then ?? throw new ArgumentNullException(nameof(then));
            title.text = tierTitle ?? "";
            if (!overlays.Contains(this)) overlays.Push(this);
        }

        public void Show()
        {
            var font = Ui.Theme.FontFor(loc.Current, FontRole.Body);
            title.font = Ui.Theme.FontFor(loc.Current, FontRole.Title);
            body.font = showLabel.font = cancelLabel.font = font;
            body.text = loc.Get("hint.popup_body");
            showLabel.text = loc.Get("hint.popup_show");
            cancelLabel.text = loc.Get("hint.popup_cancel");
            root.transform.SetAsLastSibling();
            root.SetActive(true);
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        public void Hide() => root.SetActive(false);

        void Choose(bool yes)
        {
            var then = onShow;
            onShow = null;
            if (overlays.Contains(this)) overlays.Close(this);
            if (yes) then?.Invoke();
        }

        /// Enter = show (keyboard), while the popup is the top overlay.
        sealed class KeyReader : MonoBehaviour
        {
            public HintConfirmPanel Owner;
            void Update()
            {
                var kb = Keyboard.current;
                if (kb == null || Owner == null || Owner.overlays.Top != Owner) return;
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Owner.Choose(true);
            }
        }
    }
}
