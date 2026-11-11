using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;
using SevenSteps.Game.UI.Planets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SevenSteps.EditorTools
{
    /// <summary>
    /// Review captures of the v4 stage select built from the production views (PlanetMap3DView, PlanetSurfaceView) with
    /// fixed save fixtures, 1280x720, for comparison with concepts/ui/stage-select-2026-11-11/v3, v4, v4b. Needs graphics:
    /// unity.sh stageselect-capture (no -nographics). Writes reports/stage-select/*.png and a short log.
    /// </summary>
    public static class StageSelectCapture
    {
        const int W = 1280, H = 720;

        [MenuItem("Seven Steps/Gameplay/Capture stage select v4")]
        public static void Run() => Capture(false);

        public static void Batch()
        {
            try { Capture(true); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// A world-space canvas covering the orthographic view 1 unit in front of the camera, 1280x720 units.
        static void PlaceCanvas(Camera cam, RectTransform canvas)
        {
            canvas.sizeDelta = new Vector2(W, H);
            canvas.pivot = new Vector2(0.5f, 0.5f);
            canvas.SetPositionAndRotation(cam.transform.position + cam.transform.forward * (cam.nearClipPlane + 0.5f), cam.transform.rotation);
            canvas.localScale = Vector3.one * (2 * cam.orthographicSize / H);
        }

        static void Capture(bool batch)
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/stage-select"));
            Directory.CreateDirectory(outDir);
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            if (assets == null || assets.planetBodies == null) throw new InvalidOperationException("GameAssets / planetBodies missing");
            Ui.Theme = assets.uiTheme;
            var levels = LevelCatalog.Load(assets);
            var planets = PlanetEntry.FromCatalog(levels.Length);
            var tables = assets.strings.Where(a => a.json != null).Select(a => StringTable.FromJson(a.language, a.json.text)).ToList();
            var keys = planets.Select(p => p.Key).ToList();
            var story = JsonUtility.FromJson<StoryFileDto>(assets.storyJson.text);
            var trails = StageSelectModel.TrailsByKey(keys, story.dipper.Select(d => (d.from, d.to)));
            var stages = planets.Select(p => p.Stages).ToList();

            StageSelectModel Fixture(int completePlanets, int extraOnNext, int markerPlanet, int markerSlot)
            {
                var cleared = new HashSet<int>();
                for (int p = 0; p < planets.Count; p++)
                    for (int s = 0; s < stages[p].Count; s++)
                        if (p < completePlanets || (p == completePlanets && s < extraOnNext)) cleared.Add(stages[p][s]);
                int last = markerPlanet >= 0 ? stages[markerPlanet][markerSlot] : -1;
                return StageSelectModel.From(keys, stages, cleared.Contains, last, trails);
            }

            var log = new List<string> { $"Unity {Application.unityVersion}; {W}x{H}; production PlanetMap3DView / PlanetSurfaceView; fixtures below" };
            foreach (var lang in new[] { Language.Ko, Language.En })
            {
                var loc = new Localizer(tables, lang);
                string code = Languages.Code(lang);
                void Shot(string name, Action<PlanetMap3DView, PlanetSurfaceView> pose, string fixture)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    assets = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset"); // a new scene may unload it
                    Ui.Theme = assets.uiTheme;
                    var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                    var camGo = new GameObject("Main Camera");
                    var cam = camGo.AddComponent<Camera>();
                    cam.targetTexture = rt;
                    cam.orthographic = true;
                    cam.nearClipPlane = 0.3f;
                    cam.farClipPlane = 1000f;
                    cam.backgroundColor = assets.background;
                    var uiGo = new GameObject("TitleUI", typeof(RectTransform));
                    uiGo.layer = 5;
                    var canvas = uiGo.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.WorldSpace; // the game uses an overlay; a batch capture puts the canvas on the view plane (PlaceCanvas)
                    canvas.worldCamera = cam;
                    canvas.scaleFactor = 1f;
                    var space = new PlanetSpace(cam, assets.background);
                    var map = new PlanetMap3DView(uiGo.transform, space, assets.planetBodies, planets);
                    var surface = new PlanetSurfaceView(uiGo.transform, space, assets.planetBodies, planets, map.SetBodiesVisible);
                    map.Apply(loc);
                    surface.Apply(loc);
                    PlaceCanvas(cam, (RectTransform)uiGo.transform);
                    Canvas.ForceUpdateCanvases();
                    pose(map, surface);
                    PlaceCanvas(cam, (RectTransform)uiGo.transform); // on the posed camera, then lay out again on it
                    Canvas.ForceUpdateCanvases();
                    pose(map, surface);
                    Canvas.ForceUpdateCanvases();
                    cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    string file = Path.Combine(outDir, $"{name}-{code}.png");
                    File.WriteAllBytes(file, tex.EncodeToPNG());
                    int tris = Object.FindObjectsByType<MeshFilter>().Where(f => f.gameObject.activeInHierarchy && f.sharedMesh != null).Sum(f => f.sharedMesh.triangles.Length / 3);
                    int mats = Object.FindObjectsByType<MeshRenderer>().Where(r => r.gameObject.activeInHierarchy).SelectMany(r => r.sharedMaterials).Distinct().Count();
                    log.Add($"{name}-{code}: {fixture}; live mesh tris {tris}, mesh materials {mats}");
                    Object.DestroyImmediate(tex);
                    cam.targetTexture = null;
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }

                var fresh = Fixture(0, 0, -1, 0);
                Shot("map-default", (m, s) => m.Show(fresh, 0, -1), "fresh save");
                Shot("map-focused", (m, s) => { m.Show(fresh, 0, -1); m.Preview(0); }, "fresh save, moss focused");
                var mid = Fixture(3, 2, 3, 2);
                Shot("map-mid-game", (m, s) => { m.Show(mid, 3, -1); m.Preview(3); }, "moss/hollow/carved complete, frost 2 cleared, brothers on frost, frost focused");
                var all = Fixture(planets.Count, 0, planets.Count - 1, stages[planets.Count - 1].Count - 1);
                Shot("map-all-complete", (m, s) => m.Show(all, planets.Count - 1, -1), "every stage cleared");
                Shot("surface-moss-default", (m, s) => { m.Show(fresh, 0, -1); s.Bind(fresh, 0); s.Show(); s.Preview(-1); }, "fresh save");
                var mix = Fixture(0, 3, 0, 3);
                Shot("surface-moss-cleared-mix", (m, s) => { m.Show(mix, 0, -1); s.Bind(mix, 0); s.Show(); s.Preview(3); }, "moss 1-3 cleared, place 4 current and focused");
                var altar = Fixture(planets.Count - 1, 4, planets.Count - 1, 4);
                Shot("surface-altar-cleared-mix", (m, s) => { m.Show(altar, planets.Count - 1, -1); s.Bind(altar, planets.Count - 1); s.Show(); s.Preview(4); }, "altar 1-4 cleared, place 5 current and focused");
            }
            File.WriteAllLines(Path.Combine(outDir, "capture.txt"), log);
            foreach (var l in log) Debug.Log("[SevenSteps] Stage select capture " + l);
            if (!batch) EditorUtility.RevealInFinder(outDir);
        }
    }
}
