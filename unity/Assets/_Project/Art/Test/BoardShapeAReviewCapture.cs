using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using SevenSteps.Contracts;
using SevenSteps.Game;
using UnityEngine;
using UnityEngine.UI;

namespace SevenSteps.Art
{
    // Isolated art review player. Saved production assets, real BoardView camera fitting.
    // It never changes AppConfig, production scenes, stage data or the default CameraRig.
    public sealed class BoardShapeAReviewCapture : MonoBehaviour
    {
        public ChapterPreset[] chapters;
        public Font numberFont;
        readonly List<GameObject> labels=new List<GameObject>();
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int k=Array.IndexOf(args,"-boardshape-capture");
            if(k<0||k+1>=args.Length)yield break;
            string output=args[k+1];Directory.CreateDirectory(output);var camera=Camera.main;
            var csv=new List<string>{"id,w,h,proposedFraming,tris,renderers,materials,cellWidthPx,cellHeightPx"};
            foreach(var chapter in chapters)
            {
                foreach(var size in new[]{new Vector2Int(8,6),new Vector2Int(12,5),new Vector2Int(10,3),new Vector2Int(7,7)})
                {
                    if(chapter.id!="moss"&&size!=new Vector2Int(8,6))continue;
                    foreach(bool proposed in new[]{false,true})
                    {
                        if(!proposed&&(chapter.id!="moss"||size!=new Vector2Int(8,6)))continue;
                        var root=new GameObject("Review_"+chapter.id);var grid=new BoardGrid(size.x,size.y);
                        var planet=Instantiate(chapter.sky.planetPrefab,root.transform).GetComponent<RuinsHengePlanet>();planet.Fit(grid);
                        var set=chapter.tileSet;int pitX=2,iceX=3,mudX=Mathf.Min(6,size.x-2),specialY=size.y/2;
                        void Spawn(TileVisual visual,int x,int y)
                        {
                            if(!visual.prefab)throw new Exception("Missing review tile");
                            var go=Instantiate(visual.prefab,root.transform);go.transform.position=grid.CellToWorld(x,y)+visual.offset;
                            go.transform.localScale=visual.Scale;go.transform.rotation=visual.RotationFor(x+y*size.x);ChapterMaterials.Apply(go,chapter);
                        }
                        for(int y=0;y<size.y;y++)for(int x=0;x<size.x;x++)
                        {
                            if(y==specialY&&x==pitX)Spawn(set.pit,x,y);
                            else if(y==specialY&&x==iceX)Spawn(set.ice,x,y);
                            else if(y==specialY+1&&x==mudX)Spawn(set.mud,x,y);
                            else Spawn((x+y)%2==0?set.floor:set.floorAlt,x,y);
                        }
                        Spawn(set.gate,2,Mathf.Max(0,specialY-1));Spawn(set.wall,0,0);
                        for(int body=0;body<3;body++)Spawn(set.PlayerFor(body),Mathf.Min(body+2,size.x-1),Mathf.Min(specialY+1,size.y-1));
                        foreach(var rig in new[]{chapter.lighting.key,chapter.lighting.fill,chapter.lighting.rim})
                        {
                            var light=new GameObject("PresetLight",typeof(Light)).GetComponent<Light>();light.transform.SetParent(root.transform);light.type=LightType.Directional;
                            light.enabled=rig.enabled;light.color=rig.color;light.intensity=rig.intensity;light.transform.rotation=Quaternion.Euler(rig.euler);light.shadows=LightShadows.None;
                        }
                        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=chapter.lighting.ambient;
                        camera.backgroundColor=chapter.sky.background;camera.allowHDR=false;
                        var sky=Instantiate(chapter.sky.skyPrefab,camera.transform);
                        var cameraRig=set.cameraRig;BoardView.Fit(camera,grid,cameraRig);
                        if(proposed)
                        {
                            // Review-only framing proposal: use 75% of width, retain full 3m crust.
                            camera.orthographic=true;
                            camera.orthographicSize=Mathf.Max(4.65f,(size.x+3.86f)/(2*camera.aspect*.75f),size.y*.383f+2.18f);
                            camera.transform.position=new Vector3(0,-.75f,0)-camera.transform.forward*cameraRig.distance;
                        }
                        var canvas=new GameObject("ReviewLabels",typeof(Canvas),typeof(CanvasScaler));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                        Text Text(string value,Vector2 pos,int fontSize)
                        {
                            var go=new GameObject(value,typeof(RectTransform),typeof(Text));go.transform.SetParent(canvas.transform,false);var text=go.GetComponent<Text>();text.font=numberFont;text.text=value;text.fontSize=fontSize;text.color=new Color(1,.91f,.71f);text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
                            var rt=text.rectTransform;rt.anchorMin=rt.anchorMax=Vector2.zero;rt.pivot=Vector2.one*.5f;rt.sizeDelta=new Vector2(320,75);rt.anchoredPosition=pos;return text;
                        }
                        Text(chapter.id+"  "+size.x+" x "+size.y,new Vector2(180,665),28);
                        Text(proposed?"Proposed framing / pitch 50":"Default framing / pitch 50",new Vector2(190,615),20);
                        Text("Wall / Pit / Ice / Door / Mud",new Vector2(185,proposed?30:100),17);
                        var number=Text("3",Vector2.zero,proposed?32:26);number.color=new Color(1,.94f,.78f);
                        yield return null;yield return new WaitForSeconds(.4f);
                        var sp=camera.WorldToScreenPoint(grid.CellToWorld(2,Mathf.Max(0,specialY-1))+set.gate.labelOffset);number.rectTransform.anchoredPosition=sp;
                        var a=camera.WorldToScreenPoint(grid.CellToWorld(1,1));var bx=camera.WorldToScreenPoint(grid.CellToWorld(2,1));var by=camera.WorldToScreenPoint(grid.CellToWorld(1,0));
                        var filters=root.GetComponentsInChildren<MeshFilter>();long tris=0;foreach(var mf in filters)for(int sm=0;sm<mf.sharedMesh.subMeshCount;sm++)tris+=mf.sharedMesh.GetIndexCount(sm)/3;
                        var materials=new HashSet<Material>();foreach(var r in root.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)materials.Add(m);
                        csv.Add(chapter.id+","+size.x+","+size.y+","+proposed+","+tris+","+root.GetComponentsInChildren<Renderer>().Length+","+materials.Count+","+Mathf.Abs(bx.x-a.x).ToString("F2")+","+Mathf.Abs(by.y-a.y).ToString("F2"));
                        yield return new WaitForEndOfFrame();
                        string name=chapter.id+"-"+size.x+"x"+size.y+(proposed?"-proposed":"-ortho")+".png";
                        string file=Path.Combine(output,name);ScreenCapture.CaptureScreenshot(file);yield return new WaitForSeconds(.3f);
                        if(!File.Exists(file))throw new IOException("Unity screenshot was not saved: "+file);
                        Destroy(canvas);Destroy(sky);Destroy(root);yield return null;
                    }
                }
            }
            File.WriteAllLines(Path.Combine(output,"unity-review-geometry.csv"),csv);Debug.Log("[BoardShapeAReview] PASS: actual Unity captures complete");Application.Quit();
        }
    }
}
