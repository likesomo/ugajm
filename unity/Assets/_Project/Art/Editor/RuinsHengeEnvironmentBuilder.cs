#if UNITY_EDITOR
using System.IO;
using SevenSteps.Contracts;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
namespace SevenSteps.Art
{
    public static class RuinsHengeEnvironmentBuilder
    {
        const string Art="Assets/_Project/Art/RuinsHenge";
        const string Env="Assets/_Project/Prefabs/Tiles/RuinsHenge/Env";
        static Color C(string s){ColorUtility.TryParseHtmlString("#"+s,out var c);return c;}
        static T Asset<T>(string path) where T:ScriptableObject {var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a)return a;a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);return a;}
        public static void Build()
        {
            Directory.CreateDirectory(Env);AssetDatabase.Refresh();
            var root=new GameObject("Planet_Chapter01");var planet=root.AddComponent<RuinsHengePlanet>();planet.surface=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/BaseAtlas.mat");
            var summit=PrefabUtility.SaveAsPrefabAsset(root,Env+"/PlanetSummit_Chapter01.prefab");
            var skyObject=new GameObject("Sky_Chapter01");var skyComponent=skyObject.AddComponent<RuinsHengeSky>();skyComponent.space=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/MarkUnlit.mat");
            var volume=skyObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10;volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Art+"/PreviewPostFX.asset");
            var sky=PrefabUtility.SaveAsPrefabAsset(skyObject,Env+"/Sky_Chapter01.prefab");skyObject.transform.SetParent(root.transform,false);
            var lighting=Asset<LightingPreset>(Art+"/Lighting_Chapter01.asset");
            lighting.key=new LightRig{enabled=true,color=C("FFE2B9"),intensity=2,euler=new Vector3(48,-35,0),shadows=true};
            lighting.fill=new LightRig{enabled=true,color=C("A8CFFA"),intensity=.55f,euler=new Vector3(32,135,0)};
            lighting.rim=new LightRig{enabled=true,color=C("E4CEFF"),intensity=.28f,euler=new Vector3(20,175,0)};lighting.ambient=C("8A9CAF");
            foreach(var pair in new[]{("Key",lighting.key),("Fill",lighting.fill),("Rim",lighting.rim)}){var light=new GameObject(pair.Item1,typeof(Light)).GetComponent<Light>();light.transform.SetParent(root.transform,false);light.type=LightType.Directional;light.color=pair.Item2.color;light.intensity=pair.Item2.intensity;light.transform.localRotation=Quaternion.Euler(pair.Item2.euler);light.shadows=pair.Item2.shadows?LightShadows.Soft:LightShadows.None;light.shadowStrength=.65f;}
            PrefabUtility.SaveAsPrefabAsset(root,Env+"/Planet_Chapter01.prefab");Object.DestroyImmediate(root);
            var skyPreset=Asset<SkyPreset>(Art+"/Sky_Chapter01.asset");skyPreset.background=C("101C32");skyPreset.planetPrefab=summit;skyPreset.skyPrefab=sky;
            var chapter=Asset<ChapterPreset>(Art+"/Chapter01.asset");chapter.id="moss-ruins";chapter.displayName="이끼의 첫 별";chapter.tileSet=AssetDatabase.LoadAssetAtPath<TileSet>(Art+"/RuinsHenge_Chapter01.asset");chapter.lighting=lighting;chapter.sky=skyPreset;
            foreach(var a in new ScriptableObject[]{lighting,skyPreset,chapter})EditorUtility.SetDirty(a);AssetDatabase.SaveAssets();
            Debug.Log("[RuinsHenge] Environment full prefab + contract-split summit/sky/light presets created");
        }
    }
}
#endif
