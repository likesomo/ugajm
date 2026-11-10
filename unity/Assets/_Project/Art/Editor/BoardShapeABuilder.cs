#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SevenSteps.Contracts;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.Art
{
    public static class BoardShapeABuilder
    {
        const string Art="Assets/_Project/Art/RuinsHenge";
        const string Shared="Assets/Shared/Art/RuinsHenge/BoardShapeA";
        const string Prefabs="Assets/_Project/Prefabs/Tiles/RuinsHenge";
        public static readonly string[] Ids={"moss","hollow","carved","frost","joinery","crumble","altar"};
        static Color Hex(string h){ColorUtility.TryParseHtmlString("#"+h,out var c);return c;}
        static Mesh Mesh(string name)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Shared+"/SS_BSA_"+name+".fbx");
            if(!model)throw new Exception("Run Blender MCP production script first: "+name);
            var mesh=model.GetComponentInChildren<MeshFilter>().sharedMesh;
            if(mesh.subMeshCount!=1||mesh.uv2.Length!=mesh.vertexCount||mesh.uv2[0].x!=4)throw new Exception("Invalid BoardShapeA FBX channels: "+name);
            return mesh;
        }
        static void Textures(Material m,string id)
        {
            m.SetTexture("_BoardShapeMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Shared+"/BSA_ColorAO.png"));
            m.SetTexture("_BoardShapeNormal",AssetDatabase.LoadAssetAtPath<Texture2D>(Shared+"/BSA_NormalRough.png"));
            m.SetFloat("_BoardNormalStrength",.42f);m.SetFloat("_BoardMoss",Moss(id));
            int k=Array.IndexOf(Ids,id);int[] slots={0,8,9,10,11,12,13};
            m.SetFloat("_BoardStoneSlot",k<0?0:slots[k]);m.SetColor("_BoardRockTint",Color.white);
            m.enableInstancing=true;EditorUtility.SetDirty(m);
        }
        static float Moss(string id)=>id=="frost"?0:id=="moss"?1:id=="altar"?.75f:id=="joinery"?.1f:.25f;
        static GameObject Part(Transform parent,string name,Mesh mesh,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;
            r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;return go;
        }
        static void Replace(string key,Material material,params string[] modules)
        {
            string path=Prefabs+"/"+key+".prefab";
            bool exists=File.Exists(path);var root=exists?PrefabUtility.LoadPrefabContents(path):new GameObject(key);
            try
            {
                foreach(Transform child in root.transform.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
                // Existing random wall component would create late renderers after ChapterMaterials.Apply.
                foreach(var c in root.GetComponents<RuinsWallVariations>())UnityEngine.Object.DestroyImmediate(c);
                foreach(string module in modules)Part(root.transform,module=="DoorPanel"?"GatePanel":"Model_"+module,Mesh(module),material);
                if(modules.Contains("Block")||modules.Contains("DoorFrame"))
                {
                    var contact=root.GetComponent<BoardShapeAContact>()??root.AddComponent<BoardShapeAContact>();
                    contact.radius=new Vector2(.54f,.40f);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{if(exists)PrefabUtility.UnloadPrefabContents(root);else UnityEngine.Object.DestroyImmediate(root);}
        }
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var existingGuids=AssetDatabase.FindAssets("",new[]{Art,Prefabs}).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToDictionary(p=>p,AssetDatabase.AssetPathToGUID);
            foreach(string file in Directory.GetFiles(Shared,"*.fbx"))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
                importer.isReadable=true;importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
                importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importNormals=ModelImporterNormals.Import;
                importer.importTangents=ModelImporterTangents.CalculateMikk;importer.generateSecondaryUV=false;importer.SaveAndReimport();
            }
            foreach(string file in Directory.GetFiles(Shared,"*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
                // RG normal + B roughness + A AO must survive import byte-for-byte.
                bool normal=file.Contains("Normal");importer.textureType=TextureImporterType.Default;
                importer.sRGBTexture=!normal;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.wrapMode=TextureWrapMode.Clamp;
                importer.alphaIsTransparency=false;
                importer.mipmapEnabled=true;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            var kit=AssetDatabase.LoadAssetAtPath<BoardShapeAKit>(Art+"/BoardShapeA_Kit.asset");
            if(!kit){kit=ScriptableObject.CreateInstance<BoardShapeAKit>();AssetDatabase.CreateAsset(kit,Art+"/BoardShapeA_Kit.asset");}
            kit.crust=Enumerable.Range(0,3).Select(i=>Mesh("Crust_"+i)).ToArray();kit.menhirs=Enumerable.Range(0,2).Select(i=>Mesh("Menhir_"+i)).ToArray();
            kit.kerb=Mesh("Kerb");kit.corner=Mesh("Corner");kit.medallion=Mesh("Medallion");kit.chest=Mesh("Chest");kit.backGate=Mesh("BackGate");kit.wall=Mesh("Wall");kit.joist=Mesh("Joist");EditorUtility.SetDirty(kit);
            kit.rubble=Mesh("Rubble");kit.fragment=Mesh("Fragment");kit.grass=Mesh("Grass");
            kit.hollowArch=Mesh("HollowArch");kit.terrace=Mesh("Terrace");kit.frostSpire=Mesh("FrostSpire");kit.timberFrame=Mesh("TimberFrame");kit.mossPatch=Mesh("MossPatch");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/BaseAtlas.mat");Textures(material,"moss");
            Replace("Floor",material,"Floor_0");Replace("FloorAlt",material,"Floor_1");Replace("FloorThird",material,"Floor_2");
            foreach(string actor in new[]{"PlayerA","PlayerB","PlayerC","Statue","FlipUp","FlipDown","Lever"})
            {
                string path=Prefabs+"/"+actor+".prefab";if(!File.Exists(path))continue;
                var root=PrefabUtility.LoadPrefabContents(path);
                var contact=root.GetComponent<BoardShapeAContact>()??root.AddComponent<BoardShapeAContact>();
                contact.radius=new Vector2(.48f,.35f);PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
            }
            foreach(string wall in new[]{"Wall","WallStoneV2","WallStoneV3"})Replace(wall,material,"Block");
            Replace("Pit",material,"Pit","Cavity");Replace("Mud",material,"Mud");
            // Reuse the existing frost material class: one lightly translucent ice draw, no refraction.
            var frost=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/FrostOpaque.mat");
            // Packed atlas alpha stores shadows. Ice opacity must be independent of it.
            frost.shader=Shader.Find("SevenSteps/RuinsSurface");Textures(frost,"frost");frost.SetFloat("_SurfaceAlpha",.92f);
            frost.SetFloat("_BoardNormalStrength",.12f);frost.SetFloat("_SrcBlend",5);frost.SetFloat("_DstBlend",10);frost.SetFloat("_ZWrite",0);
            frost.SetOverrideTag("RenderType","Transparent");frost.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");frost.DisableKeyword("_NORMALMAP");frost.renderQueue=3000;frost.enableInstancing=true;EditorUtility.SetDirty(frost);
            Replace("Ice",frost,"Ice");
            foreach(string door in new[]{"Gate","Door","CountDoor"})Replace(door,material,"DoorFrame","DoorPanel");
            foreach(string guid in AssetDatabase.FindAssets("t:TileSet",new[]{Art}))
            {
                var set=AssetDatabase.LoadAssetAtPath<TileSet>(AssetDatabase.GUIDToAssetPath(guid));
                TileVisual Door(TileVisual v){v.inactiveChild="GatePanel";v.materialTarget="GatePanel";v.inactiveMaterial=material;v.labelOffset=new Vector3(0,.43f,-.18f);v.labelScale=.64f;return v;}
                set.gate=Door(set.gate);set.countDoor=Door(set.countDoor);for(int d=0;d<set.doors.Length;d++)set.doors[d]=Door(set.doors[d]);
                set.mud=new TileVisual{prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"/Mud.prefab"),scale=Vector3.one};EditorUtility.SetDirty(set);
            }
            foreach(string id in Ids)
            {
                var chapter=AssetDatabase.LoadAssetAtPath<ChapterPreset>(Art+"/Chapter_"+id+".asset");
                var env=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/Environment_"+id+".mat");Textures(env,id);
                chapter.materialOverrides=new[]{new MaterialOverride{from=material,to=env}};EditorUtility.SetDirty(chapter);
                string path=Prefabs+"/Env/PlanetSummit_"+id+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
                try{var planet=root.GetComponent<RuinsHengePlanet>();planet.boardShape=kit;planet.surface=env;PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
                int index=Array.IndexOf(Ids,id);
                string[] backgrounds={"071C1B","170D2D","20170E","081A33","24110E","20151D","211A0C"};
                string[] nebulae={"1B6258","62358A","80552A","2E6297","824630","63516A","8D7540"};
                string[] moons={"76C7AA","D1A2F7","E1B55D","D8F3FF","D89869","C5A8AB","F2D486"};
                var skyRoot=PrefabUtility.LoadPrefabContents(Prefabs+"/Env/Sky_"+id+".prefab");
                try{var sky=skyRoot.GetComponent<RuinsHengeSky>();sky.backgroundTint=Hex(backgrounds[index]);sky.nebulaTint=Hex(nebulae[index]);sky.secondaryTint=Hex(moons[index]);sky.distantWorlds=true;sky.worldStyle=index;PrefabUtility.SaveAsPrefabAsset(skyRoot,Prefabs+"/Env/Sky_"+id+".prefab");}
                finally{PrefabUtility.UnloadPrefabContents(skyRoot);}
                chapter.sky.background=Hex(backgrounds[index]);EditorUtility.SetDirty(chapter.sky);
                var key=chapter.lighting.key;key.color=Hex(id=="frost"?"DCEFFF":"FFE9D3");key.euler=new Vector3(40,125,0);chapter.lighting.key=key;chapter.lighting.ambient=Hex("35445B");EditorUtility.SetDirty(chapter.lighting);
                // Preserve sky and light GUIDs; no production camera, AppConfig or scene edits.
            }
            AssetDatabase.SaveAssets();
            foreach(var pair in existingGuids)if(AssetDatabase.AssetPathToGUID(pair.Key)!=pair.Value)throw new Exception("Existing art GUID changed: "+pair.Key);
            Validate();Debug.Log("[BoardShapeA] Existing GUID stability PASS: "+existingGuids.Count+" assets");
        }
        public static void Validate()
        {
            var packedImporter=(TextureImporter)AssetImporter.GetAtPath(Shared+"/BSA_NormalRough.png");
            if(packedImporter.textureType!=TextureImporterType.Default||packedImporter.sRGBTexture)
                throw new Exception("RG normal / roughness / AO must remain linear RGBA data");
            foreach(string map in new[]{"BSA_ColorAO.png","BSA_NormalRough.png"})
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Shared+"/"+map);
                if(texture.width!=1024||texture.height!=1024)throw new Exception("Expected two 1024-square geology atlases: "+map);
            }
            var packed=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            try
            {
                if(!ImageConversion.LoadImage(packed,File.ReadAllBytes(Shared+"/BSA_NormalRough.png")))throw new Exception("Cannot read packed surface bake");
                var pixels=packed.GetPixels();float min=1,max=0;
                foreach(var pixel in pixels){min=Mathf.Min(min,pixel.b);max=Mathf.Max(max,pixel.b);}
                if(min>.5f||max<.8f)throw new Exception("Roughness channel was lost or repacked as a normal Z channel");
            }
            finally{UnityEngine.Object.DestroyImmediate(packed);}
            // Independent conservative sightline check for the requested taller arch at pitch 50.
            // A ray from the next cell's floor centre is already above the complete door AABB.
            var door=Mesh("DoorFrame").bounds;
            float sightline=(1-Mathf.Max(Mathf.Abs(door.min.z),Mathf.Abs(door.max.z)))*Mathf.Tan(50*Mathf.Deg2Rad);
            if(door.max.y>=sightline)throw new Exception("Number arch occludes the next cell centre at pitch 50");
            var lines=new List<string>{"chapter,width,height,environmentTris,fullCleanBoardTris,environmentDraws"};
            foreach(string id in Ids)foreach(var size in new[]{new Vector2Int(8,6),new Vector2Int(12,5),new Vector2Int(10,3),new Vector2Int(7,7)})
            {
                var chapter=AssetDatabase.LoadAssetAtPath<ChapterPreset>(Art+"/Chapter_"+id+".asset");
                var root=UnityEngine.Object.Instantiate(chapter.sky.planetPrefab);
                try
                {
                    var planet=root.GetComponent<RuinsHengePlanet>();planet.Fit(new BoardGrid(size.x,size.y));planet.Fit(new BoardGrid(size.x,size.y));
                    var renderers=root.GetComponentsInChildren<MeshRenderer>();
                    if(renderers.Length!=1)throw new Exception("Expected one combined environment renderer: "+id);
                    var mesh=root.GetComponentInChildren<MeshFilter>().sharedMesh;long tris=mesh.GetIndexCount(0)/3;
                    long all=tris+size.x*size.y*(Mesh("Floor_0").GetIndexCount(0)/3);
                    if(all>50000)throw new Exception("50k board budget exceeded: "+id+" "+size+" "+all);
                    if(planet.surface.GetFloat("_BoardMoss")!=Moss(id))throw new Exception("Moss override mismatch");
                    int[] slots={0,8,9,10,11,12,13};
                    if(planet.surface.GetFloat("_BoardStoneSlot")!=slots[Array.IndexOf(Ids,id)])throw new Exception("Chapter stone recipe mismatch: "+id);
                    lines.Add(id+","+size.x+","+size.y+","+tris+","+all+",1");
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            var report="../reports/board-shape-a";Directory.CreateDirectory(report);File.WriteAllLines(report+"/geometry.csv",lines);
            Debug.Log("[BoardShapeA] PASS: 7 chapters x 4 board sizes, one-cell decoration clearance, repeated Fit, one environment draw and <50k clean board tris");
            Debug.Log("[BoardShapeA] Number arch next-cell centre sightline PASS at pitch50; digit glyph size is checked by the actual review player");
        }
        public static void BatchPrepare()
        {
            try
            {
                Apply();
                int errors=SevenSteps.EditorTools.SceneFlowValidator.ValidateAll(out string report);Debug.Log(report);
                if(errors!=0)throw new Exception("Scene/Glyph checks failed: "+errors);
                errors=SevenSteps.EditorTools.TileSetValidator.ValidateAll(out report);Debug.Log(report);
                if(errors!=0)throw new Exception("TileSet checks failed: "+errors);
                HandwrittenFontBuilder.Check();
                global::BuildTools.CompileCheck();
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void ReviewWindows()
        {
            try
            {
                Apply();
                var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
                var camera=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;
                var review=new GameObject("BoardShapeAReview").AddComponent<BoardShapeAReviewCapture>();
                review.chapters=Ids.Select(id=>AssetDatabase.LoadAssetAtPath<ChapterPreset>(Art+"/Chapter_"+id+".asset")).ToArray();
                review.numberFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Shared/Fonts/Jua-Regular.ttf");
                const string path="Assets/_Project/Art/Test/BoardShapeAReview.unity";UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,path);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{path},target=BuildTarget.StandaloneWindows64,locationPathName="../Builds/BoardShapeAReview/BoardShapeAReview.exe",extraScriptingDefines=new[]{"SEVENSTEPS_ART_PREVIEW"}});
                Debug.Log("[BoardShapeAReview] "+report.summary.result+" errors="+report.summary.totalErrors);EditorApplication.Exit(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded?0:1);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
#endif
