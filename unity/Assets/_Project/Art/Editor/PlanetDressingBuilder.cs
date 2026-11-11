#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.Art
{
    /// <summary>Imports only external Blender binaries, preserving preset and summit GUIDs.</summary>
    public static class PlanetDressingBuilder
    {
        const string Shared="Assets/Shared/Art/RuinsHenge/PlanetDressing";
        const string Art="Assets/_Project/Art/RuinsHenge";
        const string Env="Assets/_Project/Prefabs/Tiles/RuinsHenge/Env";
        static readonly string[] Ids={"moss","hollow","carved","frost","joinery","crumble","altar"};
        static readonly string[][] Modules={
            new[]{"MossBoulder","Fern","Flowers","FootprintStone","FernRock"},
            new[]{"PumiceRing","PumiceArch","PumiceSpires","StarWindow","PerforatedBoulder"},
            new[]{"RuneSlab","StrataPillar","BrokenStela","SpiralStele","RuneTablet"},
            new[]{"IceCluster","IceMenhir","IcicleFringe","QuartzFan","FrozenStream"},
            new[]{"TimberFrame","GearLever","Scaffold","RopeCoil","PlankStack"},
            new[]{"MineralPool","DrippingStone","WarmLamp","BrokenTiles","CascadePools"},
            new[]{"Lintel","RingSegment","LichenStone","GrassTuft","StarAltarDisc"}
        };
        public static void Build()
        {
            try
            {
                AssetDatabase.Refresh();Directory.CreateDirectory(Art+"/Dressing");AssetDatabase.Refresh();
                foreach(var path in Directory.GetFiles(Shared,"*.fbx"))
                {
                    var importer=(ModelImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                    importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;
                    importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
                    importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
                    importer.SaveAndReimport();
                }
                foreach(var path in Directory.GetFiles(Shared,"*.png"))
                {
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                    importer.textureType=TextureImporterType.Default;importer.sRGBTexture=!path.Contains("Detail");
                    importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;importer.maxTextureSize=512;
                    importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                }
                var kit=AssetDatabase.LoadAssetAtPath<BoardShapeAKit>(Art+"/BoardShapeA_Kit.asset");
                var kerb=AssetDatabase.LoadAssetAtPath<GameObject>(Shared+"/PD_JoineryKerbLite.fbx");
                if(!kerb)throw new Exception("Missing joinery kerb LOD; run build_dressing_kerb.py");
                kit.dressingJoineryKerb=kerb.GetComponentInChildren<MeshFilter>().sharedMesh;EditorUtility.SetDirty(kit);
                Debug.Log("[PlanetDressing] Joinery kerb "+kit.joineryKerb.triangles.Length/3+" -> "+kit.dressingJoineryKerb.triangles.Length/3+" tris (oak bevels, brass hardware and atlas preserved)");
                for(int i=0;i<Ids.Length;i++)
                {
                    string id=Ids[i];var mat=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/Environment_"+id+".mat");
                    mat.SetTexture("_PlanetColor",AssetDatabase.LoadAssetAtPath<Texture2D>(Shared+"/PD_"+id+"_Color.png"));
                    mat.SetTexture("_PlanetDetail",AssetDatabase.LoadAssetAtPath<Texture2D>(Shared+"/PD_"+id+"_Detail.png"));
                    mat.SetFloat("_PlanetDressing",1);mat.SetFloat("_PlanetIce",id=="frost"?1:0);mat.SetFloat("_BoardNormalStrength",id=="frost"?.22f:.60f);
                    var pigments=new[]{new Color(.58f,.61f,.55f),new Color(.57f,.40f,.27f),new Color(.79f,.37f,.22f),new Color(.64f,.85f,.96f),new Color(.25f,.30f,.34f),new Color(.86f,.76f,.58f),new Color(.68f,.63f,.43f)};
                    mat.SetColor("_PlanetPigment",pigments[i]);
                    EditorUtility.SetDirty(mat);
                    string path=Art+"/Dressing/Dressing_"+id+".asset";
                    var set=AssetDatabase.LoadAssetAtPath<PlanetDressingSet>(path);
                    if(!set){set=ScriptableObject.CreateInstance<PlanetDressingSet>();AssetDatabase.CreateAsset(set,path);}
                    set.planetId=id;set.material=mat;set.modules=new GameObject[Modules[i].Length];
                    long tris=0;
                    for(int j=0;j<Modules[i].Length;j++)
                    {
                        string name="PD_"+id+"_"+Modules[i][j];
                        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Shared+"/"+name+".fbx");
                        if(!model)throw new Exception("Missing Blender module: "+name);
                        var root=new GameObject(name);var child=UnityEngine.Object.Instantiate(model,root.transform);
                        child.name="BlenderModel";
                        foreach(var r in child.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
                        foreach(var f in child.GetComponentsInChildren<MeshFilter>())tris+=f.sharedMesh.triangles.Length/3;
                        set.modules[j]=PrefabUtility.SaveAsPrefabAsset(root,Env+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(root);
                    }
                    if(tris>8000)throw new Exception(id+" dressing exceeds 8k: "+tris);
                    EditorUtility.SetDirty(set);
                    string summit=Env+"/PlanetSummit_"+id+".prefab";var planetRoot=PrefabUtility.LoadPrefabContents(summit);
                    try{planetRoot.GetComponent<RuinsHengePlanet>().dressing=set;PrefabUtility.SaveAsPrefabAsset(planetRoot,summit);}
                    finally{PrefabUtility.UnloadPrefabContents(planetRoot);}
                    Debug.Log("[PlanetDressing] "+id+" source tris="+tris+" modules="+set.modules.Length+" material="+mat.name);
                }
                AssetDatabase.SaveAssets();Debug.Log("[PlanetDressing] BUILD COMPLETE");
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
#endif
