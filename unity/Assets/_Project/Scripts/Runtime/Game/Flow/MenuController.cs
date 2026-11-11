using System;

namespace SevenSteps.Game.Flow
{
    /// <summary>The in-game menu as the controller sees it (MenuPanel; a fake in tests).</summary>
    public interface IMenuPanel : IOverlay
    {
        event Action SettingsRequested, StagesRequested, SolutionRequested, ControlsRequested, ReplayLineRequested;
        /// The "start line again" row shows (true) or is left out (false); set before the menu opens.
        void ShowReplayLine(bool shown);
    }

    /// <summary>
    /// The one menu button of the minimal HUD (production/ui-minimal-plan.md, design/art/ui-minimal.md "메뉴 모달"):
    /// settings, stage select, show solution and the controls guide live inside it. An overlay, so the board takes no input
    /// while it is open and Esc closes it. Each item closes the menu first, then:
    /// - Settings: the settings panel (Esc there returns to the game).
    /// - Stages: the planet map (GameFlowController.OpenStageSelect, the same as before).
    /// - Solution: the H hint (BoardController.ShowHint: counts a hint, stars as before).
    /// - Controls: the move guide once more (TutorialTracker.PlayControls; nothing saved).
    /// - Start line again (story-integration.md 0.2): the stage's start line once the menu closes (StoryDirector via
    ///   GameController.ReplayStartLine). Shown only when the stage has one and no result is up (review ebdb653 N1).
    /// Pure C#: the scene root passes the actions.
    /// </summary>
    public sealed class MenuController : IDisposable
    {
        readonly OverlayStack overlays;
        readonly IMenuPanel panel;
        readonly Action openSettings, openStages, showSolution, playControls;
        readonly Func<bool> canReplayLine, replayLine;

        public MenuController(OverlayStack overlays, IMenuPanel panel, Action openSettings, Action openStages, Action showSolution, Action playControls,
            Func<bool> canReplayLine = null, Func<bool> replayLine = null)
        {
            this.overlays = overlays ?? throw new ArgumentNullException(nameof(overlays));
            this.panel = panel ?? throw new ArgumentNullException(nameof(panel));
            this.openSettings = openSettings ?? throw new ArgumentNullException(nameof(openSettings));
            this.openStages = openStages ?? throw new ArgumentNullException(nameof(openStages));
            this.showSolution = showSolution ?? throw new ArgumentNullException(nameof(showSolution));
            this.playControls = playControls ?? throw new ArgumentNullException(nameof(playControls));
            if ((canReplayLine == null) != (replayLine == null)) throw new ArgumentException("canReplayLine and replayLine come together");
            this.canReplayLine = canReplayLine;
            this.replayLine = replayLine;
            panel.SettingsRequested += OnSettings;
            panel.StagesRequested += OnStages;
            panel.SolutionRequested += OnSolution;
            panel.ControlsRequested += OnControls;
            panel.ReplayLineRequested += OnReplayLine;
        }

        public void Dispose()
        {
            panel.SettingsRequested -= OnSettings;
            panel.StagesRequested -= OnStages;
            panel.SolutionRequested -= OnSolution;
            panel.ControlsRequested -= OnControls;
            panel.ReplayLineRequested -= OnReplayLine;
        }

        public bool IsOpen => overlays.Contains(panel);

        /// The HUD menu button. Only over the board: ignored while another overlay is open.
        public void Open()
        {
            if (overlays.Count > 0) return;
            panel.ShowReplayLine(canReplayLine != null && canReplayLine());
            overlays.Push(panel);
        }

        /// Menu button again: closes it.
        public void Toggle()
        {
            if (IsOpen) overlays.Close(panel);
            else Open();
        }

        void Then(Action action)
        {
            if (!IsOpen) return; // a late click after Esc
            overlays.Close(panel);
            action();
        }

        void OnSettings() => Then(openSettings);
        void OnStages() => Then(openStages);
        void OnSolution() => Then(showSolution);
        void OnControls() => Then(playControls);
        void OnReplayLine() { if (replayLine != null) Then(() => replayLine()); }
    }
}
