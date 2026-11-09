#if UNITY_EDITOR
using System;
using System.IO;
using SevenSteps.Contracts;
using UnityEngine;
using UnityEditor;
namespace SevenSteps.Art
{
    public static class SevenPlanetBuilder
    {
        const string Art="Assets/_Project/Art/RuinsHenge";
        const string Env="Assets/_Project/Prefabs/Tiles/RuinsHenge/Env";
        static Color C(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var c);return c;}
        static T Asset<T>(string path) where T:ScriptableObject
        {var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a)return a;a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);return a;}
        public static void Build()
        {
            AssetDatabase.Refresh();
            string[] ids={"moss","hollow","carved","frost","joinery","crumble","altar"};
            string[] names={"Mossy Orbit","Starhole Moon","Carved Stone World","Frost Moon","Joinery World","Crumbling World","The Stone Circle"};
            string[] tops={"C6CAB6","B8C8CA","D2C9AE","D8E4DE","D1C6AD","C7BBB1","D9DEC8"};
            string[] rims={"748D57","607D78","928366","A1BBC4","8B7654","85766B","607568"};
            string[] rocks={"586B80","405A70","81735B","66828D","715B4F","78645F","526B6A"};
            string[] skies={"385177","315B67","65576B","4D6084","465E61","635168","344D70"};
            string[] keys={"FFE2B9","D5E7EE","FFE0AF","CDE9FF","FFD3A0","F0C8AC","F5E4BC"};
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i];
                var root=new GameObject("PlanetSummit_"+id);var planet=root.AddComponent<RuinsHengePlanet>();
                planet.surface=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/BaseAtlas.mat");planet.chapterId=id;
                planet.summitTint=C(tops[i]);planet.rimTint=C(rims[i]);planet.rockTint=C(rocks[i]);planet.moonTint=C(rims[i]);
                var summit=PrefabUtility.SaveAsPrefabAsset(root,Env+"/PlanetSummit_"+id+".prefab");UnityEngine.Object.DestroyImmediate(root);
                var skyObject=new GameObject("Sky_"+id);var skyComponent=skyObject.AddComponent<RuinsHengeSky>();
                skyComponent.space=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/MarkUnlit.mat");skyComponent.nebulaTint=C(skies[i]);
                var sky=PrefabUtility.SaveAsPrefabAsset(skyObject,Env+"/Sky_"+id+".prefab");UnityEngine.Object.DestroyImmediate(skyObject);
                var light=Asset<LightingPreset>(Art+"/Lighting_"+id+".asset");
                light.key=new LightRig{enabled=true,color=C(keys[i]),intensity=i==3?1.7f:2,euler=new Vector3(48,-35+i*4,0),shadows=true};
                light.fill=new LightRig{enabled=true,color=C("A8CFFA"),intensity=.55f,euler=new Vector3(32,135,0)};
                light.rim=new LightRig{enabled=true,color=C(skies[i]),intensity=.35f,euler=new Vector3(20,175,0)};light.ambient=C("8A9CAF");
                var skyPreset=Asset<SkyPreset>(Art+"/Sky_"+id+".asset");skyPreset.background=C("101C32");skyPreset.planetPrefab=summit;skyPreset.skyPrefab=sky;
                var chapter=Asset<ChapterPreset>(Art+"/Chapter_"+id+".asset");chapter.id=id;chapter.displayName=names[i];chapter.tileSet=AssetDatabase.LoadAssetAtPath<TileSet>(Art+"/RuinsHenge_Chapter01.asset");chapter.lighting=light;chapter.sky=skyPreset;
                foreach(var a in new ScriptableObject[]{light,skyPreset,chapter})EditorUtility.SetDirty(a);
                if(!chapter.tileSet||!summit||!sky)throw new Exception("Missing art reference: "+id);
            }
            var bgPath="Assets/Shared/UI/RuinsHenge/Handwritten_Background.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(bgPath);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.SaveAndReimport();
            var background=new GameObject("MenhirJoineryBackground",typeof(RectTransform),typeof(UnityEngine.UI.Image));
            var rect=background.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=background.GetComponent<UnityEngine.UI.Image>();image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(bgPath);image.raycastTarget=false;
            PrefabUtility.SaveAsPrefabAsset(background,"Assets/_Project/Art/UI/MenhirJoineryBackground.prefab");UnityEngine.Object.DestroyImmediate(background);
            AssetDatabase.SaveAssets();Debug.Log("[SevenPlanets] Seven chapter/summit/sky/lighting presets created; IDs match chapters.json");
        }
        [Serializable] sealed class Catalog { public Entry[] chapters; }
        [Serializable] sealed class Entry { public string id;public string artPreset; }
        public static void Validate()
        {
            var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText("Assets/_Project/Data/chapters.json"));
            if(catalog.chapters.Length!=7)throw new Exception("Expected seven confirmed chapters");
            var seen=new System.Collections.Generic.HashSet<string>();
            foreach(var entry in catalog.chapters)
            {
                var chapter=AssetDatabase.LoadAssetAtPath<ChapterPreset>(Art+"/Chapter_"+entry.artPreset+".asset");
                if(!seen.Add(entry.id)||!chapter||chapter.id!=entry.id||!chapter.tileSet||!chapter.lighting||!chapter.sky)throw new Exception("Invalid chapter preset: "+entry.id);
                var planet=chapter.sky.planetPrefab.GetComponent<RuinsHengePlanet>();var sky=chapter.sky.skyPrefab.GetComponent<RuinsHengeSky>();
                if(!planet||planet.chapterId!=entry.id||!planet.surface||!sky||!sky.space)throw new Exception("Invalid environment: "+entry.id);
            }
            Debug.Log("[SevenPlanets] Validate PASS: seven JSON IDs/artPreset keys, chapter, TileSet, summit, sky, material and lighting references");
        }
        public static void FinalWindows(){try{Build();Validate();HandwrittenFontBuilder.Windows();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void Prepare(){try{Build();Validate();HandwrittenFontBuilder.CompileAndPrepare();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    }
}
#endif
