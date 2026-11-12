using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Art.Map;
using SevenSteps.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SevenSteps.Tests.EditMode.Game
{
    public sealed class PlanetSurfaceSetTests
    {
        const string SetPath = "Assets/_Project/Data/PlanetSurfaceSet.asset";
        const string TextureRoot = "Assets/Shared/Art/Map/PlanetsV7Surface/";
        static readonly string[] PlanetIds = { "moss", "hollow", "carved", "frost", "joinery", "crumble", "altar" };

        [Test]
        public void EveryPlanetProjectionAndMaterialReferenceResolves()
        {
            var set = AssetDatabase.LoadAssetAtPath<PlanetSurfaceSet>(SetPath);
            Assert.IsNotNull(set, "Planet surface mapping asset");
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            Assert.IsNotNull(assets);
            Assert.AreSame(set, assets.planetSurfaceArt, "GameAssets should expose the surface mapping through scene-flow's contract");
            var variants = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>("Assets/_Project/Art/Map/PlanetSurfaceProjection.shadervariants");
            Assert.IsNotNull(variants, "WebGL surface shader variants");
            Assert.Greater(variants.shaderCount, 0);
            var graphicsSettings = System.IO.File.ReadAllText("ProjectSettings/GraphicsSettings.asset");
            Assert.IsTrue(graphicsSettings.Contains(AssetDatabase.AssetPathToGUID("Assets/_Project/Art/Map/PlanetSurfaceProjection.shadervariants")),
                "surface variant collection must be preloaded for player builds");
            Assert.AreEqual(PlanetIds.Length, set.entries.Length);
            var found = new HashSet<string>();
            var materials = new HashSet<Material>();

            foreach (string id in PlanetIds)
            {
                Assert.IsTrue(set.TryGetSurface(id, out var texture, out var material), "mapping for " + id);
                Assert.IsTrue(found.Add(id), "duplicate planet id " + id);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + id + ".png"), texture);
                var projection = texture as Texture2D;
                Assert.IsNotNull(projection);
                Assert.AreEqual(1024, projection.width);
                Assert.AreEqual(1024, projection.height);
                Assert.IsNotNull(material);
                Assert.AreEqual("SevenSteps/PlanetSurfaceProjection", material.shader.name);
                Assert.AreSame(texture, material.GetTexture("_BaseMap"));
                Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, material.renderQueue);
                Assert.IsTrue(materials.Add(material), "each planet owns a material so _BaseMap is stable: " + id);

                var importer = AssetImporter.GetAtPath(TextureRoot + id + ".png") as TextureImporter;
                Assert.IsNotNull(importer);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
                Assert.IsTrue(importer.alphaIsTransparency);
                Assert.IsTrue(importer.mipmapEnabled);
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
                Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
                Assert.AreEqual(4, importer.anisoLevel);
                Assert.AreEqual(TextureImporterCompression.CompressedHQ, importer.textureCompression);
                var webgl = importer.GetPlatformTextureSettings("WebGL");
                Assert.IsTrue(webgl.overridden);
                Assert.AreEqual(1024, webgl.maxTextureSize);
                Assert.AreEqual(TextureImporterCompression.CompressedHQ, webgl.textureCompression);
                var meta = System.IO.File.ReadAllBytes(TextureRoot + id + ".png.meta");
                Assert.Greater(meta.Length, 0);
                Assert.AreEqual((byte)10, meta[meta.Length - 1], id + " .meta must end with newline");
            }
        }
    }
}
