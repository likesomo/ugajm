using SevenSteps.Contracts;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>Boot scene entry (build index 0). No screen: registers services and moves on to Title, or Game for ?level= / -level.</summary>
    public sealed class BootScreen : MonoBehaviour
    {
        [Tooltip("Data/AppConfig.asset")]
        public AppConfig config;
        [Tooltip("Data/GameAssets.asset: the stage list sets AppFlow's stage count")]
        public GameAssets assets;

        void Awake()
        {
            if (config == null) throw new MissingReferenceException($"[SevenSteps] {name}: BootScreen.config is not set (unity.sh scene-validate)");
            if (assets == null) throw new MissingReferenceException($"[SevenSteps] {name}: BootScreen.assets is not set (unity.sh scene-validate)");
        }

        void Start() => Bootstrap.Run(config, assets);
    }
}
