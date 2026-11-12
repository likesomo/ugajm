using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Game.UI.Planets;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// <summary>
    /// V7 map planets (user choice 2026-11-12): a map body drawn as its sprite keeps its transform and radius (the map
    /// layout, targets and the journey dive read those), hides its mesh, sizes the planet to the body's diameter and takes
    /// the map tint. Owner: scene-flow.
    /// </summary>
    public sealed class PlanetSpriteTests
    {
        readonly List<Object> made = new List<Object>();
        T Make<T>(T o) where T : Object { made.Add(o); return o; }

        [TearDown]
        public void CleanUp()
        {
            foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }


        PlanetBody Body(string key)
        {
            var set = Make(ScriptableObject.CreateInstance<PlanetBodySet>());
            set.material = Make(new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")));
            var parent = Make(new GameObject("World")).transform;
            var body = PlanetBody.Create(set, key, 0, parent, seated: false);
            body.transform.localScale = Vector3.one * PlanetMapLayout.BodyScale;
            return body;
        }

        Sprite Square(int px)
        {
            var tex = Make(new Texture2D(px, px));
            return Make(Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 100));
        }

        [Test]
        public void SpriteReplacesTheMeshAtTheBodysSize()
        {
            var body = Body("moss");
            float radius = body.Radius;
            var cam = Make(new GameObject("Camera")).AddComponent<Camera>();
            var sprite = Square(512);
            body.UseSprite(sprite, cam);

            Assert.IsFalse(body.Renderer.enabled, "the mesh no longer draws");
            Assert.IsNotNull(body.Impostor);
            Assert.AreSame(sprite, body.Impostor.sprite);
            Assert.AreEqual(radius, body.Radius, 1e-5f, "same radius for the layout and the dive");
            float drawn = body.Impostor.bounds.size.x * PlanetBody.SpriteFill;
            Assert.AreEqual(2f * radius, drawn, 0.01f, "the planet is as wide as the body");

            var dim = new Color(0.7f, 0.72f, 0.78f, 1f);
            body.SetTint(dim);
            Assert.AreEqual(dim, body.Impostor.color, "unvisited planets dim on the map");
            Assert.Throws<System.InvalidOperationException>(() => body.UseSprite(sprite, cam), "once per body");
            Assert.Throws<System.ArgumentNullException>(() => Body("hollow").UseSprite(null, cam));
        }

        /// The shipped set (GameAssets.planetMapSprites) gives a sprite for every chapter with stages. Its references once
        /// pointed at "*_0" sub-sprites that a Single-mode import does not have (sprite id 21300000), so every lookup failed
        /// and the map silently kept the meshes. Needs the shared assets junction (tools/link-shared-assets.ps1).
        [Test]
        public void ShippedSetHasASpriteForEveryPlanet()
        {
            var assets = UnityEditor.AssetDatabase.LoadAssetAtPath<SevenSteps.Game.GameAssets>("Assets/_Project/Data/GameAssets.asset");
            Assert.IsNotNull(assets, "Data/GameAssets.asset");
            var sprites = assets.PlanetMapSprites;
            Assert.IsNotNull(sprites, "GameAssets.planetMapSprites is set (V7 map planets)");
            var levels = SevenSteps.Game.LevelCatalog.Load(assets);
            foreach (var p in SevenSteps.Game.UI.PlanetEntry.FromCatalog(levels.Length))
                Assert.IsTrue(sprites.TryGetSprite(p.Key, out var s) && s != null, $"a V7 sprite for planet {p.Key}");
        }
    }
}
