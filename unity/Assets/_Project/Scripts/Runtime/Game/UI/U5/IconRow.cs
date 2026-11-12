using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Game.UI.U5
{
    /// <summary>
    /// A short centred row of icon buttons (docs/ui-u5-kit.md 1: pause, settings): 48 buttons 28 apart (76 centre to
    /// centre), one line, not a full-screen panel. Hidden buttons close the gap and leave the keyboard path; left / right
    /// are linked explicitly (<see cref="IconNav"/>).
    /// </summary>
    public sealed class IconRow : MonoBehaviour
    {
        public const float Spacing = 28, Pitch = IconButton.Size + Spacing;

        readonly List<IconButton> buttons = new List<IconButton>();

        public IReadOnlyList<IconButton> Buttons => buttons;

        /// An empty row centred at <paramref name="center"/> (reference units from <paramref name="anchor"/> on the parent).
        public static IconRow Build(Transform parent, Vector2 anchor, Vector2 center, string name = "Icon row")
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var r = Ui.Rect(name, parent);
            r.Place(anchor, new Vector2(0.5f, 0.5f), center, new Vector2(0, IconButton.Size));
            return r.gameObject.AddComponent<IconRow>();
        }

        /// Adds an icon button at the end and lays the row out again.
        public IconButton Add(UiIcon icon, UiIconSet set, string shortcut = null, Action onClick = null, string tipKey = null)
        {
            var b = IconButton.Build(transform, icon, set, shortcut, onClick, tipKey);
            buttons.Add(b);
            Layout();
            return b;
        }

        /// Shows or hides one button; the row closes up and re-links.
        public void SetShown(IconButton b, bool shown)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (!buttons.Contains(b)) throw new ArgumentException($"{b.name} is not in row {name}", nameof(b));
            b.SetShown(shown);
            Layout();
        }

        /// Centres the shown buttons and links them left / right.
        public void Layout()
        {
            var shown = buttons.FindAll(b => b.Shown);
            float width = shown.Count == 0 ? 0 : shown.Count * IconButton.Size + (shown.Count - 1) * Spacing;
            ((RectTransform)transform).sizeDelta = new Vector2(width, IconButton.Size);
            for (int i = 0; i < shown.Count; i++)
                shown[i].Rect.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (shown.Count - 1) / 2f) * Pitch, 0),
                    new Vector2(IconButton.Size, IconButton.Size));
            IconNav.Row(buttons);
        }

        /// The first shown button (keyboard focus when the row opens), or null.
        public IconButton First => buttons.Find(b => b.Shown);
    }

    /// Explicit keyboard navigation (docs/ui-u5-kit.md 4; automatic navigation stopped on hidden buttons, cd38eb8 R1).
    public static class IconNav
    {
        /// Links the shown buttons of <paramref name="row"/> left / right in order; hidden ones get no links and are
        /// skipped. No wrap-around.
        public static void Row(IReadOnlyList<IconButton> row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            var shown = new List<Selectable>();
            foreach (var b in row)
            {
                if (b == null) throw new ArgumentException("null button in row", nameof(row));
                if (b.Shown) shown.Add(b.Selectable);
                else b.Selectable.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            for (int i = 0; i < shown.Count; i++)
                shown[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? shown[i - 1] : null,
                    selectOnRight = i + 1 < shown.Count ? shown[i + 1] : null,
                };
        }
    }
}
