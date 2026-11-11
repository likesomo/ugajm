using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Object=UnityEngine.Object;
namespace SevenSteps.Art.StageSelectReview
{
    public static partial class Capture
    {
        static Material bodyMaterial,lightMaterial;
        static readonly List<string> bodyStats=new List<string>{"planet,tris,materials"};
        public static void RunV4b(){PlanetBodyStudy.PolishV4b=true;RunV4();}
        public static void RunV4(){PlanetBodyStudy.UseV4=true;RunV3();}
        static bool V4=>PlanetBodyStudy.UseV4;
        static float Seat=>V4?2.30f:3.35f;
        [MenuItem("Seven Steps/Art/Stage Select/Capture journey v3")]
        public static void RunV3()
        {
            Output="C:/unityjam/shared-assets/seven-steps/concepts/ui/stage-select-2026-11-11/"+(PlanetBodyStudy.PolishV4b?"v4b":V4?"v4":"v3");Directory.CreateDirectory(Output);
            try
            {
                assets=AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/_Project/Data/GameAssets.asset");Ui.Theme=assets.uiTheme;levels=LevelCatalog.Load(assets);planets=PlanetEntry.FromCatalog(levels.Length);
                chapters=Ids.Select(id=>AssetDatabase.LoadAssetAtPath<ChapterPreset>("Assets/_Project/Art/RuinsHenge/Chapter_"+id+".asset")).ToArray();font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Shared/Fonts/Jua-Regular.ttf");
                copy=JsonUtility.FromJson<Copy>(File.ReadAllText("../design/story/copy-levels.json"));story=JsonUtility.FromJson<Story>(File.ReadAllText("../design/story/story.json"));
                bodyMaterial=new Material(Shader.Find(PlanetBodyStudy.PolishV4b?"SevenSteps/Review/PlanetDepthV4b":"SevenSteps/RuinsSurface")){name="Shared vertex-colour planet stone",hideFlags=HideFlags.HideAndDontSave};
                bodyMaterial.SetColor("_BaseColor",Color.white);bodyMaterial.SetFloat("_UseMoss",0);bodyMaterial.SetFloat("_SurfaceAlpha",1);bodyMaterial.SetFloat("_UseAtlas",0);bodyMaterial.SetFloat("_UseVertexColor",1);bodyMaterial.SetFloat("_EnvironmentDetail",.12f);bodyMaterial.SetFloat("_AmbientFloor",.42f);
                lightMaterial=new Material(bodyMaterial){name="Shared ground starlight",hideFlags=HideFlags.HideAndDontSave};lightMaterial.SetFloat("_Unlit",1);
                worlds=BakeBodiesV3();language="ko";
                if(PlanetBodyStudy.PolishV4b){MapV3("default",-1,0);MapV3("focused",0,0);}
                else if(V4){MapV3("default",-1,0);MapV3("focused",0,0);MapV3("mid-game",3,3);SurfaceV3(0,"default",-1,0);for(int i=0;i<7;i++){DiveV3(i);AdvanceV3(i);}}
                else {                MapV3("default",-1,0);MapV3("focused",0,0);MapV3("mid-game",3,3);MapV3("all-complete",-1,7);
                foreach(int p in new[]{0,6}){SurfaceV3(p,"default",-1,0);SurfaceV3(p,"focused",3,0);SurfaceV3(p,"cleared-mix",3,3);}
                language="en";MapV3("focused",0,0);language="ko";
                for(int i=0;i<7;i++){DiveV3(i);AdvanceV3(i);CompleteV3(i);}
                }
                File.WriteAllLines(Path.Combine(Output,"performance.csv"),perf);File.WriteAllLines(Path.Combine(Output,"planet-bodies.csv"),bodyStats);
                File.WriteAllText(Path.Combine(Output,"capture-source.txt"),"Unity "+Application.unityVersion+"; editor-only "+(PlanetBodyStudy.PolishV4b?"v4b":V4?"v4":"v3")+"; all captures 1280x720; orthographic pitch 50 yaw 0; actual chapter skies, lighting, board tiles, gate and three player prefabs; newly authored faceted planet meshes, no map grid.\n");
                Debug.Log(V4?"STAGE_SELECT_V4_PASS":"STAGE_SELECT_V3_PASS");EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);File.WriteAllText(Path.Combine(Output,"failure.txt"),e.ToString());EditorApplication.Exit(1);}
        }
        static void FitV3(Vector3 target,float size)
        {cam.orthographicSize=size;cam.transform.rotation=Quaternion.Euler(50,0,0);cam.transform.position=target-cam.transform.forward*30;var sky=Object.FindAnyObjectByType<RuinsHengeSky>();if(sky)sky.SendMessage("LateUpdate");}
        static Sprite[] BakeBodiesV3()
        {
            var atlas=new Texture2D(2048,1024,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};atlas.SetPixels(new Color[2048*1024]);var result=new Sprite[7];
            for(int p=0;p<7;p++)
            {
                for(int view=0;view<3;view++)
                {
                    Reset(p,false);Object.DestroyImmediate(canvas.gameObject);canvas=null;cam.backgroundColor=Color.clear;FitV3(new Vector3(0,.3f,0),5.4f);
                    var body=PlanetBodyStudy.Build(p,bodyMaterial,world.transform,view*120);cam.Render();var tex=Read();
                    if(view==0)
                    {
                        for(int y=0;y<512;y++)for(int x=0;x<512;x++)atlas.SetPixel((p%4)*512+x,(p/4)*512+y,tex.GetPixelBilinear(.5f+(x/511f-.5f)*.5625f,y/511f));
                        bodyStats.Add(Ids[p]+","+MeshTris(body.GetComponent<MeshFilter>().sharedMesh)+",1");
                    }
                    File.WriteAllBytes(Path.Combine(Output,"body-"+Ids[p]+"-yaw"+(view*120).ToString("000")+".png"),tex.EncodeToPNG());Object.DestroyImmediate(tex);
                }
            }
            atlas.Apply();File.WriteAllBytes(Path.Combine(Output,"unity-body-atlas.png"),atlas.EncodeToPNG());
            for(int p=0;p<7;p++){result[p]=Sprite.Create(atlas,new Rect((p%4)*512,(p/4)*512,512,512),Vector2.one*.5f,100);result[p].hideFlags=HideFlags.HideAndDontSave;}return result;
        }
        static void MapV3(string state,int focus,int cleared)
        {
            Reset(0,true,false);Back();
            foreach(var edge in story.dipper){int a=Array.IndexOf(Ids,edge.from),b=Array.IndexOf(Ids,edge.to);if(a>=0&&b>=0)Trail(A[a],A[b],a<cleared);}
            for(int p=0;p<7;p++)
            {
                Image("Planet body "+Ids[p],A[p],Vector2.one*260,worlds[p],Color.white);
                if(p<cleared)for(int s=0;s<3;s++)Image("Grounded progress star",A[p]+new Vector2((s-1)*19,-91),Vector2.one*15,Ui.StarShape,Gold);
                if(p==focus)
                {Dot(A[p]+new Vector2(0,-92),6,Gold);Text(Name(p),A[p]+(V4?(p==3?new Vector2(110,-130):new Vector2(100,-119)):new Vector2(0,-119)),24,310);Text(Tagline(p),A[p]+(V4?(p==3?new Vector2(110,-159):new Vector2(100,-148)):new Vector2(0,-148)),16,310,new Color(.78f,.84f,.85f));}
            }
            if(cleared==7){Trail(A[6],new Vector2(1240,65),true);Ring(new Vector2(1230,65),26,Gold);}
            Save("map-"+language+"-"+state);
        }
        static Vector3 Place(int s,int count)
        {
            float u=s/(float)(count-1);float x=-2.7f+u*5.4f;float z=-1.9f+Mathf.Sin(u*Mathf.PI*2)*.55f;
            return new Vector3(x,Mathf.Sqrt(Mathf.Max(.1f,3.65f*3.65f-x*x-z*z))*.88f+.10f,z);
        }
        static GameObject SurfacePlaces(int p,Transform parent,int cleared,int focus)
        {
            var collider=parent.gameObject.AddComponent<MeshCollider>();var full=parent.GetComponent<MeshFilter>().sharedMesh;
            // The first 1024 triangles are the celestial crust; exclude menhirs from the ground projection.
            var ground=new Mesh();ground.vertices=full.vertices;ground.triangles=full.triangles.Take((V4?5120:1024)*3).ToArray();ground.RecalculateBounds();collider.sharedMesh=ground;
            Vector3 Ground(Vector3 point)
            {var direction=point.normalized;var ray=new Ray(parent.TransformPoint(direction*8),-parent.TransformDirection(direction));if(collider.Raycast(ray,out var hit,20))return parent.InverseTransformPoint(hit.point)+parent.InverseTransformDirection(hit.normal)*.07f;return point;}
            int count=planets[p].Stages.Count;var mesh=new PlanetBodyStudy{Project=Ground};
            for(int s=0;s<count;s++)
            {
                var pos=Ground(Place(s,count));var n=new Vector3(pos.x,pos.y/.7744f,pos.z).normalized;
                mesh.Lift=0;
                mesh.Disc(pos,n,.34f,s<cleared?new Color(.50f,.60f,.44f):new Color(.22f,.27f,.29f));
                if(s<cleared){mesh.Lift=.028f;mesh.Star(pos+n*.018f,n,.27f,Gold);}
                if(s==cleared)
                {mesh.Lift=.018f;mesh.Disc(pos+n*.012f,n,.42f,new Color(.43f,.48f,.34f));mesh.Lift=.05f;for(int dot=0;dot<3;dot++)mesh.Disc(pos+n*.04f+Vector3.right*(dot-1)*.16f,n,.065f,dot==0?Gold:dot==1?new Color(.6f,.82f,.85f):new Color(.85f,.59f,.46f),10);}
                mesh.Lift=.06f;
                if(s==focus)for(int dot=0;dot<16;dot++){float a=dot*Mathf.PI/8;var r=Quaternion.FromToRotation(Vector3.up,n);mesh.Disc(pos+n*.025f+r*new Vector3(Mathf.Cos(a)*.48f,0,Mathf.Sin(a)*.48f),n,.025f,Gold,6);}
                mesh.Lift=.015f;
                if(s>0)for(int k=1;k<8;k++)
                {var a=Place(s-1,count);var path=Ground(Vector3.Lerp(a,pos,k/8f));var normal=new Vector3(path.x,path.y/.7744f,path.z).normalized;mesh.Disc(path+new Vector3(0,0,(k%2==0?1:-1)*.045f),normal,.027f,s<=cleared?Gold:new Color(.47f,.51f,.45f),6);}
            }
            return mesh.Draw("Places and footsteps in planet ground",lightMaterial,parent);
        }
        static void SurfaceV3(int p,string state,int focus,int cleared)
        {
            Reset(p,true,false);Back();FitV3(new Vector3(0,.5f,0),5.5f);var body=PlanetBodyStudy.Build(p,bodyMaterial,world.transform,-8,true);SurfacePlaces(p,body.transform,cleared,focus);
            Text(Name(p),new Vector2(640,665),26);if(focus>=0)Text("걸음 "+(focus+1).ToString("00"),new Vector2(640,58),23);
            // A place is attached to the body; UI hit areas are invisible and follow its projected position.
            for(int s=0;s<planets[p].Stages.Count;s++){var sp=(Vector2)cam.WorldToScreenPoint(body.transform.TransformPoint(Place(s,planets[p].Stages.Count)));var hit=Rect("Surface place target "+s,sp,Vector2.one*64).gameObject.AddComponent<Image>();hit.color=Color.clear;hit.gameObject.AddComponent<Button>();}
            Save("surface-"+Ids[p]+"-"+state);
        }
        static GameObject BoardV3(Transform parent,Vector3 pos,float rise,int p=0,int levelIndex=0,bool openExit=false,bool openEntry=false)
        {
            var root=new GameObject("Actual level "+(levelIndex+1)+" rises from planet ground");root.transform.SetParent(parent,false);root.transform.localPosition=pos+Vector3.down*(1-rise)*1.7f;root.transform.localScale=Vector3.one*(V4?.22f:.30f);
            var level=levels[levelIndex];var set=chapters[p].tileSet;
            void Spawn(TileVisual visual,Vector3 at)
            {if(!visual.prefab)return;var tile=Object.Instantiate(visual.prefab,root.transform);tile.transform.localPosition=at+visual.offset;tile.transform.localScale=visual.Scale;tile.transform.localRotation=visual.RotationFor(0);ChapterMaterials.Apply(tile,chapters[p]);}
            for(int i=0;i<level.Count;i++)
            {char cell=level.Tile(i);var at=new Vector3(level.X(i)-(level.W-1)*.5f,0,(level.H-1)*.5f-level.Y(i));Spawn(cell=='o'?set.pit:cell=='~'?set.ice:set.floor,at);bool threshold=level.Y(i)==level.H/2&&((openExit&&level.X(i)==level.W-1)||(openEntry&&level.X(i)==0));if(cell=='#'&&!threshold)Spawn(set.wall,at);if(cell=='E')Spawn(set.exit,at);}
            return root;
        }
        static void Brothers(Transform parent,float x)
        {for(int k=0;k<3;k++){var v=chapters[0].tileSet.PlayerFor(k);if(!v.prefab)continue;var go=Object.Instantiate(v.prefab,parent);go.transform.localPosition=new Vector3(x-k*.45f,0,.3f)+v.offset;go.transform.localScale=v.Scale;ChapterMaterials.Apply(go,chapters[0]);}}
        static float Ease(float t)=>t*t*(3-2*t);
        static void CaptionTime(int i)=>Text((i*.25f).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" s",new Vector2(1208,35),16,110,new Color(.7f,.78f,.8f));
        static GameObject[] SpaceBodies()
        {
            FitV3(new Vector3(0,.3f,0),13);var list=new GameObject[7];
            for(int p=0;p<7;p++){list[p]=PlanetBodyStudy.Build(p,bodyMaterial,world.transform,0,p==0);list[p].transform.position=cam.ScreenToWorldPoint(new Vector3(A[p].x,A[p].y,30));list[p].transform.localScale=Vector3.one*.90f;}return list;
        }
        static void DiveV3(int frame)
        {
            if(frame==0){MapV3("dive-start",0,0);File.Copy(Path.Combine(Output,"map-ko-dive-start.png"),Path.Combine(Output,"dive-00.png"),true);return;}
            Reset(0,true,false);Back();float t=Ease(frame/6f);var bodies=SpaceBodies();var body=bodies[0];var startTarget=new Vector3(0,.3f,0);var endTarget=body.transform.TransformPoint(new Vector3(0,Seat,0));
            FitV3(Vector3.Lerp(startTarget,endTarget,t),Mathf.Lerp(13,1.8f,t));var board=BoardV3(body.transform,new Vector3(0,Seat,0),Mathf.Clamp01((t-.35f)/.65f));if(t>.65f)Brothers(board.transform,-3.5f);CaptionTime(frame);Save("dive-"+frame.ToString("00"));
        }
        static void AdvanceV3(int frame)
        {
            Reset(0,true,false);Back();float t=Ease(frame/6f);var body=PlanetBodyStudy.Build(0,bodyMaterial,world.transform,0,true);
            var current=BoardV3(body.transform,new Vector3(V4?-1.35f:-1.9f,Seat,0),1,0,0,true);var next=BoardV3(body.transform,new Vector3(V4?1.35f:1.9f,Seat,0),Mathf.Clamp01(t*1.5f),0,1,false,frame<6);
            var bridge=new PlanetBodyStudy();bridge.Box(new Vector3(V4?.15f:.35f,V4?2.25f:3.3f,0),new Vector3(V4?1.4f:2,.12f,.48f),new Color(.48f,.54f,.44f),Quaternion.identity);bridge.Draw("Continuous surface threshold",bodyMaterial,body.transform);
            FitV3(new Vector3(Mathf.Lerp(V4?-1.35f:-1.9f,V4?1.35f:1.9f,t),Seat,0),2.7f);
            // Exit is on +X: the brothers and camera move right, towards the next board rising on +X.
            var party=new GameObject("Continuous exit walk");party.transform.SetParent(body.transform,false);party.transform.localPosition=new Vector3(Mathf.Lerp(V4?-.60f:-.9f,V4?.91f:1.3f,t),V4?2.36f:3.43f,0);party.transform.localScale=Vector3.one*(V4?.22f:.30f);Brothers(party.transform,0);
            CaptionTime(frame);Save("advance-"+frame.ToString("00"));
        }
        static void CompleteV3(int frame)
        {
            if(frame==6){MapV3("complete-next-focus",1,1);File.Copy(Path.Combine(Output,"map-ko-complete-next-focus.png"),Path.Combine(Output,"complete-06.png"),true);return;}
            Reset(0,true,false);Back();float t=Ease(frame/6f);var bodies=SpaceBodies();var body=bodies[0];var start=body.transform.TransformPoint(new Vector3(0,Seat,0));var board=BoardV3(body.transform,new Vector3(0,Seat,0),1);Brothers(board.transform,1.4f);
            FitV3(Vector3.Lerp(start,new Vector3(0,.3f,0),t),Mathf.Lerp(1.8f,13,t));
            if(frame>=4){Trail(A[0],A[1],true);Dot(A[1]+new Vector2(0,-92),6,Gold);}CaptionTime(frame);Save("complete-"+frame.ToString("00"));
        }
    }
}
