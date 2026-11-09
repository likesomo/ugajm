using System;
using SevenSteps.Contracts;
using UnityEngine.SceneManagement;

namespace SevenSteps.Game
{
    /// <summary>
    /// <see cref="ISceneLoader"/> over SceneManager (docs/architecture.md 11.1). Scene names = AppScenes constants.
    /// Loaded comes from SceneManager.sceneLoaded, which runs after the new scene's Awake/OnEnable and before its Start.
    /// It listens only while a load is pending, so a loader from an earlier Play (domain reload off) is not kept alive.
    /// </summary>
    public sealed class SceneLoader : ISceneLoader
    {
        string pending;

        public string Current => SceneManager.GetActiveScene().name;
        public event Action<string> Loaded;

        public void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) throw new ArgumentException("scene name is empty", nameof(sceneName));
            if (pending != null) throw new InvalidOperationException($"SceneLoader: {sceneName} requested while {pending} is loading");
            pending = sceneName;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != pending) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            pending = null;
            Loaded?.Invoke(scene.name);
        }
    }
}
