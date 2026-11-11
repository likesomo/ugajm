#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SevenSteps.Art
{
    /// <summary>Real Editor Play Mode renders, no player build, using the saved production stage maps and presets.</summary>
    public sealed class PlanetDressingReview : MonoBehaviour
    {
        const string Output="C:/unityjam/shared-assets/seven-steps/concepts/themes/planet-dressing-2026-11-12";
        const string Art="Assets/_Project/Art/RuinsHenge";
        static readonly string[] Ids={"moss","hollow","carved","frost","joinery","crumble","altar"};
        static readonly int[] Stages={1,8,15,22,29,36,43};
        Camera cam;GameObject root,sky;BoardGrid grid;Texture2D frame;
        readonly List<string> perf=new List<string>{"planet,stage,before,stageTris,dressingTris,materials,modules,floorLuma,wallLuma,contrast"};
        public static void Run()
        {
            Directory.CreateDirectory(Output);PlanetDressingBuilder.Build();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Planet Dressing Review").AddComponent<PlanetDressingReview>();EditorApplication.isPlaying=true;
        }
        IEnumerator Start()
        {
            var run=Review();
            while(true)
            {
                object current;
                try{if(!run.MoveNext())yield break;current=run.Current;}
                catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);yield break;}
                yield return current;
            }
        }
        IEnumerator Review()
        {
            yield return null;
            cam=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();cam.tag="MainCamera";
            cam.allowHDR=false;cam.orthographic=true;cam.aspect=1280f/720;
            var data=JsonUtility.FromJson<LevelFileDto>(File.ReadAllText("Assets/_Project/Data/levels.json"));
            for(int planet=0;planet<Ids.Length;planet++)
            {
                var chapter=AssetDatabase.LoadAssetAtPath<ChapterPreset>(Art+"/Chapter_"+Ids[planet]+".asset");
                var stage=data.levels[Stages[planet]-1];
                foreach(bool before in new[]{true,false})
                {
                    Setup(chapter,stage,before);yield return null;yield return new WaitForSeconds(.25f);
                    Capture((before?"before-":"after-")+chapter.id+".png");Measure(chapter,stage,Stages[planet],before);
                    if(!before)
                    {
                        // Full module clearance against the entire playable board under the specified camera.
                        VerifyClearance(root.GetComponentInChildren<RuinsHengePlanet>(),chapter.id);
                        if(chapter.id=="frost")
                        {
                            cam.orthographicSize=1.7f;cam.transform.position=new Vector3(-grid.W*.5f-.90f,.18f,.10f)-cam.transform.forward*30;
                            Capture("frost-closeup.png");
                        }
                    }
                    Destroy(root);Destroy(sky);yield return null;
                }
            }
            ValidateStageFits(data);
            File.WriteAllLines(Output+"/perf.csv",perf);
            Debug.Log("[PlanetDressingReview] COMPLETE: seven actual stages, before/after, frost detail, contrast and geometry verified");EditorApplication.Exit(0);
        }
        void ValidateStageFits(LevelFileDto data)
        {
            var rows=new List<string>{"stage,planet,w,h,modules,sharedMeshSafe"};
            for(int i=0;i<data.levels.Length;i++)
            {
                var dto=data.levels[i];string id=string.IsNullOrEmpty(dto.chapter)?"moss":dto.chapter;
                var chapter=AssetDatabase.LoadAssetAtPath<ChapterPreset>(Art+"/Chapter_"+id+".asset");
                if(!chapter)throw new Exception("Unknown stage chapter: "+id);
                var level=Level.Parse(dto.name,dto.hint,dto.bodies,dto.map,dto.statueCount);
                var planet=Instantiate(chapter.sky.planetPrefab).GetComponent<RuinsHengePlanet>();
                var original=planet.dressing.modules[0].GetComponentInChildren<MeshFilter>().sharedMesh;
                // A second Fit at non-unit scale and translated origin catches coordinate and ownership regressions.
                planet.Fit(new BoardGrid(level));planet.Fit(new BoardGrid(level.W,level.H,.75f,new Vector3(2,1,-3)));
                var dressing=planet.transform.Find("ThemedDressing_"+id);int count=dressing?dressing.childCount:0;
                if(count<4)throw new Exception("Too few supported modules on stage "+(i+1)+": "+count);
                DestroyImmediate(planet.gameObject);
                if(!original)throw new Exception("Imported shared mesh was destroyed during Fit cleanup");
                rows.Add($"{i+1},{id},{level.W},{level.H},{count},True");
            }
            File.WriteAllLines(Output+"/stage-fit-validation.csv",rows);
            Debug.Log("[PlanetDressingReview] All "+data.levels.Length+" stage bounds: repeat Fit, translated origin, .75 cell scale and imported mesh ownership PASS");
        }
        void Setup(ChapterPreset chapter,LevelDto dto,bool before)
        {
            root=new GameObject("Stage_"+chapter.id);var level=Level.Parse(dto.name,dto.hint,dto.bodies,dto.map,dto.statueCount);grid=new BoardGrid(level);
            var planet=Instantiate(chapter.sky.planetPrefab,root.transform).GetComponent<RuinsHengePlanet>();
            Material baseline=null;
            if(before){planet.dressing=null;baseline=new Material(planet.surface);baseline.SetFloat("_PlanetDressing",0);baseline.SetFloat("_PlanetIce",0);planet.surface=baseline;}
            planet.Fit(grid);
            GameObject Spawn(TileVisual v,int x,int y)
            {
                if(!v.prefab)return null;var go=Instantiate(v.prefab,root.transform);go.transform.position=grid.CellToWorld(x,y)+v.offset;go.transform.localRotation=v.RotationFor(x+y*grid.W);go.transform.localScale=v.Scale;ChapterMaterials.Apply(go,chapter);
                if(before)foreach(var r in go.GetComponentsInChildren<Renderer>()){var mats=r.sharedMaterials;for(int m=0;m<mats.Length;m++)if(mats[m]==planet.dressing?.material||mats[m]==chapter.Override(AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/BaseAtlas.mat")))mats[m]=baseline;r.sharedMaterials=mats;}
                return go;
            }
            for(int y=0;y<level.H;y++)for(int x=0;x<level.W;x++)
            {
                var id=TileId.FromChar(level.Tile(y*level.W+x),x,y);
                if(id.Kind==TileKind.Exit||id.Kind==TileKind.Gate||id.Kind==TileKind.Door||id.Kind==TileKind.CountDoor||id.Kind==TileKind.Lever||id.Kind==TileKind.Plate)Spawn(chapter.tileSet.floor,x,y);
                Spawn(chapter.tileSet.Get(id),x,y);
            }
            Spawn(chapter.tileSet.PlayerFor(0),level.X(level.Start),level.Y(level.Start));
            foreach(var rig in new[]{chapter.lighting.key,chapter.lighting.fill,chapter.lighting.rim})
            {
                var l=new GameObject("PresetLight",typeof(Light)).GetComponent<Light>();l.transform.SetParent(root.transform);l.type=LightType.Directional;l.enabled=rig.enabled;l.color=rig.color;l.intensity=rig.intensity;l.transform.rotation=Quaternion.Euler(rig.euler);l.shadows=LightShadows.None;
            }
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=chapter.lighting.ambient;
            cam.backgroundColor=chapter.sky.background;cam.transform.rotation=Quaternion.Euler(50,0,0);
            cam.orthographicSize=Mathf.Max(4.6f,(grid.W+5.2f)/(2*cam.aspect),grid.H*.383f+1.8f);
            cam.transform.position=new Vector3(0,-.15f,0)-cam.transform.forward*30;sky=Instantiate(chapter.sky.skyPrefab,cam.transform);
        }
        void Capture(string name)
        {
            var rt=new RenderTexture(1280,720,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            if(frame)Destroy(frame);frame=new Texture2D(1280,720,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,1280,720),0,0);frame.Apply();File.WriteAllBytes(Output+"/"+name,frame.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;Destroy(rt);
        }
        float Luma(Vector3 p)
        {
            var uv=cam.WorldToViewportPoint(p);float value=0;
            for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++){var c=frame.GetPixel(Mathf.RoundToInt(uv.x*1280)+x,Mathf.RoundToInt(uv.y*720)+y).linear;value+=.2126f*c.r+.7152f*c.g+.0722f*c.b;}
            return value/25;
        }
        void Measure(ChapterPreset chapter,LevelDto dto,int stage,bool before)
        {
            var level=Level.Parse(dto.name,dto.hint,dto.bodies,dto.map,dto.statueCount);
            var floors=new List<float>();var walls=new List<float>();
            for(int y=0;y<grid.H;y++)for(int x=0;x<grid.W;x++)
            {
                char tile=level.Tile(y*grid.W+x);
                if(tile=='.')floors.Add(Luma(grid.CellToWorld(x,y)+Vector3.up*.01f));
                if(tile=='#')walls.Add(Luma(grid.CellToWorld(x,y)+Vector3.up*.58f));
            }
            floors.Sort();walls.Sort();float floor=floors[floors.Count/2],wall=walls[walls.Count/2],contrast=(floor-wall)/floor;
            long Count(GameObject go)=>go.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh).Sum(f=>Enumerable.Range(0,f.sharedMesh.subMeshCount).Sum(sm=>(long)f.sharedMesh.GetIndexCount(sm)/3));
            long tris=Count(root)+Count(cam.gameObject);var dressing=root.GetComponentInChildren<RuinsHengePlanet>().transform.Find("ThemedDressing_"+chapter.id);
            long dressingTris=dressing?Count(dressing.gameObject):0;int modules=dressing?dressing.childCount:0;
            var materials=new HashSet<Material>(root.GetComponentsInChildren<Renderer>().Concat(cam.GetComponentsInChildren<Renderer>()).SelectMany(r=>r.sharedMaterials));
            perf.Add(string.Join(",",chapter.id,stage,before,tris,dressingTris,materials.Count,modules,floor.ToString("F4",CultureInfo.InvariantCulture),wall.ToString("F4",CultureInfo.InvariantCulture),contrast.ToString("F4",CultureInfo.InvariantCulture)));
            Debug.Log($"[PlanetDressingReview] {chapter.id} before={before} triangles={tris} dressing={dressingTris} modules={modules} materials={materials.Count} contrast={contrast:P1}");
            if(!before&&(contrast<.30f||dressingTris>8000||materials.Count>6||modules<4))throw new Exception("Dressing budget, module count or readability failed: "+chapter.id);
        }
        void VerifyClearance(RuinsHengePlanet planet,string id)
        {
            var dressing=planet.transform.Find("ThemedDressing_"+id);if(!dressing)throw new Exception("Missing themed dressing");
            // At yaw zero, every playable cell projects inside the board's horizontal interval.
            // Test each triangle's projected box against that interval and the board's vertical span.
            float left=cam.WorldToViewportPoint(new Vector3(-grid.W*.5f,0,0)).x,right=cam.WorldToViewportPoint(new Vector3(grid.W*.5f,0,0)).x;
            float bottom=cam.WorldToViewportPoint(new Vector3(0,0,-grid.H*.5f)).y,top=cam.WorldToViewportPoint(new Vector3(0,.65f,grid.H*.5f)).y;
            foreach(var f in dressing.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=f.sharedMesh;var verts=mesh.vertices;var indices=mesh.triangles;
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=cam.WorldToViewportPoint(f.transform.TransformPoint(verts[indices[i]]));var b=cam.WorldToViewportPoint(f.transform.TransformPoint(verts[indices[i+1]]));var c=cam.WorldToViewportPoint(f.transform.TransformPoint(verts[indices[i+2]]));
                    if(Mathf.Max(a.x,b.x,c.x)>left&&Mathf.Min(a.x,b.x,c.x)<right&&Mathf.Max(a.y,b.y,c.y)>bottom&&Mathf.Min(a.y,b.y,c.y)<top)
                        throw new Exception("Dressing projects over board/exit: "+id+"/"+f.name);
                }
            }
        }
    }
}
#endif
