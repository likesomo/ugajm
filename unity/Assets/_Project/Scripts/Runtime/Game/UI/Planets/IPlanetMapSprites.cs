using UnityEngine;

namespace SevenSteps.Game.UI.Planets
{
    /// <summary>
    /// Map planet sprites by chapter key (V7 cutouts, user choice 2026-11-12; the asset is Art's PlanetMapSpriteSet,
    /// Data/PlanetMapSpriteSet.asset, which the Game assembly can not name: Art references Game, as IStageEnvironment).
    /// The star map draws a planet with its sprite when there is one, else with its PlanetBodySet body.
    /// </summary>
    public interface IPlanetMapSprites
    {
        /// The sprite of <paramref name="planetId"/> (chapters.json id); false when there is none.
        bool TryGetSprite(string planetId, out Sprite sprite);
    }
}
