using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.U5
{
    /// <summary>
    /// U5 icon button (docs/ui-u5-kit.md 1, 4): a 48 x 48 press area with one white icon (about 40 inside), no visible
    /// words. Its name is shown by the canvas's <see cref="FocusPlate"/> on hover, keyboard focus or a 0.5 s touch hold
    /// (<see cref="FocusChanged"/>). Click, tap and Submit raise <see cref="Clicked"/>; a touch held 0.5 s only explains
    /// and does not act. View only: it reports, a controller decides (docs 7).
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class IconButton : MonoBehaviour, IPointerClickHandler, ISubmitHandler, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        public const float Size = 48, IconInset = 4, HoldSeconds = 0.5f;

        [SerializeField] UiIcon icon;
        [SerializeField] Image glyph;
        [SerializeField] Selectable selectable;
        [SerializeField] string tipKey;
        [SerializeField] string shortcut;

        /// Pressed (click, tap, Enter / Submit). Not raised for a touch hold, nor while not interactable.
        public event Action Clicked;
        /// The explanation should show (true) or go (false), and why. FocusPlate listens.
        public event Action<IconButton, TipSource, bool> FocusChanged;

        public UiIcon Icon => icon;
        public Image Glyph => glyph;
        public Selectable Selectable => selectable;
        /// String key of the explanation (default ui5.icon.&lt;icon&gt;).
        public string TipKey => tipKey;
        /// Keyboard shortcut added after the explanation ("Z"), or null.
        public string Shortcut => string.IsNullOrEmpty(shortcut) ? null : shortcut;
        /// Format arguments of the explanation, or null.
        public object[] TipArgs { get; private set; }

        /// Changes the explanation (key, its format arguments); a shown tip redraws.
        public void SetTip(string key, object[] args = null)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("no string key", nameof(key));
            tipKey = key;
            TipArgs = args;
            if (tipShown != 0) foreach (TipSource s in Enum.GetValues(typeof(TipSource))) if (Has(s)) Raise(s, true);
        }
        public RectTransform Rect => (RectTransform)transform;

        /// Default explanation key of an icon: ui5.icon.&lt;lower-case name&gt; (docs 5).
        public static string KeyOf(UiIcon icon)
        {
            UiIconSet.Check(icon);
            return "ui5.icon." + icon.ToString().ToLowerInvariant();
        }

        /// Builds an icon button under <paramref name="parent"/> (Edit Mode too). Place it with Ui.Place / TopLeft.
        /// <paramref name="onClick"/> may be null when the caller subscribes to <see cref="Clicked"/> itself.
        public static IconButton Build(Transform parent, UiIcon icon, UiIconSet set, string shortcut = null, Action onClick = null,
            string tipKey = null, string name = null, float size = Size)
        {
            if (!(size >= Size)) throw new ArgumentOutOfRangeException(nameof(size), size, "at least 48 (docs/ui-u5-kit.md 3)");
            return Build<IconButton>(parent, icon, set, size, shortcut, onClick, tipKey, name);
        }

        protected static T Build<T>(Transform parent, UiIcon icon, UiIconSet set, float size, string shortcut, Action onClick,
            string tipKey, string name) where T : IconButton
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            UiIconSet.Check(icon);
            var root = Ui.Rect(name ?? icon.ToString(), parent);
            root.sizeDelta = new Vector2(size, size);
            // The whole square is the press area: a clear image catches the pointer (docs 3: transparent hit areas count).
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            hit.raycastTarget = true;

            var g = Ui.MakeImage(root, Ui.Ink, UiIconSet.Of(set, icon), "Icon");
            g.preserveAspect = true;
            g.rectTransform.Stretch();
            float inset = IconInset * size / Size;
            g.rectTransform.offsetMin = new Vector2(inset, inset);
            g.rectTransform.offsetMax = new Vector2(-inset, -inset);
            // 14.3: 3:1 over light board tiles too; a soft dark edge keeps the line readable on any background.
            var shadow = g.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.02f, 0.04f, 0.08f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            var sel = root.gameObject.AddComponent<Selectable>();
            sel.targetGraphic = g;
            sel.transition = Selectable.Transition.ColorTint;
            var colors = sel.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = new Color(1f, 0.85f, 0.5f, 1f); // toward gold
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.3f);
            colors.fadeDuration = 0.08f;
            sel.colors = colors;
            sel.navigation = new Navigation { mode = Navigation.Mode.None }; // IconNav links rows explicitly (docs 4)

            var b = root.gameObject.AddComponent<T>();
            b.icon = icon;
            b.glyph = g;
            b.selectable = sel;
            b.tipKey = string.IsNullOrEmpty(tipKey) ? KeyOf(icon) : tipKey;
            b.shortcut = shortcut;
            if (onClick != null) b.Clicked += onClick;
            return b;
        }

        /// A round dark backing behind the icon (U5 buttons over the board: the thin line art keeps 3:1 on moss and crust,
        /// 14.3; R2 on 1f90617). Idempotent.
        public Image Backing()
        {
            var t = transform.Find("Backing");
            if (t != null) return t.GetComponent<Image>();
            var disc = Ui.MakeImage(transform, new Color(0.04f, 0.06f, 0.1f, 0.78f), Ui.Circle, "Backing");
            disc.rectTransform.Stretch();
            disc.transform.SetSiblingIndex(0);
            return disc;
        }

        /// Swaps the icon (sound on / off) and its default explanation.
        public void SetIcon(UiIcon value, UiIconSet set)
        {
            UiIconSet.Check(value);
            bool defaultKey = tipKey == KeyOf(icon);
            icon = value;
            glyph.sprite = UiIconSet.Of(set, value);
            if (defaultKey) tipKey = KeyOf(value);
            if (tipShown != 0) foreach (TipSource s in Enum.GetValues(typeof(TipSource))) if (Has(s)) Raise(s, true); // new words
        }

        /// Shown and usable, or hidden and out of the keyboard path (docs 4: hidden = not interactable).
        public void SetShown(bool shown)
        {
            gameObject.SetActive(shown);
            selectable.interactable = shown;
            if (!shown) Untip();
        }

        public bool Shown => gameObject.activeSelf && selectable.interactable;

        // ---------- pointer / keyboard ----------

        bool pressing, pressTouch, heldLong, suppressClick;
        float pressStart;
        int tipShown; // bit per TipSource

        bool Has(TipSource s) => (tipShown & (1 << (int)s)) != 0;

        protected void Raise(TipSource s, bool on)
        {
            if (on) tipShown |= 1 << (int)s; else tipShown &= ~(1 << (int)s);
            FocusChanged?.Invoke(this, s, on);
        }

        static bool IsTouch(PointerEventData e) => e is ExtendedPointerEventData x ? x.pointerType == UIPointerType.Touch : e.pointerId >= 0;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            pressing = true;
            pressTouch = IsTouch(e);
            heldLong = false;
            pressStart = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!pressing) return;
            pressing = false;
            if (heldLong) { suppressClick = true; Raise(TipSource.Hold, false); }
        }

        void Update()
        {
            if (!pressing || !pressTouch || heldLong) return;
            if (Time.unscaledTime - pressStart < HoldSeconds) return;
            heldLong = true;
            if (selectable.IsInteractable()) Raise(TipSource.Hold, true);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (suppressClick) { suppressClick = false; return; }
            if (!selectable.IsInteractable()) return;
            Tap(IsTouch(e));
        }

        public void OnSubmit(BaseEventData e)
        {
            if (selectable.IsInteractable()) Press();
        }

        /// A click or tap that was not a hold. Icons act at once; GlyphButton asks for a second tap on touch.
        protected virtual void Tap(bool touch) => Press();

        /// Raises <see cref="Clicked"/> (also for a controller's own shortcut path in tests).
        public void Press()
        {
            if (!selectable.IsInteractable()) return;
            Clicked?.Invoke();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (!IsTouch(e) && selectable.IsInteractable()) Raise(TipSource.Hover, true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (Has(TipSource.Hover)) Raise(TipSource.Hover, false);
            if (pressing && heldLong) Raise(TipSource.Hold, false);
            pressing = false;
        }

        public virtual void OnSelect(BaseEventData e) => Raise(TipSource.Focus, true);
        public virtual void OnDeselect(BaseEventData e) { if (Has(TipSource.Focus)) Raise(TipSource.Focus, false); }

        void OnDisable()
        {
            pressing = false;
            Untip();
        }

        void Untip()
        {
            foreach (TipSource s in Enum.GetValues(typeof(TipSource))) if (Has(s)) Raise(s, false);
        }
    }

    /// <summary>
    /// KEEP / SWITCH glyph (docs/ui-u5-kit.md 1, 4): an <see cref="IconButton"/> of 56. A mouse click or Enter confirms at
    /// once; on touch the first tap only focuses it and shows its explanation (the moment of decision), a second tap on the
    /// same glyph confirms, and a tap on the other glyph moves the focus there.
    /// </summary>
    public sealed class GlyphButton : IconButton
    {
        public new const float Size = 56;

        bool armed;

        /// Focused by a first tap: the next tap confirms.
        public bool Armed => armed;

        public static GlyphButton Build(Transform parent, UiIcon icon, UiIconSet set, string shortcut = null, Action onClick = null,
            string tipKey = null, string name = null)
        {
            if (icon != UiIcon.Keep && icon != UiIcon.Switch) throw new ArgumentOutOfRangeException(nameof(icon), icon, "glyphs are KEEP and SWITCH");
            return Build<GlyphButton>(parent, icon, set, Size, shortcut, onClick, tipKey, name);
        }

        protected override void Tap(bool touch)
        {
            if (!touch || armed) { armed = false; Press(); return; }
            armed = true;
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(gameObject); // OnSelect raises the focus tip; the other glyph deselects
            else Raise(TipSource.Focus, true);
        }

        public override void OnDeselect(BaseEventData e)
        {
            armed = false;
            base.OnDeselect(e);
        }
    }
}
