using SevenSteps.Contracts;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>Boot scene entry (build index 0). No screen: registers services and moves on to Title, or Game for ?level= / -level.</summary>
    public sealed class BootScreen : MonoBehaviour
    {
        [Tooltip("Data/AppConfig.asset")]
        public AppConfig config;

        void Start() => Bootstrap.Run(config);
    }
}
