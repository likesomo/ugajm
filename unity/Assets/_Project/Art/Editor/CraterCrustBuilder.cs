#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace SevenSteps.Art {
 public static class CraterCrustBuilder {
  static void ValidateClosed(Mesh mesh,string id){
   var positions=new Dictionary<Vector3,int>();var welded=new int[mesh.vertexCount];var vertices=mesh.vertices;
   for(int i=0;i<vertices.Length;i++){if(!positions.TryGetValue(vertices[i],out int index)){index=positions.Count;positions.Add(vertices[i],index);}welded[i]=index;}
   var edges=new Dictionary<ulong,(int count,int winding)>();var triangles=mesh.triangles;
   void Edge(int a,int b){if(a==b)throw new Exception("Degenerate crust edge: "+id);ulong key=((ulong)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);edges.TryGetValue(key,out var value);edges[key]=(value.count+1,value.winding+(a<b?1:-1));}
   for(int i=0;i<triangles.Length;i+=3){int a=welded[triangles[i]],b=welded[triangles[i+1]],c=welded[triangles[i+2]];Edge(a,b);Edge(b,c);Edge(c,a);}
   int bad=edges.Values.Count(e=>e.count!=2||e.winding!=0);
   if(bad!=0)throw new Exception("Open/non-manifold/inconsistently wound crust: "+id+" edges "+bad);
   Debug.Log("[CraterTopology] "+id+" closed, consistently wound, "+triangles.Length/3+" tris");
  }
  public static void Apply(){
   AssetDatabase.Refresh();
   foreach(string id in BoardShapeABuilder.Ids){
    string path="Assets/Shared/Art/RuinsHenge/CraterCrust/CraterCrust_"+id+".fbx";
    var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.generateSecondaryUV=false;importer.SaveAndReimport();
    var filter=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<MeshFilter>();var mesh=filter.sharedMesh;Debug.Log("[CraterBounds] "+id+" "+mesh.bounds+" scale "+filter.transform.lossyScale+" rotation "+filter.transform.rotation);
    if(mesh.triangles.Length/3<20000||mesh.uv2.Length!=mesh.vertexCount)throw new Exception("Invalid crust: "+id);
    ValidateClosed(mesh,id);
    var names=id=="moss"?new[]{"PlanetSummit_moss","PlanetSummit_Chapter01","Planet_Chapter01"}:new[]{"PlanetSummit_"+id};
    foreach(string name in names){string prefab="Assets/_Project/Prefabs/Tiles/RuinsHenge/Env/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(prefab);try{
      var planet=root.GetComponent<RuinsHengePlanet>();if(!planet)throw new Exception("Missing planet: "+prefab);if(!planet.boardShape)planet.boardShape=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Tiles/RuinsHenge/Env/PlanetSummit_moss.prefab").GetComponent<RuinsHengePlanet>().boardShape;planet.craterCrust=mesh;planet.craterCrustScale=filter.transform.lossyScale;planet.craterCrustEuler=filter.transform.eulerAngles;PrefabUtility.SaveAsPrefabAsset(root,prefab);
    }finally{PrefabUtility.UnloadPrefabContents(root);}}
    Debug.Log("[CraterCrust] "+id+" "+mesh.triangles.Length/3+" tris, one existing stone material");
   }
   var kit=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Tiles/RuinsHenge/Env/PlanetSummit_joinery.prefab").GetComponent<RuinsHengePlanet>().boardShape;Debug.Log("[CraterProps] rubble "+kit.rubble.triangles.Length/3+", timberFrame "+kit.timberFrame.triangles.Length/3);
   AssetDatabase.SaveAssets();EditorApplication.Exit(0);
  }
 }
}
#endif
