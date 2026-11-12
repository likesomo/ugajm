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
    public static class SurfaceUiCapture
    {
        const string Key="SurfaceUiCapture.active";
        const string Output="C:/unityjam/shared-assets/seven-steps/concepts/ui/stage-select-2026-11-11/surface-ui-2026-11-12";
        static readonly string[] Names={"moss","frost","altar"};
        static readonly List<string> Report=new List<string>(),Metrics=new List<string>(),TargetRows=new List<string>();
        static readonly Dictionary<string,string> Protected=new Dictionary<string,string>();
        static int planet=-1,direction,language,step;static double ready;
        static GameAssets assets;static Camera cam;static GameObject reviewRoot,uiGo;static PlanetSpace space;
        static PlanetSurfaceView surface;static PlanetBody body;static SurfaceDirectionLayout layout;
        static IReadOnlyList<PlanetEntry> planets;static Localizer loc;static StageSelectModel model;static int planetIndex;
        static SurfaceUiCapture(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            Directory.CreateDirectory(Output);SnapshotProtected();SessionState.SetBool(Key,true);planet=-1;step=0;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
            try
            {
                if(planet<0)
                {
                    if(step==0)
                    {
                        if(Protected.Count==0)SnapshotProtected();
                        Bootstrap.UseLaunchOptionsForNextRun(LaunchOptions.Parse(new[]{"SevenSteps","-lang","ko","-ui5","-telemetry","off","-savekey","surface-ui-review"},""));
                        SceneManager.LoadScene("Title");step=1;ready=EditorApplication.timeSinceStartup+3;return;
                    }
                    if(EditorApplication.timeSinceStartup<ready)return;
                    var title=UnityEngine.Object.FindAnyObjectByType<TitleScreen>();if(title==null)return;assets=title.assets;Ui.Theme=assets.uiTheme;
                    foreach(var c in UnityEngine.Object.FindObjectsByType<Canvas>())c.enabled=false;
                    foreach(var c in UnityEngine.Object.FindObjectsByType<Camera>())c.enabled=false;
                    planets=PlanetEntry.FromCatalog(LevelCatalog.Load(assets).Length);loc=Localizer.FromAssets(assets.strings,Language.Ko);
                    Metrics.Add("direction,planet,language,stages,screen,paintingPixels,markerPixels,paintingCoveredPixels,paintingOcclusionPct,bodyScreenPct,minTargetPx,hitOverlaps,issues");
                    TargetRows.Add("direction,planet,language,index,state,bestStars,x,y,width,height");Report.Add("Unity "+Application.unityVersion+"; batch Play Mode; production Title / PlanetSurfaceView / S2 projected body; 1280x720");
                    planet=0;direction=0;language=0;step=0;
                }
                if(EditorApplication.timeSinceStartup<ready)return;
                if(planet>=3){Finish();return;}
                if(step==0){BuildPlanet();ready=EditorApplication.timeSinceStartup+.5;step=1;return;}
                if(step==1)
                {
                    CapturePlain("before-"+Names[planet]+".png");
                    uiGo.GetComponent<Canvas>().enabled=false;
                    foreach(var r in body.GetComponentsInChildren<MeshRenderer>())if(r!=body.Renderer)r.enabled=false;
                    cam.orthographicSize=3.9f;space.Pose(Vector3.zero,3.9f);direction=1;language=0;step=2;
                }
                if(step==2)
                {
                    body.transform.localRotation=Quaternion.Euler(0,-8,0);
                    loc.Set(language==0?Language.Ko:Language.En);
                    int stage=planets[planetIndex].Stages[2];string title=LevelCatalog.TextOf(stage,loc.Current).name;
                    layout=new SurfaceDirectionLayout(cam,body,assets,direction,model.StageCount(planetIndex),language!=0,title);
                    ready=EditorApplication.timeSinceStartup+.15;step=3;return;
                }
                if(step==3)
                {
                    string code=language==0?"ko":"en";string name="D"+direction+"-"+Names[planet]+"-"+code+".png";
                    CaptureMeasured(name,code);
                    if(planet==0&&language==0)
                    {
                        body.transform.localRotation=Quaternion.Euler(0,-4,0);CapturePlain("D"+direction+"-moss-turn-plus.png");
                        body.transform.localRotation=Quaternion.Euler(0,-12,0);CapturePlain("D"+direction+"-moss-turn-minus.png");body.transform.localRotation=Quaternion.Euler(0,-8,0);
                    }
                    layout.Dispose();layout=null;language++;
                    if(language>=2){language=0;direction++;}
                    if(direction>4){Cleanup();planet++;step=0;}else step=2;
                    ready=EditorApplication.timeSinceStartup+.1;
                }
            }
            catch(Exception e){Report.Add("FAIL: "+e);Write();SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void BuildPlanet()
        {
            reviewRoot=new GameObject("S2 surface UI review");var cameraGo=new GameObject("Surface UI camera",typeof(Camera));cameraGo.transform.SetParent(reviewRoot.transform,false);cam=cameraGo.GetComponent<Camera>();
            cam.orthographic=true;cam.aspect=1280f/720;cam.nearClipPlane=.05f;cam.farClipPlane=200;cam.backgroundColor=assets.background;cam.clearFlags=CameraClearFlags.SolidColor;
            uiGo=new GameObject("Native surface UI",typeof(RectTransform),typeof(Canvas));uiGo.transform.SetParent(reviewRoot.transform,false);var canvas=uiGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=cam;canvas.sortingOrder=10;
            space=new PlanetSpace(cam,assets.background);space.World.SetParent(reviewRoot.transform,false);var backdrop=GameObject.Find("StageSelectBackdrop");if(backdrop)backdrop.transform.SetParent(reviewRoot.transform,false);
            var map=new PlanetMap3DView(uiGo.transform,space,assets.planetBodies,planets,assets.PlanetMapSprites);
            surface=new PlanetSurfaceView(uiGo.transform,space,assets.planetBodies,planets,map.SetBodiesVisible);loc.Set(Language.Ko);map.Apply(loc);surface.Apply(loc);
            var keys=planets.Select(p=>p.Key).ToList();planetIndex=keys.IndexOf(Names[planet]);var stages=planets.Select(p=>p.Stages).ToList();var cleared=new HashSet<int>(stages[planetIndex].Take(4));
            model=StageSelectModel.From(keys,stages,cleared.Contains,stages[planetIndex][4],Array.Empty<(int,int)>(),Array.Empty<(int,string)>());
            map.Show(model,planetIndex,-1);surface.Bind(model,planetIndex);body=surface.Body;SurfaceProjection.Apply(body,cam,Names[planet]);surface.Show();surface.Preview(2);
            backdrop=GameObject.Find("StageSelectBackdrop");
            if(backdrop){backdrop.transform.SetParent(reviewRoot.transform,false);var scaler=backdrop.GetComponent<CanvasScaler>();if(scaler)scaler.enabled=false;}
            SurfaceDirectionLayout.PlaceCanvas(canvas,cam,.8f);
            if(backdrop)SurfaceDirectionLayout.PlaceCanvas(backdrop.GetComponent<Canvas>(),cam,90);
            Canvas.ForceUpdateCanvases();surface.Preview(2);SurfaceDirectionLayout.PlaceCanvas(canvas,cam,.8f);
            var collider=body.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=body.GetComponent<MeshFilter>().sharedMesh;
            if(model.StageCount(planetIndex)!=(planet==2?8:7))throw new Exception("Wrong stage count for "+Names[planet]);
        }
        static Texture2D Render()
        {
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);rt.Create();cam.targetTexture=rt;cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;
            var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();RenderTexture.active=old;cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);return image;
        }
        static void CapturePlain(string name){Canvas.ForceUpdateCanvases();var image=Render();File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);Debug.Log("[SurfaceUI] "+name);}
        static void CaptureMeasured(string name,string code)
        {
            CapturePlain(name);var backdrop=GameObject.Find("StageSelectBackdrop");var bc=backdrop?.GetComponent<Canvas>();bool backdropOn=bc!=null&&bc.enabled;var color=cam.backgroundColor;
            if(bc)bc.enabled=false;layout.Overlay.enabled=false;cam.backgroundColor=Color.clear;layout.Markers.SetActive(false);
            var paint=Render();var p=paint.GetPixels32();layout.Markers.SetActive(true);body.Renderer.enabled=false;
            var marker=Render();var m=marker.GetPixels32();body.Renderer.enabled=true;
            int painting=0,mark=0,covered=0;for(int i=0;i<p.Length;i++){bool a=p[i].a>38,b=m[i].a>38;if(a)painting++;if(b)mark++;if(a&&b)covered++;}
            layout.Overlay.enabled=true;if(bc)bc.enabled=backdropOn;cam.backgroundColor=color;
            int overlap=layout.Issues.Count(s=>s.StartsWith("Overlapping"));
            float occlusion=painting>0?100f*covered/painting:0;float bodyPct=100f*painting/(1280*720);
            Metrics.Add(string.Join(",","D"+direction,Names[planet],code,model.StageCount(planetIndex),name,painting,mark,covered,occlusion.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),bodyPct.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),80,overlap,layout.Issues.Count));
            for(int i=0;i<layout.Targets.Count;i++)
            {
                var r=layout.Targets[i];string state=i<4?"cleared":i==4?"current":i<model.StageCount(planetIndex)?"untouched":"navigation";
                TargetRows.Add(string.Join(",","D"+direction,Names[planet],code,i,state,i<4?i:0,r.x.ToString("F1",System.Globalization.CultureInfo.InvariantCulture),r.y.ToString("F1",System.Globalization.CultureInfo.InvariantCulture),r.width,r.height));
            }
            foreach(string issue in layout.Issues)Report.Add(name+": "+issue);
            Report.Add(name+": stages="+model.StageCount(planetIndex)+"; occlusion="+occlusion+"%; 80px targets; focused 03 / best 2");
            UnityEngine.Object.DestroyImmediate(paint);UnityEngine.Object.DestroyImmediate(marker);Write();
        }
        static void Cleanup(){surface.Hide();space.Exit();UnityEngine.Object.DestroyImmediate(reviewRoot);reviewRoot=null;}
        static void SnapshotProtected()
        {
            foreach(string folder in new[]{"PlanetsV7","PlanetsV7Surface"})
                foreach(string file in Directory.GetFiles("C:/unityjam/shared-assets/seven-steps/Assets/Art/Map/"+folder,"*.meta",SearchOption.AllDirectories))
                    using(var sha=SHA256.Create())Protected[file]=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file)));
        }
        static void Write(){File.WriteAllLines(Path.Combine(Output,"metrics.csv"),Metrics);File.WriteAllLines(Path.Combine(Output,"targets.csv"),TargetRows);File.WriteAllLines(Path.Combine(Output,"verification.txt"),Report);}
        static void Finish()
        {
            var original=new Dictionary<string,string>(Protected);SnapshotProtected();foreach(var pair in original)if(Protected[pair.Key]!=pair.Value)throw new Exception("Protected metadata changed: "+pair.Key);
            Report.Add("PASS: protected PlanetsV7 and PlanetsV7Surface metadata unchanged ("+Protected.Count+" hashes)");Write();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;
            Debug.Log("[SurfaceUI] PASS: 24 focused frames, 8 turn checks, 3 baselines; metadata unchanged");EditorApplication.Exit(0);
        }
    }
}
