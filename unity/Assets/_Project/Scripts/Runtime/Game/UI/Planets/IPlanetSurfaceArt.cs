using UnityEngine;

namespace SevenSteps.Game.UI.Planets
{
    /// <summary>
    /// Planet surface paintings by chapter key (S2, user choice 2026-11-12; v7-surface README "Scene-flow integration
    /// work"): the V7 painting is front-projected onto the surface view's 3D body, which keeps its idle turn. The asset is
    /// Art's mapping (feat/art-surface-v7), which the Game assembly can not name (as <see cref="IPlanetMapSprites"/>).
    /// A planet without an entry keeps the current PlanetBodySet body.
    /// </summary>
    public interface IPlanetSurfaceArt
    {
        /// The painting of <paramref name="planetId"/> (chapters.json id) and the material drawing it; false when there is
        /// none. True needs a texture or a material: with no material the body builds the default projected look (URP Lit,
        /// transparent, depth writes on) over the texture; a material is used as given (its _BaseMap is the painting).
        bool TryGetSurface(string planetId, out Texture texture, out Material material);
    }
}
