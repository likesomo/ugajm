using System;
using System.Collections.Generic;

namespace SevenSteps.Game.Flow
{
    /// <summary>
    /// A panel that opens over a screen and closes with Esc or its own close button: settings, credits, later a confirm box
    /// or the chapter transition card (docs/scene-flow.md 4.7). The view only shows, hides and asks to close; whether it is
    /// open is the <see cref="OverlayStack"/>'s answer.
    /// </summary>
    public interface IOverlay
    {
        void Show();
        void Hide();
        /// The overlay's close button. The stack closes it (the view never hides itself).
        event Action CloseRequested;
    }

    /// <summary>
    /// Open overlays, top last (docs/scene-flow.md 4.3, S4). Pure C#, one per scene. The single source of "is something open":
    /// board input is blocked while <see cref="Count"/> &gt; 0, and Esc closes the top one first (<see cref="BackNavigator"/>).
    /// No Time.timeScale: the puzzle is turn based, "paused" just means the board takes no input.
    /// </summary>
    public sealed class OverlayStack
    {
        readonly List<IOverlay> open = new List<IOverlay>();
        readonly Dictionary<IOverlay, Action> closeHandlers = new Dictionary<IOverlay, Action>();

        public int Count => open.Count;
        public IOverlay Top => open.Count > 0 ? open[open.Count - 1] : null;
        public bool Contains(IOverlay overlay) => open.Contains(overlay);

        /// Raised after every push or pop.
        public event Action Changed;

        /// Opens an overlay on top. Opening one that is already open is a caller bug.
        public void Push(IOverlay overlay)
        {
            if (overlay == null) throw new ArgumentNullException(nameof(overlay));
            if (open.Contains(overlay)) throw new InvalidOperationException($"{overlay.GetType().Name} is already open");
            open.Add(overlay);
            Action onClose = () => Close(overlay);
            closeHandlers[overlay] = onClose;
            overlay.CloseRequested += onClose;
            overlay.Show();
            Changed?.Invoke();
        }

        /// Closes the top overlay. Returns false when none is open.
        public bool Pop()
        {
            if (open.Count == 0) return false;
            Close(open[open.Count - 1]);
            return true;
        }

        /// Closes an open overlay. Usually the top one (it covers the others), but one below may close itself too
        /// (the chapter card's timer while settings is open). Closing one that is not open is a caller bug.
        public void Close(IOverlay overlay)
        {
            if (overlay == null) throw new ArgumentNullException(nameof(overlay));
            int at = open.IndexOf(overlay);
            if (at < 0) throw new InvalidOperationException($"{overlay.GetType().Name} is not open");
            open.RemoveAt(at);
            overlay.CloseRequested -= closeHandlers[overlay];
            closeHandlers.Remove(overlay);
            overlay.Hide();
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Esc / back button in one place (docs/scene-flow.md 4.3, S4): close the top overlay, otherwise the scene's flow back
    /// (GameFlowController.Back, TitleFlowController.Back), which asks AppFlow.TryGetBackTarget.
    /// </summary>
    public sealed class BackNavigator
    {
        readonly OverlayStack overlays;
        readonly Action flowBack;

        public BackNavigator(OverlayStack overlays, Action flowBack)
        {
            this.overlays = overlays ?? throw new ArgumentNullException(nameof(overlays));
            this.flowBack = flowBack ?? throw new ArgumentNullException(nameof(flowBack));
        }

        public void Back()
        {
            if (!overlays.Pop()) flowBack();
        }
    }
}
