using System;

namespace SevenSteps.Game.UI.U5
{
    /// Why a button wants its explanation shown (docs/ui-u5-kit.md 4). Stronger sources win: a touch hold over the mouse
    /// hover over the keyboard / programmatic focus.
    public enum TipSource { Focus = 0, Hover = 1, Hold = 2 }

    /// <summary>
    /// What the one <see cref="FocusPlate"/> of a canvas says (pure model, docs/ui-u5-kit.md 7): per source the owner and
    /// its string key + shortcut; the strongest source that has an owner is shown. A source left by an owner that no
    /// longer holds it is ignored (late pointer-exit after another button took the hover).
    /// </summary>
    public sealed class FocusTip
    {
        public readonly struct Tip
        {
            public readonly object Owner;
            /// String table key (ui5.*).
            public readonly string Key;
            /// Keyboard shortcut shown after the words ("Z"), or null.
            public readonly string Shortcut;
            public Tip(object owner, string key, string shortcut) { Owner = owner; Key = key; Shortcut = shortcut; }
            public bool IsEmpty => Owner == null;
        }

        readonly Tip[] bySource = new Tip[3];

        /// Raised when <see cref="Current"/> changes.
        public event Action Changed;

        /// The tip to show; IsEmpty = hide the plate.
        public Tip Current
        {
            get
            {
                for (int i = bySource.Length - 1; i >= 0; i--) if (!bySource[i].IsEmpty) return bySource[i];
                return default;
            }
        }

        public void Enter(TipSource source, object owner, string key, string shortcut = null)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("no string key", nameof(key));
            int i = Index(source);
            var before = Current;
            bySource[i] = new Tip(owner, key, shortcut);
            Notify(before);
        }

        /// <paramref name="owner"/> leaves <paramref name="source"/>; nothing happens if another owner holds it now.
        public void Exit(TipSource source, object owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            int i = Index(source);
            if (!ReferenceEquals(bySource[i].Owner, owner)) return;
            var before = Current;
            bySource[i] = default;
            Notify(before);
        }

        /// <paramref name="owner"/> leaves every source (hidden, destroyed, disabled).
        public void Forget(object owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            var before = Current;
            for (int i = 0; i < bySource.Length; i++) if (ReferenceEquals(bySource[i].Owner, owner)) bySource[i] = default;
            Notify(before);
        }

        public void Clear()
        {
            var before = Current;
            for (int i = 0; i < bySource.Length; i++) bySource[i] = default;
            Notify(before);
        }

        static int Index(TipSource s)
        {
            if (!Enum.IsDefined(typeof(TipSource), s)) throw new ArgumentOutOfRangeException(nameof(s), s, "not a TipSource");
            return (int)s;
        }

        void Notify(Tip before)
        {
            var now = Current;
            if (ReferenceEquals(before.Owner, now.Owner) && before.Key == now.Key && before.Shortcut == now.Shortcut) return;
            Changed?.Invoke();
        }
    }
}
