using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace SevenSteps.Art.StageSelectReview
{
    // Editor-only presentation study. Never changes scenes, save data, or production UI.
    // Planet thumbnails are rendered here from saved prefabs, packed once, then presented
    // by Unity UI. This keeps seven large worlds below the scene geometry/material budget.
    public static partial class Capture
    {
        static string Output = "C:/unityjam/shared-assets/seven-steps/concepts/ui/stage-select-2026-11-11/v2";
        static readonly string[] Ids = {"moss","hollow","carved","frost","joinery","crumble","altar"};
        static readonly Vector2[] A = {new Vector2(150,550),new Vector2(150,290),new Vector2(410,240),new Vector2(430,520),new Vector2(700,490),new Vector2(920,330),new Vector2(1120,180)};
        static readonly Vector2[] B = {new Vector2(220,475),new Vector2(430,485),new Vector2(470,285),new Vector2(245,285),new Vector2(700,335),new Vector2(920,410),new Vector2(1110,505)};
        static readonly Color Gold = new Color32(245,218,161,255), Quiet = new Color32(115,144,162,255);
        static Camera cam;
        static Canvas canvas;
        static GameObject world;
        static ChapterPreset[] chapters;
        static GameAssets assets;
        static Level[] levels;
        static List<PlanetEntry> planets;
        static Font font;
        static Sprite[] worlds;
        static Sprite thinRing;
        static readonly List<string> perf = new List<string>{"capture,liveMeshTris,meshRenderers,uniqueMeshMaterials,uiVertices,uiMaterials,estimatedUnbatchedDrawUpperBound,unityStatsDrawCalls"};
        static readonly List<string> targets = new List<string>{"planet,state,slot,visibleWidthPx,visibleHeightPx,targetPx"};
        static string direction, language;
        [Serializable] class Copy { public CopyChapter[] chapters; }
        [Serializable] class CopyChapter { public string id,intro,tagline; public CopyText en; }
        [Serializable] class CopyText { public string intro,tagline; }
        [Serializable] class Story { public Edge[] dipper; }
        [Serializable] class Edge { public string from,to; }
        static Copy copy;
        static Story story;

        [MenuItem("Seven Steps/Art/Stage Select/Capture mixed v2")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            try
            {
                assets=AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
                Ui.Theme=assets.uiTheme; levels=LevelCatalog.Load(assets); planets=PlanetEntry.FromCatalog(levels.Length);
                chapters=Ids.Select(id=>AssetDatabase.LoadAssetAtPath<ChapterPreset>("Assets/_Project/Art/RuinsHenge/Chapter_"+id+".asset")).ToArray();
                font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Shared/Fonts/Jua-Regular.ttf");
                copy=JsonUtility.FromJson<Copy>(File.ReadAllText("../design/story/copy-levels.json"));
                story=JsonUtility.FromJson<Story>(File.ReadAllText("../design/story/story.json"));
                worlds=BakeWorlds();
                direction="A";language="ko";
                Map("default",-1,0);Map("focused",0,0);Map("mid-game",3,3);Map("all-complete",-1,7);
                direction="B";
                foreach(int p in new[]{0,6}){Stages(p,"default",-1,0);Stages(p,"focused",3,0);Stages(p,"cleared-mix",3,3);}
                direction="A";language="en";Map("focused",0,0);
                File.WriteAllLines(Path.Combine(Output,"performance.csv"),perf);
                File.WriteAllLines(Path.Combine(Output,"stage-targets.csv"),targets);
                File.WriteAllText(Path.Combine(Output,"capture-source.txt"),"Unity "+Application.unityVersion+"; base e2b6932; 1280x720; real saved Chapter_* planet, sky and lighting presets.\nThumbnail cache generated from real Unity rendering, not painted mockups.\n");
                Debug.Log("STAGE_SELECT_CAPTURE_PASS"); EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText(Path.Combine(Output,"failure.txt"),e.ToString());EditorApplication.Exit(1);}
        }
        static void Reset(int p=0,bool sky=true,bool distant=true)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            cam=new GameObject("Review camera",typeof(Camera)).GetComponent<Camera>();cam.tag="MainCamera";
            cam.orthographic=true;cam.orthographicSize=8.7f;cam.aspect=1280f/720;cam.nearClipPlane=.1f;cam.farClipPlane=150;
            cam.transform.rotation=Quaternion.Euler(50,0,0);cam.transform.position=new Vector3(0,-.9f,0)-cam.transform.forward*30;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=chapters[p].sky.background;cam.allowHDR=false;
            cam.targetTexture=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
            world=new GameObject("Review geometry");
            foreach(var rig in new[]{chapters[p].lighting.key,chapters[p].lighting.fill,chapters[p].lighting.rim})
            {var l=new GameObject("Preset lighting",typeof(Light)).GetComponent<Light>();l.type=LightType.Directional;l.enabled=rig.enabled;l.color=rig.color;l.intensity=rig.intensity;l.transform.rotation=Quaternion.Euler(rig.euler);l.shadows=LightShadows.None;}
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=chapters[p].lighting.ambient;
            if(sky)
            {var s=Object.Instantiate(chapters[p].sky.skyPrefab,cam.transform);s.GetComponent<RuinsHengeSky>().distantWorlds=distant;s.SendMessage("Start",SendMessageOptions.DontRequireReceiver);s.SendMessage("LateUpdate",SendMessageOptions.DontRequireReceiver);}
            canvas=new GameObject("Review canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
            new GameObject("EventSystem",typeof(EventSystem));
        }
        static RuinsHengePlanet Planet(int p,int w=5,int h=5,bool summit=true)
        {
            var go=Object.Instantiate(chapters[p].sky.planetPrefab,world.transform);var planet=go.GetComponent<RuinsHengePlanet>();planet.Preview(w,h);
            if(summit)
            {
                var v=chapters[p].tileSet.floor;var grid=new BoardGrid(w,h);
                for(int x=0;x<w;x++)for(int y=0;y<h;y++)
                {var tile=Object.Instantiate(v.prefab,world.transform);tile.transform.position=grid.CellToWorld(x,y)+v.offset;tile.transform.localScale=v.Scale;tile.transform.localRotation=v.RotationFor(x+y*w);ChapterMaterials.Apply(tile,chapters[p]);}
            }
            return planet;
        }
        static Sprite[] BakeWorlds()
        {
            var atlas=new Texture2D(2048,1024,TextureFormat.RGBA32,false);atlas.hideFlags=HideFlags.HideAndDontSave;atlas.SetPixels(new Color[2048*1024]);
            var result=new Sprite[7];var bakePerf=new List<string>{"planet,tris,materials"};
            for(int p=0;p<7;p++)
            {
                Reset(p,false);Object.DestroyImmediate(canvas.gameObject);canvas=null;
                cam.backgroundColor=Color.clear;cam.orthographicSize=6.5f;
                Planet(p);
                cam.transform.rotation=Quaternion.Euler(50,0,0);
                if(!cam.orthographic||Mathf.Abs(cam.transform.eulerAngles.x-50)>.01f||Mathf.Abs(cam.transform.eulerAngles.y)>.01f)throw new Exception("Atlas camera angle mismatch");
                cam.Render();
                var tex=Read();
                // Square source crop: 720x720 out of 1280x720, preserving the game camera's projection.
                int ax=(p%4)*512,ay=(p/4)*512;
                for(int y=0;y<512;y++)for(int x=0;x<512;x++)atlas.SetPixel(ax+x,ay+y,tex.GetPixelBilinear(.5f+(x/511f-.5f)*.5625f,y/511f));
                long triangles=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(f=>f.sharedMesh).Sum(f=>MeshTris(f.sharedMesh));
                bakePerf.Add(Ids[p]+","+triangles+","+Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials).Distinct().Count());
                Object.DestroyImmediate(tex);
            }
            atlas.Apply();File.WriteAllBytes(Path.Combine(Output,"unity-planet-cache.png"),atlas.EncodeToPNG());File.WriteAllLines(Path.Combine(Output,"cache-bake-performance.csv"),bakePerf);
            for(int p=0;p<7;p++){result[p]=Sprite.Create(atlas,new Rect((p%4)*512,(p/4)*512,512,512),Vector2.one*.5f,100);result[p].hideFlags=HideFlags.HideAndDontSave;}
            return result;
        }
        static RectTransform Rect(string name,Vector2 position,Vector2 size,Transform parent=null)
        {var go=new GameObject(name,typeof(RectTransform));var rt=go.GetComponent<RectTransform>();rt.SetParent(parent?parent:canvas.transform,false);rt.anchorMin=rt.anchorMax=Vector2.zero;rt.pivot=Vector2.one*.5f;rt.anchoredPosition=position;rt.sizeDelta=size;return rt;}
        static Text Text(string value,Vector2 pos,int size=24,int width=640,Color? color=null)
        {var t=Rect(value,pos,new Vector2(width,60)).gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.text=value;t.color=color??Gold;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;}
        static Image Image(string name,Vector2 pos,Vector2 size,Sprite sprite,Color color)
        {var i=Rect(name,pos,size).gameObject.AddComponent<Image>();i.sprite=sprite;i.color=color;i.raycastTarget=false;return i;}
        static void Dot(Vector2 pos,float size,Color color)=>Image("Star light",pos,Vector2.one*size,Ui.Circle,color);
        static void Ring(Vector2 pos,float size,Color color)
        {
            if(!thinRing)
            {
                var t=new Texture2D(256,256,TextureFormat.RGBA32,false);t.hideFlags=HideFlags.HideAndDontSave;
                var pixels=new Color[256*256];for(int y=0;y<256;y++)for(int x=0;x<256;x++){float r=Vector2.Distance(new Vector2(x,y),new Vector2(127.5f,127.5f))/127.5f;pixels[y*256+x]=new Color(1,1,1,r>.945f&&r<.985f?1:0);}t.SetPixels(pixels);t.Apply();
                thinRing=Sprite.Create(t,new Rect(0,0,256,256),Vector2.one*.5f);thinRing.hideFlags=HideFlags.HideAndDontSave;
            }
            Image("Focus halo",pos,Vector2.one*size,thinRing,color);
        }
        static void Line(Vector2 a,Vector2 b,Color c,float width=1.3f)
        {var i=Image("Starlight trail",(a+b)*.5f,new Vector2(Vector2.Distance(a,b),width),null,c);i.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);}
        static void Trail(Vector2 a,Vector2 b,bool lit)
        {
            // Trails stop before the planets; dots and double highlights read as light, not diagram edges.
            var delta=b-a;var from=a+delta.normalized*52;var to=b-delta.normalized*52;
            Line(from,to,lit?new Color(Gold.r,Gold.g,Gold.b,1f):new Color(Quiet.r,Quiet.g,Quiet.b,.42f),lit?5:2);
            if(lit)for(int i=1;i<7;i++)Dot(Vector2.Lerp(from,to,i/7f),i%2==0?7:5,Gold);
        }
        static void Back()=>Text("‹",new Vector2(48,669),40,60);
        static string Name(int p)=>planets[p].Info.Planet(language=="ko"?Language.Ko:Language.En);
        static string Tagline(int p){var c=copy.chapters.First(x=>x.id==Ids[p]);return language=="ko"?c.tagline:c.en.tagline;}
        static void Map(string state,int focus,int cleared)
        {
            // Keep the seven selectable worlds distinct from decorative sky planets.
            Reset(0,true,false);Back();var points=direction=="A"?A:B;
            if(direction=="B")
            {Ring(new Vector2(358,385),400,new Color(.55f,.68f,.74f,.055f));Ring(new Vector2(358,385),440,new Color(.55f,.68f,.74f,.035f));}
            foreach(var e in story.dipper)
            {int a=Array.IndexOf(Ids,e.from),b=Array.IndexOf(Ids,e.to);if(a<0||b<0)continue;Trail(points[a],points[b],a<cleared&&b<cleared);}
            for(int p=0;p<7;p++)
            {
                if(direction=="B")Ring(points[p]+Vector2.down*5,156,new Color(Quiet.r,Quiet.g,Quiet.b,.48f));
                if(p==focus)Ring(points[p]+Vector2.down*9,direction=="A"?216:216,Gold);
                Image("Actual Unity world "+Ids[p],points[p],Vector2.one*(direction=="A"?290:290),worlds[p],Color.white);
                if(p<cleared)for(int s=0;s<3;s++)Image("Completed starlight",points[p]+new Vector2((s-1)*20,-106),Vector2.one*16,Ui.StarShape,Gold);
                else Dot(points[p]+new Vector2(0,-106),5,Quiet);
                // Invisible button target, minimum 96 px. No card or frame.
                var hit=Rect("Select "+Ids[p],points[p],Vector2.one*112).gameObject.AddComponent<Image>();hit.color=Color.clear;var button=hit.gameObject.AddComponent<Button>();button.targetGraphic=hit;
            }
            if(focus>=0){Text(Name(focus),new Vector2(780,656),32,480);Text(Tagline(focus),new Vector2(780,612),20,480,new Color(.71f,.78f,.79f,1));}
            if(cleared==7)
            {Trail(points[6],new Vector2(1240,65),true);Ring(new Vector2(1230,65),30,Gold);}
            Save(direction+"-"+language+"-map-"+state);
        }
        static void Stages(int p,string state,int focus,int cleared)
        {
            Reset(p);Back();var planet=Planet(p,p==6?9:5,p==6?9:5);
            cam.orthographicSize=direction=="A"?(p==6?9.2f:7.4f):(p==6?14.5f:11.8f);
            Object.FindAnyObjectByType<RuinsHengeSky>().SendMessage("LateUpdate");
            Text(Name(p),new Vector2(640,666),26);
            int count=planets[p].Stages.Count;var positions=new List<Vector2>();
            var dim=new Material(planet.surface);dim.SetColor("_BaseColor",new Color(.52f,.56f,.61f,1));
            // A walks across the summit. B follows a ring of satellite stepping stones.
            for(int s=0;s<count;s++)
            {
                Vector3 pos;
                if(direction=="A")
                {float x=(s/(float)(count-1)-.5f)*(p==6?8:5.6f);float z=Mathf.Sin(s/(float)(count-1)*Mathf.PI*1.5f)*1.0f;pos=new Vector3(x,.55f,z);}
                else
                {float angle=(140-s*280f/(count-1))*Mathf.Deg2Rad;float radius=p==6?12.5f:10.4f;pos=new Vector3(0,-.6f,0)+cam.transform.right*Mathf.Cos(angle)*radius+cam.transform.up*Mathf.Sin(angle)*radius*.77f-cam.transform.forward*5;}
                var visual=chapters[p].tileSet.floor;
                var stone=Object.Instantiate(visual.prefab,world.transform);stone.name="Stage stepping stone "+s;stone.transform.position=pos+visual.offset;stone.transform.localRotation=visual.RotationFor(s);stone.transform.localScale=visual.Scale*2.3f;ChapterMaterials.Apply(stone,chapters[p]);
                if(s>cleared)foreach(var r in stone.GetComponentsInChildren<Renderer>())r.sharedMaterial=dim;
                var bounds=stone.GetComponentInChildren<Renderer>().bounds;var min=new Vector2(float.MaxValue,float.MaxValue);var max=new Vector2(float.MinValue,float.MinValue);
                for(int corner=0;corner<8;corner++){var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));var screen=(Vector2)cam.WorldToScreenPoint(point);min=Vector2.Min(min,screen);max=Vector2.Max(max,screen);}
                targets.Add(Ids[p]+","+state+","+s+","+(max.x-min.x).ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+","+(max.y-min.y).ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+",80");
                if(max.x-min.x<40||max.y-min.y<40)throw new Exception("Stone smaller than 40px: "+Ids[p]+"/"+s);
                var sp=(Vector2)cam.WorldToScreenPoint(pos+Vector3.up*.24f);positions.Add(sp);
                if(s>0)
                {
                    var prev=positions[s-1];var tangent=(sp-prev).normalized;var normal=new Vector2(-tangent.y,tangent.x);
                    for(int k=1;k<5;k++)Dot(Vector2.Lerp(prev,sp,k/5f)+normal*(k%2==0?3:-3),s<cleared?3:2,s<cleared?Gold:new Color(Quiet.r,Quiet.g,Quiet.b,.45f));
                }
                if(s<cleared)Image("Cleared stage star",sp,Vector2.one*27,Ui.StarShape,Gold);
                if(s==cleared)for(int scarf=0;scarf<3;scarf++)Dot(sp+new Vector2((scarf-1)*12,6),9,scarf==0?Gold:scarf==1?new Color(.55f,.80f,.83f):new Color(.85f,.66f,.58f));
                if(s==cleared){for(int glow=0;glow<4;glow++)Image("Gentle current glow",sp,Vector2.one*(72-glow*8),Ui.Circle,new Color(Gold.r,Gold.g,Gold.b,.025f));}
                var target=Rect("Stage touch target "+s,sp,Vector2.one*80).gameObject.AddComponent<Image>();target.color=Color.clear;target.gameObject.AddComponent<Button>();
                if(s==focus)Ring(sp,86,Gold);
            }
            if(focus>=0)
            {
                int n=focus+1;
                Text((language=="ko"?"걸음 ":"Step ")+n.ToString("00"),new Vector2(640,55),26);
                // Numeric location is secondary and only appears on intentional focus.
            }
            Save(direction+"-"+language+"-"+Ids[p]+"-stages-"+state);
        }
        static void Before()
        {
            Reset();Planet(0,5,5,false);cam.orthographicSize=9;Object.FindAnyObjectByType<RuinsHengeSky>().SendMessage("LateUpdate");
            var grid=new BoardGrid(5,5);var v=chapters[0].tileSet.floor;
            for(int x=0;x<5;x++)for(int y=0;y<5;y++){var t=Object.Instantiate(v.prefab,world.transform);t.transform.position=grid.CellToWorld(x,y)+v.offset;t.transform.localScale=v.Scale;ChapterMaterials.Apply(t,chapters[0]);}
            Ui.MenuBackground(canvas.transform);
            var map=new PlanetMapView(canvas.transform,planets,levels);var loc=Localizer.FromAssets(assets.strings,Language.Ko);map.Apply(loc);map.Show(new ProgressData(),0);
            Save("before-ko-current-map");
            var list=new PlanetStagesPanel(canvas.transform);list.Bind(planets[0],new ProgressData(),levels,loc);list.Show();Save("before-ko-current-moss-stages");
        }
        static Texture2D Read()
        {var old=RenderTexture.active;RenderTexture.active=cam.targetTexture;var t=new Texture2D(1280,720,TextureFormat.RGBA32,false);t.ReadPixels(new Rect(0,0,1280,720),0,0);t.Apply();RenderTexture.active=old;return t;}
        static long MeshTris(Mesh mesh){long n=0;for(int s=0;s<mesh.subMeshCount;s++)n+=mesh.GetIndexCount(s)/3;return n;}
        static void Save(string name)
        {
            Canvas.ForceUpdateCanvases();if(PlanetBodyStudy.UseV4)cam.Render();cam.Render();var t=Read();File.WriteAllBytes(Path.Combine(Output,name+".png"),t.EncodeToPNG());Object.DestroyImmediate(t);
            var meshes=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(f=>f.sharedMesh).ToArray();
            var renders=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);long tris=meshes.Sum(f=>MeshTris(f.sharedMesh));
            var materials=new HashSet<Material>(renders.SelectMany(r=>r.sharedMaterials));int uiVerts=0;var uiMaterials=new HashSet<Material>();
            foreach(var graphic in canvas.GetComponentsInChildren<Graphic>())
            {if(!graphic.isActiveAndEnabled)continue;uiVerts+=graphic.canvasRenderer.GetMesh()?.vertexCount??0;uiMaterials.Add(graphic.materialForRendering);}
            var stats=typeof(UnityStats).GetProperty("drawCalls",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
            perf.Add(name+","+tris+","+renders.Length+","+materials.Count+","+uiVerts+","+uiMaterials.Count+","+(renders.Length+canvas.GetComponentsInChildren<Graphic>().Count(g=>g.isActiveAndEnabled))+","+(stats==null?"unavailable":stats.GetValue(null).ToString()));
            if(!name.StartsWith("before")&&(tris>=50000||materials.Count+uiMaterials.Count>6))throw new Exception("Scene budget exceeded: "+name+" tris="+tris+" mats="+(materials.Count+uiMaterials.Count));
        }
    }
}
