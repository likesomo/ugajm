using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Collections;
using SevenSteps.Core;
using SevenSteps.Contracts;
using SevenSteps.Game;

namespace SevenSteps.Art.Review
{
    // Read-only art review: actual Boot -> Game, actual chapter lights, board camera and PC/Mobile renderer assets.
    // No production UI or WorldStonesReady changes. Placement mirrors feat/view's pending clear-view implementation.
    [InitializeOnLoad]
    public static class WorldStarFixCapture
    {
        const string Key="WorldStarFix.capture",Output="C:/unityjam/shared-assets/seven-steps/concepts/ui/ui-redesign-2026-11-12/u5-production/world-star-fix";
        static readonly int[] Stages={0,21,28};
        static readonly string[] Planets={"moss","frost","joinery"};
        static readonly List<string> Report=new List<string>(),Metrics=new List<string>();
        static int shot=-1,step;static double ready;
        static GameController game;static Camera cam;static GameObject root;
        static RenderPipelineAsset oldPipeline,oldQuality;
        static readonly List<Renderer> inlays=new List<Renderer>();
        static string Prefix=>SessionState.GetString(Key+".prefix","after");
        static WorldStarFixCapture(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void Before()=>Run("before");
        public static void After(){ApplyMaterials();Run("after");}
        public static void AfterNative(){ApplyMaterials();Run("after");SessionState.SetBool(Key+".native",true);}
        static void ApplyMaterials()
        {
            var shell=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/UI/U5/U5StoneShell.mat");
            shell.SetColor("_BaseColor",new Color(.36f,.40f,.37f,1));shell.SetColor("_Color",new Color(.36f,.40f,.37f,1));
            shell.EnableKeyword("_EMISSION");shell.SetColor("_EmissionColor",new Color(.025f,.035f,.03f,1));EditorUtility.SetDirty(shell);
            foreach(string name in new[]{"U5StarLit","U5StarUnlit"})
            {
                var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/UI/U5/"+name+".mat");
                mat.shader=Shader.Find("Universal Render Pipeline/Unlit");mat.shaderKeywords=Array.Empty<string>();
                var color=name=="U5StarLit"?new Color(1,.76f,.30f,1):new Color(.12f,.19f,.18f,1);
                mat.SetColor("_BaseColor",color);mat.SetColor("_Color",color);mat.SetFloat("_Cull",2);mat.SetFloat("_Surface",0);mat.SetFloat("_ZWrite",1);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }
        static void Run(string prefix)
        {
            SessionState.SetBool(Key+".native",false);
            Directory.CreateDirectory(Output);SessionState.SetString(Key+".prefix",prefix);SessionState.SetBool(Key,true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            shot=-1;step=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
            try
            {
                if(shot<0)
                {
                    if(step==0)
                    {
                        Bootstrap.UseLaunchOptionsForNextRun(LaunchOptions.Parse(new[]{"SevenSteps","-level","1","-ui5","-lang","ko","-telemetry","off","-savekey","world-star-fix-review"},""));
                        SceneManager.LoadScene(AppScenes.Boot);step=1;ready=EditorApplication.timeSinceStartup+5;return;
                    }
                    if(EditorApplication.timeSinceStartup<ready)return;
                    game=UnityEngine.Object.FindAnyObjectByType<GameController>();if(game==null)return;
                    oldPipeline=GraphicsSettings.defaultRenderPipeline;oldQuality=QualitySettings.renderPipeline;
                    Report.Add("Unity "+Application.unityVersion+"; Play Mode; actual Game scene / chapter lights / board camera; "+Prefix);
                    Metrics.Add("image,planet,pipeline,state,goldPixels,minNormalDot,maxNormalDot");
                    shot=0;step=0;
                }
                if(shot>=12){Finish();return;}
                if(EditorApplication.timeSinceStartup<ready)return;
                int p=shot/4,pipeline=(shot%4)/2;bool lit=shot%2==0;
                if(step==0)
                {
                    if(root)UnityEngine.Object.DestroyImmediate(root);
                    var asset=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/"+(pipeline==0?"PC":"Mobile")+"_RPAsset.asset");
                    GraphicsSettings.defaultRenderPipeline=asset;QualitySettings.renderPipeline=asset;
                    Services.Get<AppFlow>().GoToGame(Stages[p]);ready=EditorApplication.timeSinceStartup+4;step=1;return;
                }
                if(step==1)
                {
                    game=UnityEngine.Object.FindAnyObjectByType<GameController>();cam=Camera.main;cam.aspect=1280f/720;
                    string route=LevelCatalog.PathOf(Stages[p]);
                    if(SessionState.GetBool(Key+".native",false))
                    {
                        // Present the final move normally so the real GameFlowController opens its real U5 clear view.
                        game.Board.Replay(route.Substring(0,route.Length-1));
                        if(!Rules.TryParseLetter(route[route.Length-1],out var last))throw new InvalidOperationException("Bad stored last move");
                        game.Board.Send(Command.FromAct(last));
                    }
                    else game.Board.Replay(route);
                    if(game.Board.State.Status!=Status.Win)throw new InvalidOperationException("Stored solution not Win for "+Planets[p]);
                    game.Board.InputBlocked=()=>true;ready=EditorApplication.timeSinceStartup+3.5;step=2;return;
                }
                if(step==2)
                {
                    cam.aspect=1280f/720;game.Board.View.CameraHeld=true;BoardView.Fit(cam,game.Board.View.Grid,game.Board.View.Rig);
                    foreach(var c in UnityEngine.Object.FindObjectsByType<Canvas>())c.enabled=false;
                    Place(lit);ready=EditorApplication.timeSinceStartup+.3;step=3;return;
                }
                string name=Prefix+"-"+Planets[p]+"-"+(pipeline==0?"pc":"mobile")+"-"+(lit?"lit":"unlit")+".png";
                Capture(name,Planets[p],pipeline==0?"PC":"Mobile",lit);shot++;step=0;
            }
            catch(Exception e){Report.Add("FAIL: "+e);File.WriteAllLines(Path.Combine(Output,Prefix+"-audit.txt"),Report);Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
        }
        static void Place(bool lit)
        {
            var view=game.Board.View;var state=game.Board.State;int exit=Enumerable.Range(0,state.Level.Count).First(i=>state.Level.Tile(i)=='E');
            if(SessionState.GetBool(Key+".native",false))
            {
                inlays.Clear();inlays.AddRange(view.BoardRoot.GetComponentsInChildren<Renderer>().Where(r=>r.name.Contains("StarInlay")&&r.gameObject.activeInHierarchy));
                if(inlays.Count!=3)
                {
                    var clear=UnityEngine.Object.FindAnyObjectByType<SevenSteps.Game.UI.U5.U5ClearView>(FindObjectsInactive.Include);
                    throw new InvalidOperationException("Native clear must supply three world inlays; got "+inlays.Count+"; shown="+clear?.IsShown+"; world="+clear?.WorldStones);
                }
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/UI/U5/"+(lit?"U5StarLit":"U5StarUnlit")+".mat");
                foreach(var renderer in inlays)renderer.sharedMaterial=material;
                Report.Add("Native WorldStonesReady=true path: three game-created stones, no review geometry or placement");
                return;
            }
            Vector3 e=view.Grid.CellToWorld(exit);float cell=view.Grid.CellSize;
            Vector3 away=cam.transform.forward;away.y=0;away.Normalize();Vector3 across=Vector3.Cross(Vector3.up,away).normalized;
            var groundRenderers=view.BoardRoot.GetComponentsInChildren<Renderer>();
            var spots=Enumerable.Range(0,3).Select(k=>e+away*cell+across*((k-1)*.6f*cell)).ToArray();
            float ground=0;
            foreach(var s in spots)foreach(var renderer in groundRenderers)
            {
                var b=renderer.bounds;if(b.size.x>6*cell||b.size.z>6*cell)continue;
                if(s.x>=b.min.x-.06f*cell&&s.x<=b.max.x+.06f*cell&&s.z>=b.min.z-.06f*cell&&s.z<=b.max.z+.06f*cell)ground=Mathf.Max(ground,b.max.y);
            }
            root=new GameObject("WorldStarFixReview");root.transform.SetParent(view.BoardRoot,false);inlays.Clear();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shared/Art/UI/U5/world-star-stone.fbx");
            var shell=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/UI/U5/U5StoneShell.mat");
            var star=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/UI/U5/"+(lit?"U5StarLit":"U5StarUnlit")+".mat");
            for(int k=0;k<3;k++)
            {
                var go=UnityEngine.Object.Instantiate(prefab,root.transform,false);go.transform.rotation=Quaternion.identity;
                var renderers=go.GetComponentsInChildren<Renderer>();var bounds=new Bounds(go.transform.position,Vector3.zero);foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                go.transform.localScale*=.7f*cell/bounds.size.y;
                // Intentionally use the pending production pose verbatim; the fixed asset must work when only Ready flips.
                go.transform.rotation=Quaternion.LookRotation(-cam.transform.forward,Vector3.up);
                go.transform.position=new Vector3(spots[k].x,ground,spots[k].z);
                float low=renderers.Min(r=>r.bounds.min.y);go.transform.position+=Vector3.up*(ground-low);
                foreach(var r in renderers)
                {
                    if(r.name.Contains("StarInlay")){r.sharedMaterial=star;inlays.Add(r);}else r.sharedMaterial=shell;
                    if(k==0)Report.Add("node="+r.name+"; submeshes="+r.GetComponent<MeshFilter>().sharedMesh.subMeshCount+"; slots="+r.sharedMaterials.Length+"; material="+r.sharedMaterial.name+"; shader="+r.sharedMaterial.shader.name);
                }
            }
        }
        static void Capture(string name,string planet,string pipeline,bool lit)
        {
            if(SessionState.GetBool(Key+".native",false))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/UI/U5/"+(lit?"U5StarLit":"U5StarUnlit")+".mat");
                foreach(var renderer in inlays)renderer.sharedMaterial=material;
            }
            string active=GraphicsSettings.currentRenderPipeline!=null?GraphicsSettings.currentRenderPipeline.name:"built-in";
            if(active!=pipeline+"_RPAsset")throw new InvalidOperationException("Expected "+pipeline+" renderer, got "+active);
            Report.Add(name+" active pipeline="+active+"; Camera pitch="+cam.transform.eulerAngles.x+"; yaw="+cam.transform.eulerAngles.y+"; game state="+game.Board.State.Status);
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);rt.Create();cam.targetTexture=rt;cam.Render();
            var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();RenderTexture.active=old;cam.targetTexture=null;
            File.WriteAllBytes(Path.Combine(Output,name),tex.EncodeToPNG());
            int gold=0;float minDot=1,maxDot=-1;var pixels=tex.GetPixels32();
            foreach(var r in inlays)
            {
                var b=r.bounds;float x0=1,y0=1,x1=0,y1=0;
                for(int i=0;i<8;i++){var v=cam.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));x0=Mathf.Min(x0,v.x);y0=Mathf.Min(y0,v.y);x1=Mathf.Max(x1,v.x);y1=Mathf.Max(y1,v.y);}
                for(int y=Mathf.Clamp((int)(y0*720),0,719);y<Mathf.Clamp((int)(y1*720)+1,0,720);y++)for(int x=Mathf.Clamp((int)(x0*1280),0,1279);x<Mathf.Clamp((int)(x1*1280)+1,0,1280);x++){var c=pixels[y*1280+x];if(c.r>140&&c.r>c.g*1.15f&&c.g>c.b*1.5f)gold++;}
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                using(var data=MeshUtility.AcquireReadOnlyMeshData(mesh))using(var normals=new NativeArray<Vector3>(mesh.vertexCount,Allocator.Temp))
                {
                    data[0].GetNormals(normals);foreach(var normal in normals){float dot=Vector3.Dot(r.transform.TransformDirection(normal),-cam.transform.forward);minDot=Mathf.Min(minDot,dot);maxDot=Mathf.Max(maxDot,dot);}
                }
                Report.Add(name+" node="+r.name+" shader="+r.sharedMaterial.shader.name+" bounds="+r.bounds+" dot="+minDot+".."+maxDot);
            }
            Metrics.Add(string.Join(",",name,planet,pipeline,lit?"lit":"unlit",gold,minDot.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),maxDot.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)));
            Debug.Log("[WorldStarFix] "+name+" gold="+gold+" normal="+minDot+".."+maxDot);
            UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
        }
        static void Finish()
        {
            GraphicsSettings.defaultRenderPipeline=oldPipeline;QualitySettings.renderPipeline=oldQuality;
            File.WriteAllLines(Path.Combine(Output,Prefix+"-audit.txt"),Report);File.WriteAllLines(Path.Combine(Output,Prefix+"-metrics.csv"),Metrics);
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Debug.Log("[WorldStarFix] PASS 12 Play Mode captures");EditorApplication.Exit(0);
        }
    }
}
