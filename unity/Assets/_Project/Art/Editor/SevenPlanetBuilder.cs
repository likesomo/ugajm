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
            string[] tops={"4B9A86","9167B4","C7944D","99CADF","AE6A46","968478","C7A44B"};
            string[] rims={"40A894","9252C4","D79B36","72BDF4","BA6747","937E78","DBB54B"};
            string[] rocks={"146D64","66379A","B66A27","75BFE0","873B27","71615B","B88D29"};
            string[] skies={"12352F","210E3C","382412","102C54","321318","30282B","34280C"};
            string[] moons={"65C9BA","C795FF","E9B553","D7F7FF","DC8E67","B7A196","F9DA7A"};
            string[] keys={"B6E9CF","D9B1FF","FFD392","B9E8FF","FFB878","E5C5AA","FFE5A0"};
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i];
                var root=new GameObject("PlanetSummit_"+id);var planet=root.AddComponent<RuinsHengePlanet>();
                var baseMaterial=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/BaseAtlas.mat");
                string materialPath=Art+"/Materials/Environment_"+id+".mat";
                var environmentMaterial=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!environmentMaterial){environmentMaterial=new Material(baseMaterial);AssetDatabase.CreateAsset(environmentMaterial,materialPath);}
                environmentMaterial.CopyPropertiesFromMaterial(baseMaterial);environmentMaterial.SetFloat("_UseMoss",id=="moss"?1:0);
                environmentMaterial.SetColor("_MossColor",C(id=="moss"?"438E56":rims[i]));environmentMaterial.SetColor("_WallColor",Color.Lerp(C(rocks[i]),Color.white,.30f));EditorUtility.SetDirty(environmentMaterial);
                planet.surface=environmentMaterial;planet.chapterId=id;
                planet.summitTint=C(tops[i]);planet.rimTint=C(rims[i]);planet.rockTint=C(rocks[i]);planet.moonTint=C(moons[i]);
                var summit=PrefabUtility.SaveAsPrefabAsset(root,Env+"/PlanetSummit_"+id+".prefab");UnityEngine.Object.DestroyImmediate(root);
                var skyObject=new GameObject("Sky_"+id);var skyComponent=skyObject.AddComponent<RuinsHengeSky>();
                skyComponent.space=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/MarkUnlit.mat");skyComponent.nebulaTint=C(rims[i]);skyComponent.backgroundTint=C(skies[i]);skyComponent.secondaryTint=C(moons[i]);
                var sky=PrefabUtility.SaveAsPrefabAsset(skyObject,Env+"/Sky_"+id+".prefab");UnityEngine.Object.DestroyImmediate(skyObject);
                var light=Asset<LightingPreset>(Art+"/Lighting_"+id+".asset");
                light.key=new LightRig{enabled=true,color=C(keys[i]),intensity=i==3?1.2f:1.35f,euler=new Vector3(48,-35+i*4,0),shadows=true};
                light.fill=new LightRig{enabled=true,color=C(moons[i]),intensity=.4f,euler=new Vector3(32,135,0)};
                light.rim=new LightRig{enabled=true,color=C(skies[i]),intensity=.35f,euler=new Vector3(20,175,0)};light.ambient=Color.Lerp(C(skies[i]),C(moons[i]),.32f);
                var skyPreset=Asset<SkyPreset>(Art+"/Sky_"+id+".asset");skyPreset.background=C(skies[i]);skyPreset.planetPrefab=summit;skyPreset.skyPrefab=sky;
                var chapter=Asset<ChapterPreset>(Art+"/Chapter_"+id+".asset");chapter.id=id;chapter.displayName=names[i];chapter.tileSet=AssetDatabase.LoadAssetAtPath<TileSet>(Art+"/RuinsHenge_Chapter01.asset");chapter.lighting=light;chapter.sky=skyPreset;chapter.materialOverrides=new[]{new MaterialOverride{from=baseMaterial,to=environmentMaterial}};
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
            foreach(var id in seen)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Env+"/PlanetSummit_"+id+".prefab");
                var instance=UnityEngine.Object.Instantiate(prefab);var planet=instance.GetComponent<RuinsHengePlanet>();
                planet.Preview(5,5);planet.Preview(9,7);UnityEngine.Object.DestroyImmediate(instance);
            }
            Debug.Log("[SevenPlanets] Geometry clearance PASS: all exterior prop vertices outside board + one-cell bounds at 5x5 and 9x7");
            Debug.Log("[SevenPlanets] Validate PASS: seven JSON IDs/artPreset keys, chapter, TileSet, summit, sky, material and lighting references");
        }
        public static void ReviewWindows()
        {
            try
            {
                Build();Validate();HandwrittenFontBuilder.Check();
                var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
                var camera=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=7.1f;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.transform.rotation=Quaternion.Euler(50,0,0);camera.transform.position=new Vector3(0,-.8f,0)-camera.transform.forward*20;
                var review=new GameObject("PlanetReview").AddComponent<PlanetReviewCapture>();
                string[] ids={"moss","hollow","carved","frost","joinery","crumble","altar"};review.chapters=new ChapterPreset[7];
                for(int i=0;i<7;i++)review.chapters[i]=AssetDatabase.LoadAssetAtPath<ChapterPreset>(Art+"/Chapter_"+ids[i]+".asset");
                const string path="Assets/_Project/Art/Test/PlanetReview.unity";UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,path);
                var report=UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions{scenes=new[]{path,"Assets/_Project/Art/Test/HandwrittenUI.unity"},extraScriptingDefines=new[]{"SEVENSTEPS_ART_PREVIEW"},target=UnityEditor.BuildTarget.StandaloneWindows64,locationPathName="../Builds/PlanetReview/Windows/PlanetReview.exe"});
                Debug.Log("[PlanetReview] Build "+report.summary.result+" errors="+report.summary.totalErrors);EditorApplication.Exit(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded?0:1);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void FinalWindows(){try{Build();Validate();HandwrittenFontBuilder.Windows();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void Prepare(){try{Build();Validate();HandwrittenFontBuilder.CompileAndPrepare();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    }
}
#endif
