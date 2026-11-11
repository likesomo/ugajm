#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace SevenSteps.Art {
 public static class CraterCrustBuilder {
  public static void Apply(){
   AssetDatabase.Refresh();
   foreach(string id in BoardShapeABuilder.Ids){
    string path="Assets/Shared/Art/RuinsHenge/CraterCrust/CraterCrust_"+id+".fbx";
    var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.generateSecondaryUV=false;importer.SaveAndReimport();
    var filter=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<MeshFilter>();var mesh=filter.sharedMesh;Debug.Log("[CraterBounds] "+id+" "+mesh.bounds+" scale "+filter.transform.lossyScale+" rotation "+filter.transform.rotation);
    if(mesh.triangles.Length/3<20000||mesh.uv2.Length!=mesh.vertexCount)throw new Exception("Invalid crust: "+id);
    var names=id=="moss"?new[]{"PlanetSummit_moss","PlanetSummit_Chapter01","Planet_Chapter01"}:new[]{"PlanetSummit_"+id};
    foreach(string name in names){string prefab="Assets/_Project/Prefabs/Tiles/RuinsHenge/Env/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(prefab);try{
      var planet=root.GetComponent<RuinsHengePlanet>();if(!planet)throw new Exception("Missing planet: "+prefab);if(!planet.boardShape)planet.boardShape=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Tiles/RuinsHenge/Env/PlanetSummit_moss.prefab").GetComponent<RuinsHengePlanet>().boardShape;planet.craterCrust=mesh;planet.craterCrustScale=filter.transform.lossyScale;planet.craterCrustEuler=filter.transform.eulerAngles;PrefabUtility.SaveAsPrefabAsset(root,prefab);
    }finally{PrefabUtility.UnloadPrefabContents(root);}}
    Debug.Log("[CraterCrust] "+id+" "+mesh.triangles.Length/3+" tris, one existing stone material");
   }
   AssetDatabase.SaveAssets();EditorApplication.Exit(0);
  }
 }
}
#endif
