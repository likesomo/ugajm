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

    /// <summary>
    /// Loads a scene by name. Implemented in SevenSteps.Game with SceneManager; a fake in tests.
    /// Raises <see cref="Loaded"/> with the scene name once that scene is loaded (its Awake has run, its Start has not).
    /// A fake may raise it inside Load.
    /// </summary>
    public interface ISceneLoader
    {
        string Current { get; }
        void Load(string sceneName);
        event Action<string> Loaded;
    }

    /// <summary>
    /// App-level state machine: Boot → Title → StageSelect → Game ⇄ Result. Pure C#.
    /// Changing state loads the hosting scene only if it differs from the current one;
    /// screens inside a scene listen to <see cref="Changed"/> and show/hide their panels.
    /// Assertive (docs/architecture.md 9, docs/scene-flow.md S2): a transition outside <see cref="CanGo"/>, a stage out of
    /// range, or any transition while a scene is loading throws. Input handlers ask first (<see cref="IsLoading"/>,
    /// <see cref="CanBack"/>, <see cref="CanGoToGame"/>) and ignore the key when the answer is no.
    /// </summary>
    public sealed class AppFlow
    {
        readonly ISceneLoader scenes;
        string loadingScene;

        public AppFlow(ISceneLoader scenes, int stageCount)
        {
            this.scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            if (stageCount < 1) throw new ArgumentOutOfRangeException(nameof(stageCount), stageCount, "at least one stage");
            StageCount = stageCount;
            scenes.Loaded += OnSceneLoaded;
        }

        public AppState State { get; private set; } = AppState.Boot;
        /// 0-based stage the Game state plays. Set by GoToGame.
        public int Stage { get; private set; }
        /// Number of stages (levels.json). GoToGame accepts 0 .. StageCount - 1.
        public int StageCount { get; }
        /// A scene load was requested and has not finished. No transition is allowed until it has.
        public bool IsLoading => loadingScene != null;

        /// (from, to). Raised after the scene load was requested (the old scene is still loaded).
        public event Action<AppState, AppState> Changed;

        public static bool CanGo(AppState from, AppState to) => (from, to) switch
        {
            (AppState.Boot, AppState.Title) => true,
            (AppState.Boot, AppState.Game) => true,           // ?level= / -level launch
            (AppState.Title, AppState.StageSelect) => true,
            (AppState.Title, AppState.Game) => true,          // title Begin (stage 1) / Continue (last played), S7a
            (AppState.StageSelect, AppState.Title) => true,
            (AppState.StageSelect, AppState.Game) => true,
            (AppState.Game, AppState.Result) => true,
            (AppState.Game, AppState.StageSelect) => true,
            (AppState.Game, AppState.Game) => true,           // stage change inside the game scene
            (AppState.Result, AppState.Game) => true,         // next / retry
            (AppState.Result, AppState.StageSelect) => true,
            _ => false,
        };

        /// Where Back (Esc) goes from a state, if anywhere. Every pair is also allowed by CanGo. Boot and Title have none.
        public static bool TryGetBackTarget(AppState from, out AppState to)
        {
            switch (from)
            {
                case AppState.StageSelect: to = AppState.Title; return true;
                case AppState.Game: to = AppState.StageSelect; return true;
                case AppState.Result: to = AppState.StageSelect; return true;
                default: to = from; return false;
            }
        }

        public bool TryGetBackTarget(out AppState to) => TryGetBackTarget(State, out to);
        /// Back would do something now: a back target exists and no scene is loading.
        public bool CanBack => !IsLoading && TryGetBackTarget(State, out _);
        /// GoToGame(stage) would succeed now.
        public bool CanGoToGame(int stage) => !IsLoading && stage >= 0 && stage < StageCount && CanGo(State, AppState.Game);

        public void GoToGame(int stage)
        {
            if (stage < 0 || stage >= StageCount)
                throw new ArgumentOutOfRangeException(nameof(stage), stage, $"AppFlow: stage must be 0..{StageCount - 1}");
            ThrowIfCannotGo(AppState.Game);
            int previous = Stage;
            Stage = stage;
            try { Go(AppState.Game); }
            catch { Stage = previous; throw; }
        }

        public void Go(AppState to)
        {
            ThrowIfCannotGo(to);
            var from = State;
            State = to;
            string scene = AppScenes.For(to);
            if (scenes.Current != scene)
            {
                loadingScene = scene; // set first: a fake loader may report Loaded inside Load
                try { scenes.Load(scene); }
                catch
                {
                    // The loader refused (scene not in the build): nothing changed, so the flow must not stay "loading".
                    loadingScene = null;
                    State = from;
                    throw;
                }
            }
            Changed?.Invoke(from, to);
        }

        void ThrowIfCannotGo(AppState to)
        {
            if (IsLoading) throw new InvalidOperationException($"AppFlow: {State} -> {to} while scene {loadingScene} is loading");
            if (!CanGo(State, to)) throw new InvalidOperationException($"AppFlow: {State} -> {to} is not allowed");
        }

        void OnSceneLoaded(string sceneName)
        {
            if (sceneName == loadingScene) loadingScene = null;
        }
    }
}
