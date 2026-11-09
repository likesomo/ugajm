#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SevenSteps.Game.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using UnityEditor;
using UnityEditor.U2D;
namespace SevenSteps.Art
{
    public static class RuinsHengeUiBuilder
    {
        const string Root="Assets/_Project/Art/UI";
        const string Shared="Assets/Shared/UI/RuinsHenge";
        [Serializable] class Slice {public string name;public int x,y,width,height,border;}
        [Serializable] class Slices {public Slice[] items;}
        static Color C(string h){ColorUtility.TryParseHtmlString("#"+h,out var c);return c;}
        static T Asset<T>(string path) where T:ScriptableObject {var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a)return a;a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);return a;}
        [MenuItem("Seven Steps/Art/Build Ruins UI Resources")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            string path=Shared+"/RH_UI_Atlas.png";
            var ti=(TextureImporter)AssetImporter.GetAtPath(path);
            if(ti==null)throw new Exception("Run design/art/build-ui-atlas.ps1 first");
            ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Multiple;
            ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.sRGBTexture=true;ti.maxTextureSize=1024;
            ti.textureCompression=TextureImporterCompression.Uncompressed;ti.filterMode=FilterMode.Bilinear;ti.wrapMode=TextureWrapMode.Clamp;
            var defs=JsonUtility.FromJson<Slices>("{\"items\":"+File.ReadAllText(Shared+"/RH_UI_Atlas.slices.json").TrimStart('\ufeff')+"}");
#pragma warning disable 0618
            ti.spritesheet=defs.items.Select(s=>new SpriteMetaData{name=s.name,rect=new Rect(s.x,s.y,s.width,s.height),pivot=new Vector2(.5f,.5f),alignment=9,border=Vector4.one*s.border}).ToArray();
#pragma warning restore 0618
            ti.SaveAndReimport();
            var presetPath=Root+"/RuinsHengeTextureImport.preset";var preset=new UnityEditor.Presets.Preset(ti);var oldPreset=AssetDatabase.LoadAssetAtPath<UnityEditor.Presets.Preset>(presetPath);
            if(oldPreset){EditorUtility.CopySerialized(preset,oldPreset);UnityEngine.Object.DestroyImmediate(preset);}else AssetDatabase.CreateAsset(preset,presetPath);
            var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s=>s.name);
            var atlasPath=Root+"/RuinsHengeUI.spriteatlas";var atlas=AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if(!atlas){atlas=new SpriteAtlas();AssetDatabase.CreateAsset(atlas,atlasPath);}
            var packing=atlas.GetPackingSettings();packing.enableRotation=false;packing.enableTightPacking=false;packing.padding=4;atlas.SetPackingSettings(packing);
            var ts=atlas.GetTextureSettings();ts.generateMipMaps=false;ts.filterMode=FilterMode.Bilinear;atlas.SetTextureSettings(ts);
            atlas.Remove(atlas.GetPackables());atlas.Add(sprites.Values.Cast<UnityEngine.Object>().ToArray());
            var theme=Asset<UiTheme>(Root+"/RuinsHengeTheme.asset");
            var original=AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/_Project/Data/UiTheme_Default.asset");EditorUtility.CopySerialized(original,theme);theme.name="RuinsHengeTheme";
            theme.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Shared/Fonts/Jua-Regular.ttf");
            theme.ink=C("EEE6D4");theme.dim=C("A0B0BA");theme.gold=C("C9AB6B");theme.stone=C("B9BFC0");theme.buttonBg=C("263B48");theme.panelBg=C("142238");
            // Plate colors express puzzle channels, not triplet identity; retain the existing channel mapping.
            theme.settingsPanel=Panel<SettingsPanel>(original.settingsPanel.gameObject,"SettingsPanel",sprites,theme);
            theme.resultPanel=Panel<ResultView>(original.resultPanel.gameObject,"ResultPanel",sprites,theme);
            var resource=Asset<RuinsHengeUiResources>(Root+"/RuinsHengeResources.asset");resource.theme=theme;resource.atlas=atlas;
            resource.buttonDefault=sprites["button_default"];resource.buttonHover=sprites["button_hover"];resource.buttonPressed=sprites["button_pressed"];resource.buttonDisabled=sprites["button_disabled"];
            resource.panelStone=sprites["panel_stone"];resource.panelWood=sprites["panel_wood"];
            resource.undo=sprites["undo"];resource.restart=sprites["restart"];resource.hint=sprites["hint"];resource.settings=sprites["settings"];resource.sound=sprites["sound"];
            resource.starEmpty=sprites["empty"];resource.starFilled=sprites["star"];resource.bodyA=sprites["body_a"];resource.bodyB=sprites["body_b"];resource.bodyC=sprites["body_c"];resource.bodyStone=sprites["body_stone"];
            BuildDipper(resource);
            foreach(var o in new UnityEngine.Object[]{theme,resource,atlas})EditorUtility.SetDirty(o);
            AssetDatabase.SaveAssets();var issues=new List<string>();theme.Validate(issues);if(issues.Count>0)throw new Exception(string.Join("\n",issues));
            if(sprites.Count!=17)throw new Exception("Missing UI slices");
            Debug.Log("[RuinsHengeUI] Theme validation OK; 17 slices, 9-slice borders, shared atlas, panel references intact");
        }
        static void BuildDipper(RuinsHengeUiResources resources)
        {
            var go=new GameObject("DipperCounter",typeof(RectTransform));var root=go.GetComponent<RectTransform>();root.sizeDelta=new Vector2(360,132);
            Vector2 Point(int i)=>new Vector2((resources.dipper[i].x-.5f)*360,(.5f-resources.dipper[i].y)*132-12);
            foreach(var edge in new[]{(0,1),(1,2),(2,3),(3,0),(3,4),(4,5),(5,6)})
            {
                var line=new GameObject("Line_"+(edge.Item1+1)+"_"+(edge.Item2+1),typeof(RectTransform),typeof(Image));var r=line.GetComponent<RectTransform>();r.SetParent(root,false);var a=Point(edge.Item1);var b=Point(edge.Item2);r.anchoredPosition=(a+b)*.5f;r.sizeDelta=new Vector2(Vector2.Distance(a,b),2);r.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);line.GetComponent<Image>().color=new Color(.79f,.67f,.42f,.55f);line.GetComponent<Image>().raycastTarget=false;
            }
            for(int i=0;i<7;i++)
            {
                var star=new GameObject("Star_"+(i+1),typeof(RectTransform),typeof(Image));var r=star.GetComponent<RectTransform>();r.SetParent(root,false);r.anchoredPosition=Point(i);r.sizeDelta=new Vector2(28,28);var image=star.GetComponent<Image>();image.sprite=resources.starEmpty;image.raycastTarget=false;
                var label=new GameObject("Number_"+(i+1),typeof(RectTransform),typeof(Text));var lr=label.GetComponent<RectTransform>();lr.SetParent(r,false);lr.anchoredPosition=new Vector2(0,-25);lr.sizeDelta=new Vector2(32,22);var text=label.GetComponent<Text>();text.text=(i+1).ToString();text.font=resources.theme.font;text.fontSize=16;text.alignment=TextAnchor.MiddleCenter;text.color=resources.theme.ink;text.raycastTarget=false;
            }
            PrefabUtility.SaveAsPrefabAsset(go,Root+"/DipperCounter.prefab");UnityEngine.Object.DestroyImmediate(go);
        }
        static T Panel<T>(GameObject source,string name,Dictionary<string,Sprite> sprites,UiTheme theme) where T:Component
        {
            var go=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(source));
            foreach(var text in go.GetComponentsInChildren<Text>(true)){text.font=theme.font;text.color=theme.ink;}
            foreach(var button in go.GetComponentsInChildren<Button>(true))
            {
                var image=button.GetComponent<Image>();if(image){image.sprite=sprites["button_default"];image.type=Image.Type.Sliced;image.color=Color.white;}
                button.transition=Selectable.Transition.SpriteSwap;button.spriteState=new SpriteState{highlightedSprite=sprites["button_hover"],pressedSprite=sprites["button_pressed"],disabledSprite=sprites["button_disabled"],selectedSprite=sprites["button_hover"]};
            }
            foreach(var image in go.GetComponentsInChildren<Image>(true))if(!image.GetComponent<Button>()&&image.rectTransform.rect.width>500){image.sprite=sprites["panel_stone"];image.type=Image.Type.Sliced;image.color=Color.white;}
            var saved=PrefabUtility.SaveAsPrefabAsset(go,Root+"/"+name+".prefab");PrefabUtility.UnloadPrefabContents(go);return saved.GetComponent<T>();
        }
    }
}
#endif
