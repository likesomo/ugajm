using UnityEngine;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// Background of a chapter (docs/architecture.md 12.8): camera clear color, a sky prefab that follows the camera
    /// (nebula, stars, distant planets, optional post-processing Volume) and the planet summit under the board.
    /// </summary>
    [CreateAssetMenu(menuName = "Seven Steps/Sky Preset", fileName = "Sky")]
    public sealed class SkyPreset : ScriptableObject
    {
        [Tooltip("Camera background (solid color clear).")]
        public Color background = new Color(0.06f, 0.07f, 0.12f);
        [Tooltip("Parented to the board camera, local position/rotation kept. May hold a global Volume for post FX.")]
        public GameObject skyPrefab;
        [Tooltip("Planet summit and decoration under the board, at the world origin. A root component implementing IStageEnvironment is fitted to each stage.")]
        public GameObject planetPrefab;
    }

    /// <summary>
    /// Implemented by a planet/environment prefab root. Called once when a stage is entered (not every frame) so the
    /// summit and decoration match the board size. Must not read game objects, input or the command line.
    /// </summary>
    public interface IStageEnvironment
    {
        void Fit(BoardGrid grid);
    }
}
