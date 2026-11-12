using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.Flow;
using SevenSteps.Game.UI;
using SevenSteps.Game.UI.Planets;

namespace SevenSteps.Art.SurfaceUiReview
{
    [InitializeOnLoad]
    public static class ConstellationCapture
    {
        const string Key="ConstellationCapture.active";
        const string Output="C:/unityjam/shared-assets/seven-steps/concepts/ui/stage-select-2026-11-11/surface-ui-2026-11-12/d2-refine";
        sealed class Job { public string planet,state,phase,lang; public int variant; public string Name=>"D2"+(char)('a'+variant)+"-"+planet+"-"+state+"-"+lang+(phase=="focused"?"":"-"+phase)+".png"; }
        static readonly List<Job> Jobs=new List<Job>();static readonly List<string> Report=new List<string>(),Metrics=new List<string>(),Targets=new List<string>();
        static readonly Dictionary<string,string> Protected=new Dictionary<string,string>();
        static int index,step;static double ready;static string loaded;
        static GameAssets assets;static Camera cam;static GameObject root,uiGo;static PlanetSpace space;static PlanetSurfaceView surface;static PlanetBody body;static ConstellationLayout layout;
        static IReadOnlyList<PlanetEntry> planets;static Localizer loc;static StageSelectModel model;static int planetIndex;
        static ConstellationCapture(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            Directory.CreateDirectory(Output);SnapshotProtected();SessionState.SetBool(Key,true);index=0;step=0;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        static void Add(string p,int variant,string state="mid",string lang="ko",string phase="focused")=>Jobs.Add(new Job{planet=p,variant=variant,state=state,lang=lang,phase=phase});
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
            try
            {
                if(step==0)
                {
                    if(Protected.Count==0)SnapshotProtected();
                    Bootstrap.UseLaunchOptionsForNextRun(LaunchOptions.Parse(new[]{"SevenSteps","-lang","ko","-ui5","-telemetry","off","-savekey","d2-refine-review"},""));SceneManager.LoadScene("Title");step=1;ready=EditorApplication.timeSinceStartup+3;return;
                }
                if(EditorApplication.timeSinceStartup<ready)return;
                if(step==1)
                {
                    var title=UnityEngine.Object.FindAnyObjectByType<TitleScreen>();if(title==null)return;assets=title.assets;Ui.Theme=assets.uiTheme;
                    foreach(var c in UnityEngine.Object.FindObjectsByType<Canvas>())c.enabled=false;foreach(var c in UnityEngine.Object.FindObjectsByType<Camera>())c.enabled=false;
                    planets=PlanetEntry.FromCatalog(LevelCatalog.Load(assets).Length);loc=Localizer.FromAssets(assets.strings,Language.Ko);
                    foreach(var p in new[]{"moss","frost","altar"})for(int v=0;v<3;v++){foreach(var s in new[]{"early","mid","cleared","perfect"})Add(p,v,s);Add(p,v,"mid","en");}
                    foreach(var p in new[]{"joinery","hollow","carved","crumble"})for(int v=0;v<3;v++)Add(p,v);
                    foreach(var phase in new[]{"idle","hover","pressed","release"})Add("moss",1,"mid","ko",phase);
                    Metrics.Add("variant,planet,progress,language,phase,screen,stages,cleared,current,focus,paintingPixels,markerPixels,coveredPaintingPixels,paintingOcclusionPct,minTargetPx,overlaps,issues");Targets.Add("screen,index,state,best,x,y,width,height");
                    Report.Add("Unity "+Application.unityVersion+"; real Title / PlanetSurfaceView / S2 projection; batch Play Mode 1280x720");step=2;
                }
                if(index>=Jobs.Count){Finish();return;}var job=Jobs[index];
                if(step==2)
                {
                    if(loaded!=job.planet){Cleanup();BuildPlanet(job.planet);loaded=job.planet;}
                    loc.Set(job.lang=="en"?Language.En:Language.Ko);int focus=job.phase=="focused"?2:4;string name=LevelCatalog.TextOf(planets[planetIndex].Stages[focus],loc.Current).name;
                    layout=new ConstellationLayout(cam,assets,job.planet,job.variant,model.StageCount(planetIndex),job.state,job.lang=="en",name,job.phase);ready=EditorApplication.timeSinceStartup+.15;step=3;return;
                }
                if(step==3){CaptureMeasured(job);layout.Dispose();layout=null;index++;step=2;ready=EditorApplication.timeSinceStartup+.05;}
            }
            catch(Exception e){Report.Add("FAIL: "+e);Write();SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void BuildPlanet(string id)
        {
            root=new GameObject("D2 refined surface review");var cameraGo=new GameObject("Production surface review camera",typeof(Camera));cameraGo.transform.SetParent(root.transform,false);cam=cameraGo.GetComponent<Camera>();cam.orthographic=true;cam.aspect=1280f/720;cam.nearClipPlane=.05f;cam.farClipPlane=200;cam.backgroundColor=assets.background;cam.clearFlags=CameraClearFlags.SolidColor;
            uiGo=new GameObject("Native surface UI",typeof(RectTransform),typeof(Canvas));uiGo.transform.SetParent(root.transform,false);var canvas=uiGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=cam;
            space=new PlanetSpace(cam,assets.background);space.World.SetParent(root.transform,false);var map=new PlanetMap3DView(uiGo.transform,space,assets.planetBodies,planets,assets.PlanetMapSprites);surface=new PlanetSurfaceView(uiGo.transform,space,assets.planetBodies,planets,map.SetBodiesVisible);loc.Set(Language.Ko);map.Apply(loc);surface.Apply(loc);
            var keys=planets.Select(p=>p.Key).ToList();planetIndex=keys.IndexOf(id);var stages=planets.Select(p=>p.Stages).ToList();var cleared=new HashSet<int>(stages[planetIndex].Take(4));model=StageSelectModel.From(keys,stages,cleared.Contains,stages[planetIndex][4],Array.Empty<(int,int)>(),Array.Empty<(int,string)>());
            map.Show(model,planetIndex,-1);surface.Bind(model,planetIndex);body=surface.Body;SurfaceProjection.Apply(body,cam,id);surface.Show();surface.Preview(2);
            var backdrop=GameObject.Find("StageSelectBackdrop");if(backdrop){backdrop.transform.SetParent(root.transform,false);var scaler=backdrop.GetComponent<CanvasScaler>();if(scaler)scaler.enabled=false;}
            cam.orthographicSize=3.9f;space.Pose(Vector3.zero,3.9f);if(backdrop)SurfaceDirectionLayout.PlaceCanvas(backdrop.GetComponent<Canvas>(),cam,90);canvas.enabled=false;
            foreach(var r in body.GetComponentsInChildren<MeshRenderer>())if(r!=body.Renderer)r.enabled=false;body.transform.localRotation=Quaternion.Euler(0,-8,0);
            if(model.StageCount(planetIndex)!=(id=="altar"?8:7))throw new Exception("Unexpected stage count "+id);
        }
        static Texture2D Render()
        {
            Canvas.ForceUpdateCanvases();var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);rt.Create();cam.targetTexture=rt;cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;var im=new Texture2D(1280,720,TextureFormat.RGBA32,false);im.ReadPixels(new Rect(0,0,1280,720),0,0);im.Apply();RenderTexture.active=old;cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);return im;
        }
        static void CaptureMeasured(Job j)
        {
            var frame=Render();File.WriteAllBytes(Path.Combine(Output,j.Name),frame.EncodeToPNG());UnityEngine.Object.DestroyImmediate(frame);
            var bc=GameObject.Find("StageSelectBackdrop")?.GetComponent<Canvas>();bool was=bc!=null&&bc.enabled;if(bc)bc.enabled=false;var old=cam.backgroundColor;cam.backgroundColor=Color.clear;layout.Overlay.enabled=false;layout.Markers.SetActive(false);var paint=Render();layout.Markers.SetActive(true);body.Renderer.enabled=false;var marks=Render();body.Renderer.enabled=true;layout.Overlay.enabled=true;cam.backgroundColor=old;if(bc)bc.enabled=was;
            var p=paint.GetPixels32();var m=marks.GetPixels32();int painting=0,marker=0,covered=0;for(int i=0;i<p.Length;i++){bool a=p[i].a>38,b=m[i].a>38;if(a)painting++;if(b)marker++;if(a&&b)covered++;}UnityEngine.Object.DestroyImmediate(paint);UnityEngine.Object.DestroyImmediate(marks);
            int overlaps=layout.Issues.Count(s=>s.StartsWith("Overlap"));float pct=100f*covered/Mathf.Max(1,painting);
            Metrics.Add(string.Join(",","D2"+(char)('a'+j.variant),j.planet,j.state,j.lang,j.phase,j.Name,model.StageCount(planetIndex),layout.Cleared,layout.Current+1,layout.Focus+1,painting,marker,covered,pct.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),80,overlaps,layout.Issues.Count));
            for(int i=0;i<layout.Targets.Count;i++){var r=layout.Targets[i];bool stage=i<model.StageCount(planetIndex);string state=!stage?"navigation":i<layout.Cleared?"cleared":i==layout.Current?"current":"untouched";Targets.Add(string.Join(",",j.Name,i+1,state,stage?layout.Scores[i]:-1,r.x,r.y,r.width,r.height));}
            Report.Add(j.Name+": occlusion="+pct.ToString("F3")+"%; issues="+layout.Issues.Count);foreach(var issue in layout.Issues)Report.Add("ISSUE "+j.Name+": "+issue);Write();Debug.Log("[D2Refine] "+j.Name);
        }
        static void Cleanup(){if(surface!=null){surface.Hide();space.Exit();UnityEngine.Object.DestroyImmediate(root);surface=null;}}
        static void SnapshotProtected(){foreach(var folder in new[]{"PlanetsV7","PlanetsV7Surface"})foreach(var f in Directory.GetFiles("C:/unityjam/shared-assets/seven-steps/Assets/Art/Map/"+folder,"*.meta",SearchOption.AllDirectories))using(var sha=SHA256.Create())Protected[f]=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(f)));}
        static void Write(){File.WriteAllLines(Path.Combine(Output,"metrics.csv"),Metrics);File.WriteAllLines(Path.Combine(Output,"targets.csv"),Targets);File.WriteAllLines(Path.Combine(Output,"verification.txt"),Report);}
        static void Finish(){var original=new Dictionary<string,string>(Protected);SnapshotProtected();foreach(var p in original)if(Protected[p.Key]!=p.Value)throw new Exception("Protected metadata changed "+p.Key);Report.Add("PASS "+Jobs.Count+" 1280x720 Play Mode frames; "+Protected.Count+" protected metas unchanged");Write();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;if(Report.Any(s=>s.StartsWith("ISSUE")))EditorApplication.Exit(1);else EditorApplication.Exit(0);}
    }
}
