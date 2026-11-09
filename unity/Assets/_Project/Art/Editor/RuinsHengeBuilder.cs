#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace SevenSteps.Art
{
    public static class RuinsHengeBuilder
    {
        const string Art="Assets/_Project/Art/RuinsHenge";
        const string Prefabs="Assets/_Project/Prefabs/Tiles/RuinsHenge";
        const string Shared="Assets/Shared/Art/RuinsHenge";
        const string ScenePath="Assets/_Project/Art/Test/RuinsHenge_Preview.unity";
        const string SetPath=Art+"/RuinsHenge_Chapter01.asset";
        static readonly string[] Roles={"BaseAtlas","PlayerAtlas","MenhirAtlas","MetalAtlas","FrostOpaque","MarkUnlit"};
        static readonly Dictionary<string,Material> Mats=new Dictionary<string,Material>();
        static readonly Dictionary<string,GameObject> Built=new Dictionary<string,GameObject>();
        static Color Hex(string h){ColorUtility.TryParseHtmlString("#"+h,out var c);return c;}
        static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;var parent=Path.GetDirectoryName(p).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(p));}

        [MenuItem("Seven Steps/Art/Build Ruins Henge Chapter01")]
        public static void Build()
        {
            Folder(Art+"/Materials");Folder(Art+"/Meshes");Folder(Prefabs);Folder("Assets/_Project/Art/Test");
            AssetDatabase.Refresh();
            var shader=Shader.Find("SevenSteps/RuinsSurface");
            if(shader==null)throw new Exception("RuinsSurface shader missing");
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(Shared+"/Textures/RH_Atlas512_Triplets.png");
            if(atlas==null)throw new Exception("Shared atlas missing: run tools/link-shared-assets.ps1");
            foreach(var texture in Directory.GetFiles(Shared+"/Textures","*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(texture.Replace('\\','/'));
                importer.sRGBTexture=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            }
            foreach(var role in Roles)
            {
                var path=Art+"/Materials/"+role+".mat";
                var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                var roleShader=(role=="BaseAtlas"||role=="MarkUnlit")?shader:Shader.Find("Universal Render Pipeline/Simple Lit");
                if(m==null){m=new Material(roleShader);AssetDatabase.CreateAsset(m,path);}else m.shader=roleShader;
                m.SetTexture("_BaseMap",atlas);m.SetColor("_BaseColor",Color.white);
                m.SetFloat("_UseAtlas",1);m.SetFloat("_UseMoss",role=="BaseAtlas"?1:0);
                m.SetColor("_MossColor",Hex("6F8B4E"));m.SetFloat("_UseVertexColor",0);m.SetFloat("_Unlit",0);
                if(role=="PlayerAtlas")m.SetColor("_BaseColor",Hex("DDD4BD"));
                if(role=="MenhirAtlas"){m.SetFloat("_UseAtlas",0);m.SetTexture("_BaseMap",null);m.SetColor("_BaseColor",Hex("B9BFC0"));}
                if(role=="MarkUnlit"){m.SetFloat("_UseAtlas",0);m.SetFloat("_UseVertexColor",1);m.SetFloat("_Unlit",1);}
                EditorUtility.SetDirty(m);Mats[role]=m;
            }
            foreach(var path in Directory.GetFiles(Shared+"/Models","*.fbx").Select(p=>p.Replace('\\','/')))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;
                importer.animationType=ModelImporterAnimationType.None;importer.importAnimation=false;
                importer.importCameras=false;importer.importLights=false;importer.importBlendShapes=false;
                importer.importNormals=ModelImporterNormals.Import;importer.isReadable=false;
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                foreach(var role in Roles)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"SS_RH_M_"+role),Mats[role]);
                importer.SaveAndReimport();
            }
            foreach(var pair in new[]{("Floor","SS_Tile_Floor_Moss"),("FloorAlt","SS_Tile_Floor_Clean"),("Wall","SS_Tile_Wall"),("Pit","SS_Tile_PitRim"),("Ice","SS_Tile_Ice"),("Cracked","SS_Tile_Cracked"),("Bridge","SS_Tile_Bridge"),("Exit","SS_Tile_Exit"),("Gate","SS_Tile_NumberDoor"),("Door","SS_Tile_NumberDoor"),("CountDoor","SS_Tile_CountDoor"),("Lever","SS_Tile_Lever"),("FlipUp","SS_Tile_FlipWall_Up"),("FlipDown","SS_Tile_FlipWall_Down"),("Plate","SS_Tile_Plate"),("PlayerA","SS_Actor_Triplet_A"),("PlayerB","SS_Actor_Triplet_B"),("PlayerC","SS_Actor_Triplet_C"),("Statue","SS_Actor_Triplet_Menhir")})
                Built[pair.Item1]=MakePrefab(pair.Item1,pair.Item2);
            Built["Start"]=Marker("Start",Hex("568C88"),.38f);
            Built["OldStart"]=Marker("OldStart",Hex("76847A"),.34f);
            Built["Footprint"]=Marker("Footprint",Hex("C5AE78"),.10f);
            Built["LuckyRing"]=Marker("LuckyRing",Hex("C5AE78"),.44f);
            var set=AssetDatabase.LoadAssetAtPath<TileSet>(SetPath);
            if(set==null){set=ScriptableObject.CreateInstance<TileSet>();AssetDatabase.CreateAsset(set,SetPath);}
            set.projection=BoardProjection.Quarter;set.cellSize=1;
            set.cameraRig=new CameraRig{pitch=50,yaw=0,orthographic=true,distance=30,padding=.7f,fieldOfView=30};
            set.floor=V("Floor");set.floor.randomYaw90=true;set.floorAlt=V("FloorAlt");set.floorAlt.randomYaw90=true;
            set.wall=V("Wall");set.pit=V("Pit");set.ice=V("Ice");set.cracked=V("Cracked");set.bridge=V("Bridge");set.exit=V("Exit",.12f);
            set.gate=Door("Gate");set.countDoor=Door("CountDoor");
            set.plates=Enumerable.Range(0,3).Select(i=>V("Plate",.10f)).ToArray();
            set.doors=Enumerable.Range(0,3).Select(i=>Door("Door")).ToArray();
            set.lever=V("Lever",.15f);set.lever.inactiveChild="RaisedHandle";set.lever.activeChild="PulledHandle";
            set.flipWalls=new[]{V("FlipUp"),V("FlipDown")};
            set.player=V("PlayerA",1.04f);set.statue=V("Statue");set.startMarker=V("Start");set.oldStartMarker=V("OldStart");set.footprint=V("Footprint",.035f);set.luckyRing=V("LuckyRing");
            EditorUtility.SetDirty(set);AssetDatabase.SaveAssets();
            Validate();MakeScene(set);AssetDatabase.SaveAssets();
            Debug.Log("[RuinsHenge] BUILD OK: "+SetPath+"; 6 materials; A/B/C + KEEP; preview scene isolated");
        }
        static GameObject MakePrefab(string key,string modelName)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Shared+"/Models/"+modelName+".fbx");
            if(prefab==null)throw new Exception("FBX missing: "+modelName);
            var root=new GameObject(key);var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            model.name="Model";model.transform.SetParent(root.transform,false);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>Mats[Roles.FirstOrDefault(r=>m!=null&&m.name.Contains(r))??"BaseAtlas"]).ToArray();
            if((key=="PlayerB"||key=="PlayerC"||key=="Statue")&&Built.ContainsKey("PlayerA"))
            {
                var original=Built["PlayerA"].GetComponentsInChildren<MeshFilter>(true).First(f=>f.name.EndsWith("__Body"));
                model.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name.EndsWith("__Body")).sharedMesh=original.sharedMesh;
            }
            var bounds=BoundsOf(root);
            if(key=="Wall")model.transform.localScale=Vector3.Scale(model.transform.localScale,new Vector3(1,.65f,1));
            if(key.StartsWith("Player")||key=="Statue")model.transform.localScale*=.88f/bounds.size.y;
            else if(bounds.size.x>2||bounds.size.z>2)model.transform.localScale*=.96f/Mathf.Max(bounds.size.x,bounds.size.z);
            bounds=BoundsOf(root);
            bool flat=new[]{"Floor","FloorAlt","Ice","Cracked","Bridge"}.Contains(key);
            model.transform.localPosition+=new Vector3(-bounds.center.x,flat?-bounds.max.y:((key.StartsWith("Player")||key=="Statue")?-bounds.min.y:0),-bounds.center.z);
            if(key.StartsWith("Player"))Ring(root.transform,"IdentityRing",key=="PlayerA"?Hex("568C88"):key=="PlayerB"?Hex("B99A57"):Hex("B96C59"),.30f,.028f);
            if(key=="Pit"){var q=GameObject.CreatePrimitive(PrimitiveType.Cube);q.name="SpaceCavity";q.transform.SetParent(root.transform,false);q.transform.localPosition=new Vector3(0,-.09f,0);q.transform.localScale=new Vector3(.76f,.01f,.76f);UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());q.GetComponent<Renderer>().sharedMaterial=Mats["MenhirAtlas"];}
            if(key=="Lever")
            {
                var handle=root.GetComponentsInChildren<Transform>(true).First(t=>t.name.EndsWith("__HandlePivot"));
                handle.SetParent(root.transform,true);handle.name="RaisedHandle";
                var pulled=UnityEngine.Object.Instantiate(handle.gameObject,root.transform);pulled.name="PulledHandle";pulled.transform.localRotation*=Quaternion.Euler(0,0,-55);pulled.SetActive(false);
            }
            var result=PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"/"+key+".prefab");UnityEngine.Object.DestroyImmediate(root);return result;
        }
        static Bounds BoundsOf(GameObject go){var list=go.GetComponentsInChildren<Renderer>(true);var b=list[0].bounds;foreach(var r in list)b.Encapsulate(r.bounds);return b;}
        static TileVisual V(string key,float label=0)=>new TileVisual{prefab=Built[key],scale=Vector3.one,labelOffset=new Vector3(0,label,0)};
        static TileVisual Door(string key)
        {
            var v=V(key,.66f);var panel=v.prefab.GetComponentsInChildren<Transform>(true).First(t=>t.name.EndsWith("__GatePanel"));
            v.inactiveChild=AnimationUtility.CalculateTransformPath(panel,v.prefab.transform);v.materialTarget=v.inactiveChild;
            v.activeMaterial=Mats["MetalAtlas"];v.inactiveMaterial=Mats["BaseAtlas"];v.activeLabelOffset=new Vector3(0,.1f,0);return v;
        }
        static void Ring(Transform parent,string name,Color color,float radius,float width)
        {
            var mesh=new Mesh{name=name};const int n=48;var vertices=new Vector3[n*2];var uv=new Vector2[n*2];var colors=new Color[n*2];var triangles=new int[n*6];
            for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;for(int j=0;j<2;j++){int k=i*2+j;float r=radius+(j==0?-width/2:width/2);vertices[k]=new Vector3(Mathf.Cos(a)*r,.012f,Mathf.Sin(a)*r);colors[k]=QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color;uv[k]=Vector2.zero;}int t=i*6,b=i*2,c=((i+1)%n)*2;triangles[t]=b;triangles[t+1]=c;triangles[t+2]=b+1;triangles[t+3]=b+1;triangles[t+4]=c;triangles[t+5]=c+1;}
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.colors=colors;mesh.uv=uv;mesh.RecalculateNormals();
            string path=Art+"/Meshes/"+name+"_"+ColorUtility.ToHtmlStringRGB(color)+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null){AssetDatabase.CreateAsset(mesh,path);old=mesh;}else{EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);}
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=old;go.GetComponent<MeshRenderer>().sharedMaterial=Mats["MarkUnlit"];
        }
        static GameObject Marker(string name,Color color,float radius){var go=new GameObject(name);Ring(go.transform,name,color,radius,.025f);var p=PrefabUtility.SaveAsPrefabAsset(go,Prefabs+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(go);return p;}
        public static void Validate()
        {
            var issues=new List<string>();int errors=TileSetValidator.Validate(AssetDatabase.LoadAssetAtPath<TileSet>(SetPath),issues);
            foreach(var issue in issues)Debug.Log("[RuinsHenge] "+issue);
            if(errors>0)throw new Exception("TileSet validation errors: "+errors);
            if(AssetDatabase.FindAssets("t:Material",new[]{Art+"/Materials"}).Length>6)throw new Exception("Material budget exceeded");
            Debug.Log("[RuinsHenge] TileSet validation OK (explicit Art path)");
        }
        static GameObject Spawn(string key,Vector3 position){var o=(GameObject)PrefabUtility.InstantiatePrefab(Built[key]);o.transform.position=position;return o;}
        static void MakeScene(TileSet set)
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity",OpenSceneMode.Single);
            var configPath=Art+"/PreviewConfig.asset";var cfg=AssetDatabase.LoadAssetAtPath<AppConfig>(configPath);
            if(cfg==null){cfg=ScriptableObject.CreateInstance<AppConfig>();AssetDatabase.CreateAsset(cfg,configPath);}
            cfg.tileSet=set;cfg.telemetryEndpoint="";cfg.telemetryOnByDefault=false;EditorUtility.SetDirty(cfg);
            var controller=UnityEngine.Object.FindFirstObjectByType<GameController>();controller.config=cfg;controller.overrideCamera=false;controller.startLevel=1;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.35f,.45f,.62f);RenderSettings.ambientEquatorColor=new Color(.24f,.27f,.32f);RenderSettings.ambientGroundColor=new Color(.18f,.18f,.2f);RenderSettings.skybox=null;
            var cam=UnityEngine.Object.FindFirstObjectByType<Camera>();cam.backgroundColor=Hex("0C1735");cam.clearFlags=CameraClearFlags.SolidColor;
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)){light.color=Hex("FFE1AF");light.intensity=1.4f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(45,-35,0);}
            // Decorative sibling gallery, outside the playable grid. Game actor remains owned by BoardView.
            for(int i=-1;i<=1;i++)Spawn("Floor",new Vector3(i,0,-2.5f));
            Spawn("PlayerB",new Vector3(-1,0,-2.5f));Spawn("PlayerC",new Vector3(0,0,-2.5f));Spawn("Statue",new Vector3(1,0,-2.5f));
            controller.gameObject.AddComponent<RuinsHengeProbe>();
            Planet();
            for(int i=0;i<2;i++){var moon=GameObject.CreatePrimitive(PrimitiveType.Sphere);moon.name="DistantPlanet";moon.transform.position=new Vector3(i==0?-5.4f:5.4f,.8f,2.8f);moon.transform.localScale=Vector3.one*(i==0?1.2f:.8f);UnityEngine.Object.DestroyImmediate(moon.GetComponent<Collider>());moon.GetComponent<Renderer>().sharedMaterial=Mats[i==0?"FrostOpaque":"MetalAtlas"];UniformUV(moon,"Moon"+i,i==0?5:7);}
            EditorSceneManager.SaveScene(scene,ScenePath);
            // SceneLoader uses the name Game. An art-owned alias avoids changing its common routing.
            EditorSceneManager.SaveScene(scene,"Assets/_Project/Art/Test/Game.unity",true);
        }
        static Mesh StoreMesh(Mesh mesh,string name){var path=Art+"/Meshes/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);return old;}
        static void UniformUV(GameObject go,string name,int band){var mesh=UnityEngine.Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);mesh.uv=Enumerable.Repeat(new Vector2((band+.5f)/8,.5f),mesh.vertexCount).ToArray();mesh.colors=Enumerable.Repeat(Color.black,mesh.vertexCount).ToArray();go.GetComponent<MeshFilter>().sharedMesh=StoreMesh(mesh,name);}
        static void Planet()
        {
            const int n=64;float[] ys={-.12f,-.5f,-1.5f,-3,-4.5f,-6,-7.5f,-8};float[] rs={5.5f,5.8f,6,5.8f,5.2f,4,2,.02f};
            var verts=new List<Vector3>{new Vector3(0,-.12f,0)};var triangles=new List<int>();
            for(int j=0;j<ys.Length;j++)for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;verts.Add(new Vector3(Mathf.Cos(a)*rs[j],ys[j],Mathf.Sin(a)*rs[j]));}
            for(int i=0;i<n;i++){int next=(i+1)%n;triangles.AddRange(new[]{0,1+next,1+i});}
            for(int j=0;j<ys.Length-1;j++)for(int i=0;i<n;i++){int a=1+j*n+i,b=1+j*n+(i+1)%n,c=a+n,d=b+n;triangles.AddRange(new[]{a,b,c,c,b,d});}
            var mesh=new Mesh{name="PlanetFlatSummit"};mesh.SetVertices(verts);mesh.SetTriangles(triangles,0);mesh.uv=Enumerable.Repeat(new Vector2(2.5f/8,.5f),verts.Count).ToArray();mesh.colors=Enumerable.Repeat(Color.black,verts.Count).ToArray();mesh.RecalculateNormals();
            var go=new GameObject("Planet_FlatSummit_y_minus_012",typeof(MeshFilter),typeof(MeshRenderer));go.GetComponent<MeshFilter>().sharedMesh=StoreMesh(mesh,"PlanetFlatSummit");go.GetComponent<MeshRenderer>().sharedMaterial=Mats["BaseAtlas"];
        }
        public static void BatchBuild(){try{Build();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void BatchValidate(){try{Validate();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void FinalCheck()
        {
            try
            {
                var ids=AssetDatabase.FindAssets("",new[]{Art,Prefabs,Shared}).ToDictionary(AssetDatabase.GUIDToAssetPath,g=>g);
                Build();Validate();
                foreach(var pair in ids)if(AssetDatabase.AssetPathToGUID(pair.Key)!=pair.Value)throw new Exception("GUID changed: "+pair.Key);
                var mesh=Built["PlayerA"].GetComponentsInChildren<MeshFilter>(true).First(f=>f.name.EndsWith("__Body")).sharedMesh;
                foreach(var key in new[]{"PlayerB","PlayerC","Statue"})if(Built[key].GetComponentsInChildren<MeshFilter>(true).First(f=>f.name.EndsWith("__Body")).sharedMesh!=mesh)throw new Exception("Body mesh not shared: "+key);
                Debug.Log("[RuinsHenge] Rerun GUID stability OK: "+ids.Count+" assets; A/B/C/KEEP body Mesh shared");
                global::BuildTools.CompileCheck();
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BuildWindows()=>BuildPlayer(BuildTarget.StandaloneWindows64,"../Builds/RuinsHenge/Windows/Seven Steps.exe");
        public static void BuildPreviewWindows(){Build();BuildWindows();}
        public static void BuildWebGL()=>BuildPlayer(BuildTarget.WebGL,"../Builds/RuinsHenge/WebGL");
        static void BuildPlayer(BuildTarget target,string path){Directory.CreateDirectory(Path.GetDirectoryName(path));var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/_Project/Art/Test/Game.unity"},target=target,locationPathName=path,options=BuildOptions.None});Debug.Log("[RuinsHenge] "+target+" "+report.summary.result+" errors="+report.summary.totalErrors);EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);}
    }
}
#endif
