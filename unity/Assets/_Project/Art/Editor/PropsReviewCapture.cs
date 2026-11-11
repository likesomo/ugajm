#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace SevenSteps.Art
{
 [InitializeOnLoad]
 public static class PropsReviewCapture
 {
  const string Key="PropsReviewActive";
  const string Out="C:/unityjam/shared-assets/seven-steps/concepts/ui/props-2026-11-12";
  static readonly int[] Stages=Arg("-props-all","false")=="true"?Enumerable.Range(1,50).ToArray():new[]{4,5,15,24,29,36,38,41,50};
  static int index; static double since; static GameObject root; static Camera cam; static RenderTexture rt;
  static PropsReviewCapture(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
  public static void Run(){SessionState.SetBool(Key,true);SessionState.SetString("PropsReviewPhase",Arg("-props-phase","before"));EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();}
  static string Arg(string key,string fallback){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:fallback;}
  static void Tick()
  {
   if(!EditorApplication.isPlaying)return;
   try {
    if(index>=Stages.Length){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Debug.Log("[PropsReview] PASS "+index+" Play Mode stage captures");EditorApplication.Exit(0);return;}
    if(root==null){Build(Stages[index]);since=EditorApplication.timeSinceStartup;return;}
    if(EditorApplication.timeSinceStartup-since<1.5)return;
    Canvas.ForceUpdateCanvases();FixLabels();Audit(Stages[index]);if(Arg("-props-all","false")=="true"){UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(rt);root=null;index++;return;}cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();Directory.CreateDirectory(Out);File.WriteAllBytes(Path.Combine(Out,SessionState.GetString("PropsReviewPhase","before")+"-stage-"+Stages[index].ToString("D2")+".png"),tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);RenderTexture.active=old;
    UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(rt);root=null;index++;
   }catch(Exception e){SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}
  }
  static void Audit(int stage)
  {
   long Tris(GameObject go) => go.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh!=null&&f.GetComponent<Renderer>()!=null&&f.GetComponent<Renderer>().enabled).Sum(f=>Enumerable.Range(0,f.sharedMesh.subMeshCount).Sum(i=>(long)f.sharedMesh.GetIndexCount(i)/3));
   var board=root.GetComponentInChildren<BoardView>();var mats=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().Count();
   string path=Path.Combine(Out,"unity-stage-audit-"+SessionState.GetString("PropsReviewPhase","before")+".csv");if(index==0)File.WriteAllText(path,"stage,board_triangles,scene_triangles,scene_materials\n");File.AppendAllText(path,$"{stage},{Tris(board.gameObject)},{Tris(root)},{mats}\n");
  }
  static void FixLabels() { var view=root.GetComponentInChildren<BoardView>();var labels=(System.Collections.IEnumerable)typeof(BoardView).GetField("labels",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(view); var canvas=root.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.WorldSpace;var rect=canvas.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(1600,1000);rect.SetPositionAndRotation(cam.transform.position+cam.transform.forward*.8f,cam.transform.rotation);rect.localScale=Vector3.one*(2*cam.orthographicSize/1000);foreach(var label in labels){var type=label.GetType();var text=(Text)type.GetField("Text").GetValue(label);if(text==null||!text.enabled)continue;var world=(Vector3)type.GetField("World").GetValue(label);var follow=(Transform)type.GetField("Follow").GetValue(label);if(follow!=null)world+=follow.position;var vp=cam.WorldToViewportPoint(world);text.fontSize=Mathf.Max(10,Mathf.RoundToInt(1000/(2*cam.orthographicSize)*(float)type.GetField("Size").GetValue(label)));text.rectTransform.anchoredPosition=new Vector2((vp.x-.5f)*rect.rect.width,(vp.y-.5f)*rect.rect.height);}}
  static ChapterPreset Baseline(ChapterPreset chapter)
  {
   const string p="Assets/_Project/Art/Test/PropsBaseline/";
   var copy=UnityEngine.Object.Instantiate(chapter);var set=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<TileSet>(p+"Set.asset"));
   TileVisual Replace(TileVisual v,string name){v.prefab=AssetDatabase.LoadAssetAtPath<GameObject>(p+name+".prefab");return v;}
   set.exit=Replace(set.exit,"Exit");set.gate=Replace(set.gate,"Gate");set.countDoor=Replace(set.countDoor,"CountDoor");set.plates=set.plates.Select(v=>Replace(v,"Plate")).ToArray();set.doors=set.doors.Select(v=>Replace(v,"Door")).ToArray();set.bridge=Replace(set.bridge,"Bridge");set.cracked=Replace(set.cracked,"Cracked");set.flipWalls=new[]{Replace(set.flipWalls[0],"FlipUp"),Replace(set.flipWalls[1],"FlipDown")};set.ice=Replace(set.ice,"Ice");set.lever=Replace(set.lever,"Lever");set.footprint=Replace(set.footprint,"Footprint");set.mud=Replace(set.mud,"Mud");set.startMarker=Replace(set.startMarker,"Start");set.playerBodies=new[]{Replace(set.playerBodies[0],"PlayerA"),Replace(set.playerBodies[1],"PlayerB"),Replace(set.playerBodies[2],"PlayerC")};copy.tileSet=set;return copy;
  }
  static void Build(int stage)
  {
   var file=JsonUtility.FromJson<LevelFileDto>(File.ReadAllText("Assets/_Project/Data/levels.json"));var dto=file.levels[stage-1];var level=dto.ToLevel();var chapter=AssetDatabase.LoadAssetAtPath<ChapterPreset>("Assets/_Project/Art/RuinsHenge/Chapter_"+dto.chapter+".asset");
   if(Arg("-props-baseline","false")=="true")chapter=Baseline(chapter);
   root=new GameObject("PropsReviewStage"+stage);cam=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();cam.transform.SetParent(root.transform);cam.tag="MainCamera";cam.backgroundColor=chapter.sky.background;cam.clearFlags=CameraClearFlags.SolidColor;cam.allowHDR=false;rt=new RenderTexture(1600,1000,24){antiAliasing=4};cam.targetTexture=rt;cam.aspect=1.6f;
   Ui.Theme=AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/_Project/Art/UI/RuinsHengeTheme.asset");
   var canvas=new GameObject("Labels",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();canvas.transform.SetParent(root.transform);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=2;var rect=canvas.GetComponent<RectTransform>();
   var view=new GameObject("BoardView").AddComponent<BoardView>();view.transform.SetParent(root.transform);view.Init(chapter.tileSet,rect,cam);view.UseChapter(chapter.tileSet,chapter);view.ReduceMotion=true;var state=Rules.NewState(level);
   if(Arg("-props-progress","false")=="true")foreach(char action in dto.expected.path){var a=action switch {'U'=>Act.Up,'D'=>Act.Down,'L'=>Act.Left,'R'=>Act.Right,'K'=>Act.Keep,'X'=>Act.Switch,_=>throw new Exception("Unknown path action")};state=Rules.Dispatch(state,Command.FromAct(a)).State;if(state.Statues.Length>=Mathf.Max(1,level.StatueCount)&&state.Status==Status.Play)break;}
   view.Rebuild(state);
   var planet=UnityEngine.Object.Instantiate(chapter.sky.planetPrefab,root.transform).GetComponent<RuinsHengePlanet>();planet.Fit(chapter.tileSet.GridFor(level));
   foreach(var rig in new[]{chapter.lighting.key,chapter.lighting.fill,chapter.lighting.rim}){var l=new GameObject("Light",typeof(Light)).GetComponent<Light>();l.transform.SetParent(root.transform);l.type=LightType.Directional;l.enabled=rig.enabled;l.color=rig.color;l.intensity=rig.intensity;l.transform.rotation=Quaternion.Euler(rig.euler);l.shadows=LightShadows.None;}
   RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=chapter.lighting.ambient;
  }
 }
}
#endif
