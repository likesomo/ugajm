using SevenSteps.Contracts;
using UnityEngine.SceneManagement;

namespace SevenSteps.Game
{
    /// <summary><see cref="ISceneLoader"/> over SceneManager (docs/architecture.md 11.1). Scene names = AppScenes constants.</summary>
    public sealed class SceneLoader : ISceneLoader
    {
        public string Current => SceneManager.GetActiveScene().name;
        public void Load(string sceneName) => SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
