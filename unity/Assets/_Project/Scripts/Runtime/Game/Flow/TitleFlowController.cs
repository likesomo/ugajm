using System;
using SevenSteps.Contracts;

namespace SevenSteps.Game.Flow
{
    /// <summary>Which Title-scene panel is visible (TitleScreen toggles TitleView / StageSelectView; a fake in tests).</summary>
    public interface ITitlePanels
    {
        /// Title shows the title panel, StageSelect the stage list, anything else hides both (the scene is being left).
        void Show(AppState state);
    }

    /// <summary>
    /// Screen flow inside the Title scene (states Title and StageSelect, docs/scene-flow.md 4.3, S3). Pure C#.
    /// Shows the panel for the flow state and takes the flow actions; view events and keys call the public methods.
    /// An action the flow cannot take now (scene loading, wrong state) does nothing, it is not an error.
    /// </summary>
    public sealed class TitleFlowController : IDisposable
    {
        readonly AppFlow flow;
        readonly SaveSystem save;
        readonly ITitlePanels panels;

        public TitleFlowController(AppFlow flow, SaveSystem save, ITitlePanels panels)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.panels = panels ?? throw new ArgumentNullException(nameof(panels));
            flow.Changed += OnFlowChanged;
        }

        public void Dispose() => flow.Changed -= OnFlowChanged;

        /// The Title scene has started: from Boot (direct play or the Boot scene) go to Title, otherwise show the current state.
        public void Start()
        {
            if (flow.State == AppState.Boot) flow.Go(AppState.Title);
            else if (flow.State == AppState.Title || flow.State == AppState.StageSelect) panels.Show(flow.State);
            else throw new InvalidOperationException($"[SevenSteps] Title scene started in state {flow.State}");
        }

        /// Title "시작" / Enter / Space.
        public void OpenStageSelect() => Go(AppState.StageSelect);

        /// Stage buttons.
        public void PlayStage(int stage)
        {
            if (flow.CanGoToGame(stage)) flow.GoToGame(stage);
        }

        /// Enter on Stage Select: the last played stage. Save data is external input, so it is clamped, not asserted.
        public void PlayLastStage() => PlayStage(Math.Max(0, Math.Min(save.Progress.lastStage, flow.StageCount - 1)));

        /// Stage Select "뒤로" / Esc.
        public void Back()
        {
            if (flow.CanBack && flow.TryGetBackTarget(out var target)) Go(target);
        }

        void Go(AppState to)
        {
            if (flow.IsLoading || !AppFlow.CanGo(flow.State, to)) return;
            flow.Go(to);
        }

        void OnFlowChanged(AppState from, AppState to) => panels.Show(to);
    }
}
