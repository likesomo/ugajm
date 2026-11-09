using System.Collections.Generic;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// <summary>
    /// Chapter look (docs/architecture.md 12.8): a new chapter swaps sky and planet and copies the three lights, the same
    /// chapter keeps them (only Fit), and the board's tile set comes from AppConfig.tileSet, then the chapter, then
    /// GameAssets. Presets are built in memory, so no art is needed. Owner: gameplay/UI.
    /// </summary>
    public sealed class ChapterStageTests
    {
        readonly List<Object> made = new List<Object>();

        T Make<T>(T o) where T : Object { made.Add(o); return o; }

        [TearDown]
        public void CleanUp()
        {
            foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        sealed class FitProbe : MonoBehaviour, IStageEnvironment
        {
            public static readonly List<BoardGrid> Fits = new List<BoardGrid>();
            public void Fit(BoardGrid grid) => Fits.Add(grid);
        }

        ChapterPreset Preset(string id, Color key, Color background)
        {
            var lighting = Make(ScriptableObject.CreateInstance<LightingPreset>());
            lighting.key = new LightRig { enabled = true, color = key, intensity = 1.5f, euler = new Vector3(40, 10, 0), shadows = true };
            lighting.fill = new LightRig { enabled = true, color = Color.blue, intensity = 0.4f, euler = new Vector3(20, 150, 0), shadows = true };
            lighting.rim = new LightRig { enabled = false, color = Color.red, intensity = 0.2f, euler = new Vector3(10, 90, 0) };
            var sky = Make(ScriptableObject.CreateInstance<SkyPreset>());
            sky.background = background;
            sky.skyPrefab = Make(new GameObject($"Sky {id}"));
            var planet = Make(new GameObject($"Planet {id}"));
            planet.AddComponent<FitProbe>();
            sky.planetPrefab = planet;
            var p = Make(ScriptableObject.CreateInstance<ChapterPreset>());
            p.id = id;
            p.lighting = lighting;
            p.sky = sky;
            return p;
        }

        [Test]
        public void NewChapterSwapsSkyPlanetAndLightsSameChapterOnlyFits()
        {
            FitProbe.Fits.Clear();
            var cam = Make(new GameObject("Camera")).AddComponent<Camera>();
            Light L(string n) => Make(new GameObject(n)).AddComponent<Light>();
            Light key = L("Key"), fill = L("Fill"), rim = L("Rim");
            var root = Make(new GameObject("Root")).transform;
            var stage = new ChapterStage(cam, key, fill, rim, root);
            var moss = Preset("moss", Color.green, Color.black);
            var hollow = Preset("hollow", Color.yellow, Color.gray);

            Assert.IsTrue(stage.Enter(moss), "first chapter");
            Assert.AreEqual(Color.green, key.color);
            Assert.AreEqual(1.5f, key.intensity);
            Assert.AreEqual(LightShadows.Soft, key.shadows);
            Assert.AreEqual(LightShadows.None, fill.shadows, "shadows on the key light only");
            Assert.IsFalse(rim.enabled);
            Assert.AreEqual(Color.black, cam.backgroundColor);
            Assert.AreEqual(1, cam.transform.childCount, "sky under the camera");
            Assert.AreEqual(1, root.childCount, "planet under the scene root");
            stage.Fit(new BoardGrid(5, 4));
            Assert.AreEqual(1, FitProbe.Fits.Count);

            var skyBefore = cam.transform.GetChild(0).gameObject;
            Assert.IsFalse(stage.Enter(moss), "same chapter: nothing to swap");
            Assert.AreSame(skyBefore, cam.transform.GetChild(0).gameObject);
            stage.Fit(new BoardGrid(7, 6));
            Assert.AreEqual(2, FitProbe.Fits.Count, "same chapter still fits the next stage");

            Assert.IsTrue(stage.Enter(hollow));
            Assert.AreEqual(Color.yellow, key.color);
            Assert.AreEqual(Color.gray, cam.backgroundColor);
            Assert.AreEqual(1, cam.transform.childCount, "old sky gone");
            Assert.AreEqual(1, root.childCount, "old planet gone");
            StringAssert.StartsWith("Sky hollow", cam.transform.GetChild(0).name);

            Assert.IsTrue(stage.Enter(null), "no chapter: instances removed");
            Assert.AreEqual(0, cam.transform.childCount);
            Assert.AreEqual(0, root.childCount);
            stage.Dispose();
        }

        /// Builds a child renderer with the shared base material on Fit, like the planet summit rebuilds its meshes.
        sealed class RebuildOnFit : MonoBehaviour, IStageEnvironment
        {
            public Material surface;
            public void Fit(BoardGrid grid)
            {
                var child = new GameObject("Summit", typeof(MeshRenderer));
                child.transform.SetParent(transform, false);
                child.GetComponent<MeshRenderer>().sharedMaterial = surface;
            }
        }

        [Test]
        public void MaterialOverridesReachSkyAndPlanetAlsoAfterFit()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var baseMat = Make(new Material(shader) { name = "Base" });
            var frostMat = Make(new Material(shader) { name = "Frost" });
            var preset = Preset("frost", Color.white, Color.black);
            preset.materialOverrides = new[] { new MaterialOverride { from = baseMat, to = frostMat } };
            preset.sky.skyPrefab.AddComponent<MeshRenderer>().sharedMaterial = baseMat;
            Object.DestroyImmediate(preset.sky.planetPrefab.GetComponent<FitProbe>());
            preset.sky.planetPrefab.AddComponent<MeshRenderer>().sharedMaterial = baseMat;
            preset.sky.planetPrefab.AddComponent<RebuildOnFit>().surface = baseMat;

            var cam = Make(new GameObject("Camera")).AddComponent<Camera>();
            Light L(string n) => Make(new GameObject(n)).AddComponent<Light>();
            var root = Make(new GameObject("Root")).transform;
            var stage = new ChapterStage(cam, L("Key"), L("Fill"), L("Rim"), root);
            stage.Enter(preset);
            Assert.AreSame(frostMat, cam.transform.GetChild(0).GetComponent<MeshRenderer>().sharedMaterial, "sky");
            var planet = root.GetChild(0);
            Assert.AreSame(frostMat, planet.GetComponent<MeshRenderer>().sharedMaterial, "planet");
            stage.Fit(new BoardGrid(4, 4));
            Assert.AreSame(frostMat, planet.Find("Summit").GetComponent<MeshRenderer>().sharedMaterial, "meshes built in Fit");
            stage.Dispose();
        }

        [Test]
        public void TileSetOrderIsOverrideThenChapterThenAssets()
        {
            var over = Make(ScriptableObject.CreateInstance<TileSet>());
            var chapterSet = Make(ScriptableObject.CreateInstance<TileSet>());
            var fallback = Make(ScriptableObject.CreateInstance<TileSet>());
            var chapter = Make(ScriptableObject.CreateInstance<ChapterPreset>());
            chapter.tileSet = chapterSet;
            var bare = Make(ScriptableObject.CreateInstance<ChapterPreset>());

            Assert.AreSame(over, BoardController.TileSetFor(over, chapter, fallback));
            Assert.AreSame(chapterSet, BoardController.TileSetFor(null, chapter, fallback));
            Assert.AreSame(fallback, BoardController.TileSetFor(null, bare, fallback));
            Assert.AreSame(fallback, BoardController.TileSetFor(null, null, fallback));
            Assert.Throws<System.InvalidOperationException>(() => BoardController.TileSetFor(null, bare, null));
        }
    }
}
