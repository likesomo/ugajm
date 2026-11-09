#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using SevenSteps.EditorTools;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
namespace SevenSteps.Art
{
    public static class HandwrittenFontBuilder
    {
        const string Output="C:/unityjam/shared-assets/seven-steps/concepts/ui";
        const string Scene="Assets/_Project/Art/Test/HandwrittenUI.unity";
        static Font Font(string name)=>AssetDatabase.LoadAssetAtPath<Font>("Assets/Shared/Fonts/"+name+"-Regular.ttf");
        public static void Check()
        {
            AssetDatabase.Refresh();Directory.CreateDirectory(Output);
            var assets=AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");
            var log=new StringBuilder();var theme=ScriptableObject.CreateInstance<UiTheme>();
            foreach(string name in new[]{"Jua","Gaegu","GamjaFlower","HiMelody","PatrickHand","KaushanScript"})
            {
                var path=AssetDatabase.GetAssetPath(Font(name));var bytes=File.ReadAllBytes(path);var cmap=GlyphCheck.ReadCmap(bytes);
                log.AppendLine(name+" bytes="+bytes.Length+" cmap="+cmap.Count+" hangul="+cmap.Count(c=>c>=0xAC00&&c<=0xD7A3)+" sha256="+BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(bytes)).Replace("-","").ToLowerInvariant());
                if(name=="Jua"||name=="Gaegu"||name=="GamjaFlower"||name=="HiMelody")
                {
                    theme.font=Font(name);var issues=new List<string>();int missing=GlyphCheck.Check(theme,assets,issues);log.AppendLine("  GLOBAL GlyphCheck missing="+missing);foreach(var issue in issues)log.AppendLine(issue);if(name=="Gaegu"&&missing>0)throw new Exception("Gaegu does not cover game text");
                }
            }
            var literals=GlyphCheck.StringLiterals(File.ReadAllText("Assets/_Project/Art/UI/HandwrittenUiPreview.cs")).Select(v=>v.text).ToArray();
            foreach(var role in new[]{("EN-title","KaushanScript",false),("EN-body","PatrickHand",false),("KO","Gaegu",true),("numbers","Jua",false)})
            {
                string corpus=role.Item1=="numbers"?"0123456789 =N+-/()":string.Join("",literals);
                var cps=corpus.Where(c=>c>=32&&(role.Item3||c<128)).Select(c=>(int)c).Distinct().ToArray();var cmap=GlyphCheck.ReadCmap(File.ReadAllBytes(AssetDatabase.GetAssetPath(Font(role.Item2))));
                var missing=cps.Where(c=>!cmap.Contains(c)&&!GlyphCheck.Allowed.Contains(c)).ToArray();
                log.AppendLine("LOCAL "+role.Item1+" chars="+cps.Length+" missing="+string.Join(",",missing.Select(c=>"U+"+c.ToString("X4"))));if(missing.Length>0)throw new Exception("Localized preview glyphs missing: "+role.Item1);
            }
            var future=File.ReadAllText("../design/levels/story-50.json")+File.ReadAllText("../design/levels/chapters.json");var gaegu=GlyphCheck.ReadCmap(File.ReadAllBytes(AssetDatabase.GetAssetPath(Font("Gaegu"))));var absent=future.Where(c=>c>=0xAC00&&c<=0xD7A3&&!gaegu.Contains(c)).Distinct().ToArray();log.AppendLine("50-stage story/chapter Hangul missing="+new string(absent));if(absent.Length>0)throw new Exception("Future Korean story glyphs missing");
            theme.font=Font("Jua");var stable=new List<string>();int errors=GlyphCheck.Check(theme,assets,stable);UnityEngine.Object.DestroyImmediate(theme);
            File.WriteAllText(Output+"/font-coverage.txt",log.ToString());Debug.Log(log.ToString());if(errors!=0)throw new Exception("Existing fallback GlyphCheck failed");
            Buttons();
        }
        static void Buttons()
        {
            var kit=AssetDatabase.LoadAssetAtPath<RuinsHengeUiResources>("Assets/_Project/Art/UI/RuinsHengeResources.asset");
            foreach(bool ko in new[]{false,true})
            {
                var go=new GameObject("HandwrittenButton_"+(ko?"KO":"EN"),typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));
                go.GetComponent<RectTransform>().sizeDelta=new Vector2(370,55);var image=go.GetComponent<UnityEngine.UI.Image>();image.sprite=kit.buttonDefault;image.type=UnityEngine.UI.Image.Type.Sliced;
                var button=go.GetComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.transition=UnityEngine.UI.Selectable.Transition.SpriteSwap;button.spriteState=new UnityEngine.UI.SpriteState{highlightedSprite=kit.buttonHover,selectedSprite=kit.buttonHover,pressedSprite=kit.buttonPressed,disabledSprite=kit.buttonDisabled};
                var child=new GameObject("Label",typeof(RectTransform),typeof(UnityEngine.UI.Text));var r=child.GetComponent<RectTransform>();r.SetParent(go.transform,false);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(12,6);r.offsetMax=new Vector2(-12,-6);
                var label=child.GetComponent<UnityEngine.UI.Text>();label.font=Font(ko?"Gaegu":"PatrickHand");label.fontSize=25;label.lineSpacing=ko?1.1f:.9f;label.alignment=TextAnchor.MiddleCenter;label.color=new Color32(238,230,212,255);label.text=ko?"여정 시작":"Begin the Journey";label.raycastTarget=false;
                PrefabUtility.SaveAsPrefabAsset(go,"Assets/_Project/Art/UI/"+go.name+".prefab");UnityEngine.Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
        }
        static void Make(bool baseline)
        {
            AssetDatabase.Refresh();foreach(var name in new[]{"Jua","KaushanScript","PatrickHand","Gaegu"}){var fi=(TrueTypeFontImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Font(name)));fi.includeFontData=true;fi.fontTextureCase=FontTextureCase.Dynamic;fi.SaveAndReimport();}
            var bgPath="Assets/Shared/UI/RuinsHenge/Handwritten_Background.png";var importer=(TextureImporter)AssetImporter.GetAtPath(bgPath);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.SaveAndReimport();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var camera=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.11f,.2f);
            var preview=new GameObject("ActualFontPreview").AddComponent<HandwrittenUiPreview>();preview.numbers=Font("Jua");preview.englishTitle=baseline?Font("Jua"):Font("KaushanScript");preview.englishBody=baseline?Font("Jua"):Font("PatrickHand");preview.koreanTitle=baseline?Font("Jua"):Font("Gaegu");preview.koreanBody=baseline?Font("Jua"):Font("Gaegu");preview.background=AssetDatabase.LoadAssetAtPath<Sprite>(bgPath);preview.kit=AssetDatabase.LoadAssetAtPath<RuinsHengeUiResources>("Assets/_Project/Art/UI/RuinsHengeResources.asset");
            EditorSceneManager.SaveScene(scene,Scene);AssetDatabase.SaveAssets();
        }
        public static void CompileAndPrepare(){try{Check();Make(false);global::BuildTools.CompileCheck();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void Windows(){try{Check();Make(false);Build(BuildTarget.StandaloneWindows64,"../Builds/UiFonts/Windows/UiFonts.exe");}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void BaselineWebGL(){try{Make(true);Build(BuildTarget.WebGL,"../Builds/UiFonts/Baseline");}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void HandwrittenWebGL(){try{Make(false);Build(BuildTarget.WebGL,"../Builds/UiFonts/Handwritten");}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        static void Build(BuildTarget target,string path){Directory.CreateDirectory(Path.GetDirectoryName(path));var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},target=target,locationPathName=path});Debug.Log("[Fonts] "+target+" "+result.summary.result+" bytes="+result.summary.totalSize+" errors="+result.summary.totalErrors);EditorApplication.Exit(result.summary.result==BuildResult.Succeeded?0:1);}
    }
}
#endif
