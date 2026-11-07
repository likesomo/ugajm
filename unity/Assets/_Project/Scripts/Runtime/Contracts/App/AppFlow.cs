using System;

namespace SevenSteps.Contracts
{
    /// <summary>App screens. Append-only.</summary>
    public enum AppState { Boot, Title, StageSelect, Game, Result }

    /// <summary>Scene that hosts each state (docs/architecture.md 8장). Title+StageSelect share a scene, Game+Result share a scene.</summary>
    public static class AppScenes
    {
        public const string Boot = "Boot";
        public const string Title = "Title";
        public const string Game = "Game";

        public static string For(AppState s) => s switch
        {
            AppState.Boot => Boot,
            AppState.Title or AppState.StageSelect => Title,
            _ => Game,
        };
    }

    /// <summary>Loads a scene by name. Implemented in SevenSteps.Game with SceneManager; a fake in tests.</summary>
    public interface ISceneLoader
    {
        string Current { get; }
        void Load(string sceneName);
    }

    /// <summary>
    /// App-level state machine: Boot → Title → StageSelect → Game ⇄ Result. Pure C#.
    /// Changing state loads the hosting scene only if it differs from the current one;
    /// screens inside a scene listen to <see cref="Changed"/> and show/hide their panels.
    /// </summary>
    public sealed class AppFlow
    {
        readonly ISceneLoader scenes;

        public AppFlow(ISceneLoader scenes) { this.scenes = scenes; }

        public AppState State { get; private set; } = AppState.Boot;
        /// 0-based stage the Game state plays. Set by GoToGame.
        public int Stage { get; private set; }

        /// (from, to). Raised after the scene load was requested.
        public event Action<AppState, AppState> Changed;

        public static bool CanGo(AppState from, AppState to) => (from, to) switch
        {
            (AppState.Boot, AppState.Title) => true,
            (AppState.Boot, AppState.Game) => true,           // ?level= / -level launch
            (AppState.Title, AppState.StageSelect) => true,
            (AppState.StageSelect, AppState.Title) => true,
            (AppState.StageSelect, AppState.Game) => true,
            (AppState.Game, AppState.Result) => true,
            (AppState.Game, AppState.StageSelect) => true,
            (AppState.Game, AppState.Game) => true,           // stage change inside the game scene
            (AppState.Result, AppState.Game) => true,         // next / retry
            (AppState.Result, AppState.StageSelect) => true,
            _ => false,
        };

        public void GoToGame(int stage)
        {
            Stage = stage;
            Go(AppState.Game);
        }

        public void Go(AppState to)
        {
            var from = State;
            if (!CanGo(from, to)) throw new InvalidOperationException($"AppFlow: {from} -> {to} is not allowed");
            State = to;
            string scene = AppScenes.For(to);
            if (scenes != null && scenes.Current != scene) scenes.Load(scene);
            Changed?.Invoke(from, to);
        }
    }
}
