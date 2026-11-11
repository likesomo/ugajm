#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Game;
using UnityEditor;
using UnityEngine;
namespace SevenSteps.Art
{
 public static class RuinsHengePropsBuilder
 {
  const string Art="Assets/_Project/Art/RuinsHenge";
  const string Prefabs="Assets/_Project/Prefabs/Tiles/RuinsHenge";
  const string Shared="Assets/Shared/Art/RuinsHenge/Props";
  static readonly Dictionary<string,GameObject> Built=new Dictionary<string,GameObject>();
  public static void Build()
  {
   AssetDatabase.Refresh();
   var stone=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/BaseAtlas.mat");
   var accent=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/MarkUnlit.mat");
   foreach(var kind in new[]{"Exit","Plate","Door","Gate","CountDoor","Bridge","Cracked","FlipUp","FlipDown","Ice","Lever","Footprint","Mud","Start","OldStart"})
   {
    string path=Shared+"/RH_Prop_"+(kind=="OldStart"?"Start":kind)+".fbx";
    var importer=(ModelImporter)AssetImporter.GetAtPath(path);
    if(importer==null)throw new Exception("Missing Blender export "+path);
    importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;
    importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.isReadable=false;
    importer.materialImportMode=ModelImporterMaterialImportMode.None;
    importer.SaveAndReimport();
    var src=AssetDatabase.LoadAssetAtPath<GameObject>(path);
    var root=new GameObject(kind);var model=(GameObject)PrefabUtility.InstantiatePrefab(src);
    PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
    model.name="Model";model.transform.SetParent(root.transform,false);
    foreach(var r in root.GetComponentsInChildren<Renderer>(true))
    {
     r.sharedMaterials=Enumerable.Repeat(r.name.StartsWith("inlay_")||r.name.StartsWith("carving_")?accent:stone,r.sharedMaterials.Length).ToArray();
     r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    var all=root.GetComponentsInChildren<Transform>(true);
    foreach(var t in all)t.name=System.Text.RegularExpressions.Regex.Replace(t.name,@"\.\d+$","");
    var view=root.AddComponent<TilePropView>();
    Transform Find(string name)=>all.FirstOrDefault(t=>t.name==name);
    Transform[] Series(string name,int count)=>Enumerable.Range(0,count).Select(i=>Find(name+i)).Where(t=>t!=null).ToArray();
    view.slab=Find("Slab");view.handle=Find("Handle");view.glyphs=Series("Glyph_",4);view.lamps=Series("Lamp_",7);view.sockets=Series("Socket_",7);view.tokens=Series("Token_",7);
    view.accents=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("inlay_")).ToArray();
    if(kind=="Start"||kind=="Footprint")view.accentColor=new Color(.337f,.549f,.533f);
    if(kind=="OldStart")view.accentColor=new Color(.42f,.48f,.44f);
    if(kind=="Ice")view.accentColor=new Color(.60f,.85f,.95f);
    if(kind=="Plate")view.slideDepth=.04f;
    // A hand-authored source hierarchy carries no gameplay behaviours or colliders.
    foreach(var g in view.glyphs)g.gameObject.SetActive(g==view.glyphs[0]);
    foreach(var t in view.tokens)t.gameObject.SetActive(false);
    foreach(var t in view.sockets)t.gameObject.SetActive(t==view.sockets[0]);
    foreach(var t in view.lamps)t.gameObject.SetActive(t==view.lamps[0]);
    root.AddComponent<BoardShapeAContact>().radius=new Vector2(.42f,.32f);
    Built[kind]=PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"/"+kind+".prefab");UnityEngine.Object.DestroyImmediate(root);
   }
   TileVisual V(string name,bool hide=false)=>new TileVisual{prefab=Built[name],scale=Vector3.one,hideLabel=hide};
   foreach(var guid in AssetDatabase.FindAssets("t:TileSet",new[]{Art}))
   {
    var set=AssetDatabase.LoadAssetAtPath<TileSet>(AssetDatabase.GUIDToAssetPath(guid));
    set.exit=V("Exit",true);set.gate=V("Gate",true);set.countDoor=V("CountDoor",true);
    set.plates=Enumerable.Range(0,4).Select(_=>V("Plate",true)).ToArray();set.doors=Enumerable.Range(0,4).Select(_=>V("Door",true)).ToArray();
    set.bridge=V("Bridge");set.cracked=V("Cracked");set.flipWalls=new[]{V("FlipUp"),V("FlipDown")};set.ice=V("Ice");set.lever=V("Lever");set.footprint=V("Footprint");set.mud=V("Mud");set.startMarker=V("Start");set.oldStartMarker=V("OldStart");
    EditorUtility.SetDirty(set);
   }
   // The source start and each brother used to draw concentric rings. The single Blender start ring owns this cue.
   foreach(var name in new[]{"PlayerA","PlayerB","PlayerC"})
   {
    var path=Prefabs+"/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
    foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="IdentityRing").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
    PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
   }
   var statuePath=Prefabs+"/Statue.prefab";var statueRoot=PrefabUtility.LoadPrefabContents(statuePath);
   foreach(var t in statueRoot.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("ScarfWrap")).ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
   PrefabUtility.SaveAsPrefabAsset(statueRoot,statuePath);PrefabUtility.UnloadPrefabContents(statueRoot);
   AssetDatabase.SaveAssets();Debug.Log("[StageProps] Blender FBX props wired; glyphs 0..3; sockets/lamps 0..6; shared BaseAtlas + MarkUnlit");
   EditorApplication.Exit(0);
  }
 }
}
#endif
