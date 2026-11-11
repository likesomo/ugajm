using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SevenSteps.Core;
using SevenSteps.Contracts;
using SevenSteps.Game;
namespace SevenSteps.Art.StepStars
{
public sealed class ReadabilityReview : MonoBehaviour
{
 const string Tiles="Assets/_Project/Prefabs/Tiles/RuinsHenge";
 const string Shared="Assets/Shared/Art/RuinsHenge/BoardShapeA";
 const string Output="C:/unityjam/shared-assets/seven-steps/concepts/ui/board-readability-2026-11-11";
 static readonly string[] Ids={"moss","hollow","carved","frost","joinery","crumble","altar"};
 static Mesh Source(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>(Shared+"/SS_BSA_"+name+".fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
 public static void Run()
 {
  Directory.CreateDirectory(Output);
  try { BuildTiles(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("ReadabilityReview").AddComponent<ReadabilityReview>();EditorApplication.isPlaying=true; }
  catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
 }
 static void BuildTiles()
 {
  Directory.CreateDirectory(Tiles+"/Readability");AssetDatabase.Refresh();
  foreach(string name in new[]{"Floor","FloorAlt","FloorThird","Wall","WallStoneV2","WallStoneV3","FlipUp"})
  {
   string path=Tiles+"/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
   try {
    foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))
    {
     if(!mf.name.StartsWith("Model_")&&!(name=="FlipUp"&&mf.name=="FlipUpPanel"))continue;
     bool block=name.StartsWith("Wall")||name=="FlipUp";
     // Always rebuild from source, never compound tint or displacement on a rerun.
     string module=block?"Block":name=="Floor"?"Floor_0":name=="FloorAlt"?"Floor_1":"Floor_2";
     var mesh=Instantiate(Source(module));mesh.name="Readable_"+name;
     var v=mesh.vertices;var colors=mesh.colors;if(colors.Length!=v.Length)colors=Enumerable.Repeat(Color.white,v.Length).ToArray();
     var b=mesh.bounds;
     for(int i=0;i<v.Length;i++) {
      if(block){v[i].x*=.82f;v[i].z*=.82f;v[i].y=(v[i].y-b.min.y)/b.size.y*.615f-.035f;colors[i]*=new Color(.18f,.235f,.255f,1);}
      else {v[i].x*=.96f;v[i].z*=.96f;colors[i]*=new Color(1.5f,1.37f,1.15f,1);}
     }
     mesh.vertices=v;mesh.colors=colors;mesh.RecalculateBounds();mesh.RecalculateNormals();mesh.RecalculateTangents();
     string asset=Tiles+"/Readability/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(asset);
     if(existing){EditorUtility.CopySerialized(mesh,existing);DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,asset);
     mf.sharedMesh=mesh;mf.transform.localPosition=Vector3.zero;mf.transform.localScale=Vector3.one;
    }
    PrefabUtility.SaveAsPrefabAsset(root,path);
   } finally {PrefabUtility.UnloadPrefabContents(root);}
  }
  AssetDatabase.SaveAssets();
 }
 Camera cam;GameObject root,sky,actor;BoardGrid grid;ChapterPreset chapter;Material starMat,emptyMat,stoneMat;readonly List<string> perf=new List<string>{"planet,before,tris,renderers,materials,floorLuma,wallLuma,contrast"};
 GameObject Spawn(TileVisual visual,int x,int y,bool before=false,string module=null)
 {
  if(!visual.prefab)return null;var go=Instantiate(visual.prefab,root.transform);go.transform.position=grid.CellToWorld(x,y)+visual.offset;go.transform.localScale=visual.Scale;go.transform.rotation=visual.RotationFor(x+y*grid.W);
  if(before&&module!=null)foreach(var mf in go.GetComponentsInChildren<MeshFilter>())if(mf.name.StartsWith("Model_"))mf.sharedMesh=Source(module);
  ChapterMaterials.Apply(go,chapter);return go;
 }
 IEnumerator Start()
 {
  yield return null;
  cam=new GameObject("ReviewCamera",typeof(Camera)).GetComponent<Camera>();cam.tag="MainCamera";cam.allowHDR=false;cam.orthographic=true;cam.aspect=1280f/720;
  starMat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));starMat.color=new Color(1,.78f,.33f);emptyMat=new Material(starMat);emptyMat.color=new Color(.30f,.43f,.46f);stoneMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));stoneMat.color=new Color(.18f,.23f,.24f);
  var dto=JsonUtility.FromJson<LevelFileDto>(File.ReadAllText("Assets/_Project/Data/levels.json"));var stage=dto.levels[2];
  foreach(string id in Ids)
  {
   chapter=AssetDatabase.LoadAssetAtPath<ChapterPreset>("Assets/_Project/Art/RuinsHenge/Chapter_"+id+".asset");
   foreach(bool before in new[]{true,false})
   {
    Setup(stage,before);yield return null;yield return new WaitForSeconds(.5f);
    Capture("part1-"+(before?"before":"after")+"-stage03-"+id+".png");Measure(before);
    Destroy(root);Destroy(sky);yield return null;
   }
  }
  foreach(string id in new[]{"moss","frost"})
  {
   chapter=AssetDatabase.LoadAssetAtPath<ChapterPreset>("Assets/_Project/Art/RuinsHenge/Chapter_"+id+".asset");
   foreach(string concept in new[]{"A","B","C"})foreach(int steps in new[]{0,3,6,7})
   {
    Setup(stage,false);var state=Rules.NewState(Level.Parse(stage.name,stage.hint,stage.bodies,stage.map));foreach(char move in "RRDDRLR".Substring(0,steps))state=Rules.Step(state,move=='R'?Dir.Right:move=='L'?Dir.Left:Dir.Down).State;if(state.Used!=steps||(steps==7&&state.Status!=Status.Choose))throw new Exception("Review must show a real step/choice state");actor.transform.position=grid.CellToWorld(state.Pos);var markers=new GameObject("Concept "+concept);markers.transform.SetParent(root.transform);
    var positions=new List<Vector3>();
    for(int i=0;i<7;i++)
    {
     Vector3 p;
     if(concept=="A")p=cam.ViewportToWorldPoint(new Vector3(.35f+i*.05f,.91f,12));
     else if(concept=="B")p=new Vector3(-3.3f+i*1.1f,.12f,-3.85f);
     else p=new Vector3(-2.5f+i*.83f,4.5f+Mathf.Sin(i*Mathf.PI/6)*.4f,1.7f);
     positions.Add(p);bool lit=i<steps;float radius=concept=="A"?.22f:concept=="B"?.18f:.20f;
     var starParent=markers.transform;if(steps==6&&i==6){var pulse=new GameObject("Last empty star pulses");pulse.transform.SetParent(markers.transform);pulse.AddComponent<ReviewPulse>();starParent=pulse.transform;}
     Star(starParent,p,radius,lit||(steps==6&&i==6),!lit,concept=="B");
     if(steps==6&&i==6)Star(starParent,p,radius*1.55f,true,true,concept=="B");
     if(concept=="B") {
      var plinth=GameObject.CreatePrimitive(PrimitiveType.Cube);plinth.name="Review marker stone";plinth.transform.SetParent(markers.transform);plinth.transform.position=p-new Vector3(0,.10f,0);plinth.transform.localScale=new Vector3(.55f,.14f,.48f);plinth.GetComponent<Renderer>().sharedMaterial=stoneMat;Destroy(plinth.GetComponent<Collider>());
     }
    }
    if(concept=="C")for(int i=0;i<Mathf.Min(steps-1,6);i++)Line(markers.transform,positions[i],positions[i+1],.014f);
    yield return null;yield return new WaitForSeconds(.25f);Capture("part2-"+concept+"-"+id+"-"+steps+"-steps.png");if(steps==6){yield return new WaitForSeconds(.5f);Capture("part2-"+concept+"-"+id+"-6-pulse.png");}
    Destroy(root);Destroy(sky);yield return null;
   }
  }
  File.WriteAllLines(Output+"/perf.csv",perf);Debug.Log("[ReadabilityReview] COMPLETE Play Mode 1280x720");EditorApplication.Exit(0);
 }
 void Setup(LevelDto dto,bool before)
 {
  root=new GameObject("Stage03_"+chapter.id);var level=Level.Parse(dto.name,dto.hint,dto.bodies,dto.map,dto.statueCount);grid=new BoardGrid(level,1);
  var planet=Instantiate(chapter.sky.planetPrefab,root.transform).GetComponent<RuinsHengePlanet>();planet.Fit(grid);ChapterMaterials.Apply(planet.gameObject,chapter);
  var set=chapter.tileSet;
  for(int y=0;y<level.H;y++)for(int x=0;x<level.W;x++) {int i=y*level.W+x;bool wall=level.Tile(i)=='#';Spawn(wall?set.wall:((x+y)%2==0?set.floor:set.floorAlt),x,y,before,wall?"Block":((x+y)%2==0?"Floor_0":"Floor_1"));if(level.Tile(i)=='E')Spawn(set.exit,x,y);}
  actor=Spawn(set.PlayerFor(0),level.X(level.Start),level.Y(level.Start));
  foreach(var rig in new[]{chapter.lighting.key,chapter.lighting.fill,chapter.lighting.rim}) {var l=new GameObject("PresetLight",typeof(Light)).GetComponent<Light>();l.transform.SetParent(root.transform);l.type=LightType.Directional;l.enabled=rig.enabled;l.color=rig.color;l.intensity=rig.intensity;l.transform.rotation=Quaternion.Euler(rig.euler);l.shadows=UnityEngine.LightShadows.None;}
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=chapter.lighting.ambient;cam.backgroundColor=chapter.sky.background;cam.transform.rotation=Quaternion.Euler(50,0,0);cam.orthographicSize=5.7f;cam.transform.position=new Vector3(0,.15f,0)-cam.transform.forward*30;sky=Instantiate(chapter.sky.skyPrefab,cam.transform);
 }
 Texture2D frame;
 void Capture(string name)
 {
  var rt=new RenderTexture(1280,720,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
  if(frame)Destroy(frame);frame=new Texture2D(1280,720,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,1280,720),0,0);frame.Apply();File.WriteAllBytes(Output+"/"+name,frame.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;Destroy(rt);
 }
 float Luma(Vector3 point) {var p=cam.WorldToViewportPoint(point);float sum=0;for(int x=-3;x<=3;x++)for(int y=-3;y<=3;y++){var c=frame.GetPixel((int)(p.x*1280)+x,(int)(p.y*720)+y).linear;sum+=.2126f*c.r+.7152f*c.g+.0722f*c.b;}return sum/49;}
 void Measure(bool before)
 {
  long tris=0;foreach(var f in root.GetComponentsInChildren<MeshFilter>().Concat(cam.GetComponentsInChildren<MeshFilter>()))if(f.sharedMesh)for(int i=0;i<f.sharedMesh.subMeshCount;i++)tris+=f.sharedMesh.GetIndexCount(i)/3;
  var mats=new HashSet<Material>();foreach(var r in root.GetComponentsInChildren<Renderer>().Concat(cam.GetComponentsInChildren<Renderer>()))foreach(var m in r.sharedMaterials)mats.Add(m);
  float floor=Luma(grid.CellToWorld(2,1)+Vector3.up*.01f),wall=Luma(grid.CellToWorld(4,1)+Vector3.up*(before?.1f:.58f));perf.Add($"{chapter.id},{before},{tris},{root.GetComponentsInChildren<Renderer>().Length+cam.GetComponentsInChildren<Renderer>().Length},{mats.Count},{floor:F4},{wall:F4},{(floor-wall)/floor:P1}");
 }
 void Star(Transform parent,Vector3 p,float radius,bool lit,bool outline,bool horizontal)
 {
  var v=new List<Vector3>();var t=new List<int>();var q=horizontal?Quaternion.Euler(90,0,0):cam.transform.rotation;
  for(int i=0;i<10;i++){float a=i*Mathf.PI/5;v.Add(p+q*new Vector3(Mathf.Sin(a),Mathf.Cos(a),0)*radius*(i%2==0?1:.45f));}
  if(outline){for(int i=0;i<10;i++)Line(parent,v[i],v[(i+1)%10],lit?.018f:.012f,lit);return;}
  v.Add(p);for(int i=0;i<10;i++)t.AddRange(new[]{10,i,(i+1)%10});var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();var go=new GameObject("Star",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=starMat;
 }
 void Line(Transform parent,Vector3 a,Vector3 b,float width,bool lit=true)
 {var go=new GameObject("Starlight",typeof(LineRenderer));go.transform.SetParent(parent);var line=go.GetComponent<LineRenderer>();line.sharedMaterial=lit?starMat:emptyMat;line.positionCount=2;line.SetPositions(new[]{a,b});line.startWidth=line.endWidth=width;line.startColor=line.endColor=lit?Color.white:new Color(.22f,.30f,.34f);}
}
}
