#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine.Rendering;
using Unity.Profiling;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SevenSteps.Game;
namespace SevenSteps.Art {
[InitializeOnLoad] public static class CraterReview {
 const string Root="C:/unityjam/shared-assets/seven-steps/concepts/themes/crust-craters-2026-11-11";
 static readonly int[] Stages={1,8,15,22,29,36,43};
 static readonly string[] Ids={"moss","hollow","carved","frost","joinery","crumble","altar"};
 static double ready; static bool captured; static ProfilerRecorder drawCounter,triCounter; static string stats;
 static CraterReview(){EditorApplication.playModeStateChanged+=Changed;if(SessionState.GetBool("CraterReview.active",false))EditorApplication.update+=Tick;}
 public static void Before()=>Begin("before"); public static void After()=>Begin("after");
 static void Begin(string phase){Directory.CreateDirectory(Root);SessionState.SetString("CraterReview.phase",phase);var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-craterIndex");SessionState.SetInt("CraterReview.index",arg>=0?int.Parse(args[arg+1]):0);SessionState.SetBool("CraterReview.active",true);EditorApplication.update-=Tick;EditorApplication.update+=Tick;Next();}
 static void Size(){

  var asm=typeof(Editor).Assembly;var gv=asm.GetType("UnityEditor.GameView");var window=EditorWindow.GetWindow(gv);if(!Application.isBatchMode)window.Show();
  var sizesType=asm.GetType("UnityEditor.GameViewSizes");var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);var sizes=singleton.GetProperty("instance").GetValue(null);
  var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{0});var sizeType=asm.GetType("UnityEditor.GameViewSize");var enumType=asm.GetType("UnityEditor.GameViewSizeType");
  var size=Activator.CreateInstance(sizeType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{Enum.ToObject(enumType,1),1280,720,"Crater1280"},null);
  group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
  gv.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(window,count-1);if(!Application.isBatchMode)window.Focus();
 }
 static void Next(){int i=SessionState.GetInt("CraterReview.index",0);if(i==7){SessionState.SetBool("CraterReview.active",false);EditorApplication.update-=Tick;EditorApplication.Exit(0);return;}
  EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity");UnityEngine.Object.FindAnyObjectByType<GameController>().startLevel=Stages[i];Size();captured=false;ready=double.MaxValue;EditorApplication.isPlaying=true;
 }
 static void Changed(PlayModeStateChange state){if(!SessionState.GetBool("CraterReview.active",false))return;if(state==PlayModeStateChange.EnteredPlayMode){drawCounter=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",256);triCounter=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",256);ready=EditorApplication.timeSinceStartup+8;captured=false;}if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("CraterReview.active",false);EditorApplication.Exit(0); }}
 static void CaptureCamera(string path){
  var camera=Camera.main;
  if(!camera)throw new Exception("Missing gameplay camera");
  var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
  target.Create();
  var overlays=UnityEngine.Object.FindObjectsByType<Canvas>().Where(c=>c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
  foreach(var canvas in overlays){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
  Canvas.ForceUpdateCanvases();
  RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
  var previous=RenderTexture.active;RenderTexture.active=target;
  var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
  texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
  File.WriteAllBytes(path,texture.EncodeToPNG());
  RenderTexture.active=previous;
  foreach(var canvas in overlays){canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;}
  UnityEngine.Object.DestroyImmediate(texture);target.Release();UnityEngine.Object.DestroyImmediate(target);
 }
 static void Tick(){if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<ready)return;
  int i=SessionState.GetInt("CraterReview.index",0);string phase=SessionState.GetString("CraterReview.phase","");string path=Path.Combine(Root,phase+"_"+Ids[i]+".png");
  if(!captured){
   foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(canvas.name.Contains("Chapter"))canvas.gameObject.SetActive(false);
   var meshes=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(f=>f.sharedMesh&&f.GetComponent<Renderer>()&&f.GetComponent<Renderer>().enabled).ToArray();
   long tris=meshes.Sum(f=>(long)f.sharedMesh.GetIndexCount(0)/3);int mats=meshes.SelectMany(f=>f.GetComponent<Renderer>().sharedMaterials).Distinct().Count();
   if(Application.isBatchMode)CaptureCamera(path);
   int allMats=UnityEngine.Object.FindObjectsByType<Renderer>().Where(r=>r.enabled).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().Count();
   stats=$"{Ids[i]},{Stages[i]},{tris},{mats},{allMats}";
   if(!Application.isBatchMode)ScreenCapture.CaptureScreenshot(path);captured=true;ready=EditorApplication.timeSinceStartup+.1;return;
  }
  if(!File.Exists(path))throw new Exception("Game view screenshot missing: "+path);
  long draws=drawCounter.Count>0?drawCounter.ToArray().Max(s=>s.Value):0;
  long rendered=triCounter.Count>0?triCounter.ToArray().Max(s=>s.Value):0;
  File.AppendAllText(Path.Combine(Root,phase+"_stats.csv"),stats+$",{rendered},{draws}\n");
  Debug.Log("[CraterReview] "+stats+$", rendered {rendered}, draws {draws}");
  drawCounter.Dispose();triCounter.Dispose();EditorApplication.isPlaying=false;ready=double.MaxValue;
 }
}
}
#endif
