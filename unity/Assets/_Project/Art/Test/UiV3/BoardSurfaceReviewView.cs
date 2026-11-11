using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using UnityEngine;
using UnityEngine.UI;
namespace SevenSteps.Art.Review
{
    // Actual saved BoardShapeA meshes and chapter material swaps, isolated on layer 30.
    public sealed class BoardSurfaceReviewView
    {
        readonly ReviewChoiceController v; GameObject root; Camera camera; RenderTexture texture; string key; public readonly Rect Bounds = new Rect(72, 155, 690, 430); BoardGrid grid;
        public BoardSurfaceReviewView(ReviewChoiceController view)
        {
            v = view;
        }
        public Vector2 Project(int cell)
        {
            var p = camera.WorldToViewportPoint(grid.CellToWorld(cell));
            return Bounds.position + new Vector2(p.x * Bounds.width, p.y * Bounds.height);
        }
        public void Draw(State s)
        {
            if (key != v.planet + s.Level.Name)
            {
                key = v.planet + s.Level.Name;
                if (root)
                    Object.DestroyImmediate(root);
                root = new GameObject("Actual BoardShapeA review");
                var chapter = System.Array.Find(v.chapters, c => c && c.id == v.planet);
                if (!chapter)
                    chapter = v.chapters[0];
                grid = new BoardGrid(s.Level);
                var set = chapter.tileSet;
                void Spawn(TileVisual visual, int cell)
                {
                    if (!visual.prefab)
                        return;
                    var g = Object.Instantiate(visual.prefab, root.transform);
                    g.transform.position = grid.CellToWorld(cell) + visual.offset;
                    g.transform.localScale = visual.Scale;
                    g.transform.rotation = visual.RotationFor(cell);
                    ChapterMaterials.Apply(g, chapter);
                }
                for (int i = 0; i < s.Level.Count; i++)
                {
                    char t = s.Level.Tile(i);
                    Spawn(t == '#' ? set.wall : t == 'o' ? set.pit : (i % 2 == 0 ? set.floor : set.floorAlt), i);
                    if (t == 'E')
                        Spawn(set.exit, i);
                }
                var planet = Object.Instantiate(chapter.sky.planetPrefab, root.transform).GetComponent<RuinsHengePlanet>();
                planet.Fit(grid);
                foreach (var rig in new[] { chapter.lighting.key, chapter.lighting.fill, chapter.lighting.rim })
                {
                    var l = new GameObject("Review light", typeof(Light)).GetComponent<Light>();
                    l.transform.SetParent(root.transform);
                    l.type = LightType.Directional;
                    l.enabled = rig.enabled;
                    l.color = rig.color;
                    l.intensity = rig.intensity;
                    l.transform.rotation = Quaternion.Euler(rig.euler);
                    l.cullingMask = 1 << 30;
                    l.shadows = LightShadows.None;
                }
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = chapter.lighting.ambient;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = 30;
                if (!camera)
                {
                    camera = new GameObject("Board render camera", typeof(Camera)).GetComponent<Camera>();
                    camera.cullingMask = 1 << 30;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.orthographic = true;
                    camera.nearClipPlane = .1f;
                    camera.farClipPlane = 100;
                    camera.transform.rotation = Quaternion.Euler(50, 0, 0);
                    camera.transform.position = new Vector3(0, -.8f, 0) - camera.transform.forward * 25;
                    texture = new RenderTexture(1380, 860, 24);
                    camera.targetTexture = texture;
                }
                camera.backgroundColor = chapter.sky.background;
                camera.orthographicSize = Mathf.Max(4.4f, (s.Level.W + 3.4f) / (2 * camera.aspect));
                Camera.main.cullingMask &= ~(1 << 30);
            }
            camera.Render();
            var go = v.Box("Actual Unity board", Bounds, Color.white);
            Object.DestroyImmediate(go.GetComponent<Image>());
            var image = go.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
        }
    }
}
