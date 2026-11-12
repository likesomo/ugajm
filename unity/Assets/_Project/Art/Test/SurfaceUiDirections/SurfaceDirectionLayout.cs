using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using SevenSteps.Game.UI.Planets;

namespace SevenSteps.Art.SurfaceUiReview
{
    public sealed class SurfaceDirectionLayout
    {
        public GameObject Markers {get;}
        public Canvas Overlay {get;}
        public readonly List<Rect> Targets=new List<Rect>();
        public readonly List<Vector2> Centres=new List<Vector2>();
        public readonly List<string> Issues=new List<string>();
        readonly Camera cam;readonly PlanetBody body;readonly GameAssets assets;readonly int direction,count;readonly bool en;
        readonly Font font;readonly Material stone,inkMat;readonly Transform canvas;
        readonly Color cream=new Color32(238,231,208,255),gold=new Color32(233,188,101,255),dim=new Color32(122,144,142,255);
        readonly Color[] scarves={new Color32(86,140,136,255),new Color32(185,154,87,255),new Color32(185,108,89,255)};
        readonly Vector2[] crust={new Vector2(-245,55),new Vector2(-165,-90),new Vector2(-70,-140),new Vector2(35,-75),new Vector2(140,-15),new Vector2(225,80),new Vector2(125,185),new Vector2(-5,200)};
        readonly Sprite empty,filled;
        public SurfaceDirectionLayout(Camera camera,PlanetBody planet,GameAssets gameAssets,int mode,int places,bool english,string focusTitle)
        {
            cam=camera;body=planet;assets=gameAssets;direction=mode;count=places;en=english;font=assets.uiTheme.FontFor(en?Language.En:Language.Ko,FontRole.Body);
            empty=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Shared/Art/UI/U5/4x/step-star-empty.png");filled=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Shared/Art/UI/U5/4x/step-star.png");
            Markers=new GameObject("Direction "+mode+" world marks");Markers.transform.SetParent(body.transform,false);
            var go=new GameObject("Direction overlay",typeof(RectTransform),typeof(Canvas));Overlay=go.GetComponent<Canvas>();Overlay.renderMode=RenderMode.WorldSpace;Overlay.worldCamera=cam;Overlay.sortingOrder=200;canvas=go.transform;PlaceCanvas(Overlay,cam,.8f);
            stone=Mat(new Color(.30f,.35f,.32f));inkMat=Mat(cream);
            for(int i=0;i<count;i++)BuildPlace(i);
            for(int i=1;i<count;i++)Connect(i-1,i);
            Nav("back",52,52);Nav("settings",1228,52);
            FocusLine(focusTitle);
            Canvas.ForceUpdateCanvases();
            for(int i=0;i<Targets.Count;i++)for(int j=i+1;j<Targets.Count;j++)if(Targets[i].Overlaps(Targets[j]))Issues.Add("Overlapping 80px targets "+i+" / "+j);
        }
        public static void PlaceCanvas(Canvas c,Camera camera,float distance)
        {
            var r=(RectTransform)c.transform;r.sizeDelta=new Vector2(1280,720);r.pivot=Vector2.one*.5f;r.SetPositionAndRotation(camera.transform.position+camera.transform.forward*distance,camera.transform.rotation);r.localScale=Vector3.one*(2*camera.orthographicSize/720);
        }
        readonly Dictionary<Color,Material> materials=new Dictionary<Color,Material>();
        Material Mat(Color color){if(materials.TryGetValue(color,out var existing))return existing;var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.SetColor("_BaseColor",color);m.color=color;materials.Add(color,m);return m;}
        Vector3 World(Vector2 p,float depth=25)=>cam.ViewportToWorldPoint(new Vector3(p.x/1280,1-p.y/720,depth));
        Vector2 Point(Vector3 p){var v=cam.WorldToViewportPoint(p);return new Vector2(v.x*1280,(1-v.y)*720);}
        RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x-w/2,-y+h/2);r.sizeDelta=new Vector2(w,h);return r;
        }
        Image Image(Transform parent,string name,Sprite sprite,float x,float y,float w,float h,Color color)
        {
            var r=Rect(parent,name,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=color;image.raycastTarget=false;return image;
        }
        void Stars(Transform parent,int earned,float y,float size=13)
        {
            for(int k=0;k<3;k++)Image(parent,"Best star "+k,k<earned?filled:empty,40+(k-1)*(size+2),y,size,size,k<earned?gold:cream);
        }
        void Scarf(Transform parent,float y)
        {
            for(int k=0;k<3;k++)Image(parent,"Scarf "+k,Ui.Circle,40+(k-1)*12,y,7,7,scarves[k]);
            Image(parent,"Current underline",null,40,y+10,30,2,cream);
        }
        void BuildPlace(int index)
        {
            bool done=index<4,current=index==4,focus=index==2;int earned=done?index:0;
            Vector2 p;
            if(direction==2)
            {
                Vector2[] points={new Vector2(300,430),new Vector2(330,260),new Vector2(445,135),new Vector2(640,98),new Vector2(835,135),new Vector2(950,260),new Vector2(980,430),new Vector2(640,604)};p=points[index];
            }
            else if(direction==4)
            {
                float u=index/(float)(count-1);p=new Vector2(Mathf.Lerp(180,1100,u),536+Mathf.Sin(u*Mathf.PI)*66);
            }
            else p=new Vector2(640,350)+crust[index];
            Vector3 anchor;
            if(direction==1||direction==3)
            {
                var ray=cam.ViewportPointToRay(new Vector3(p.x/1280,1-p.y/720));var collider=body.GetComponent<MeshCollider>();
                if(collider.Raycast(ray,out var hit,100)){anchor=hit.point+(-cam.transform.forward)*.9f;}
                else{anchor=World(p,28);Issues.Add("Projected crust miss at "+index);}
            }
            else anchor=World(p,25);
            Centres.Add(p);Targets.Add(new Rect(p-Vector2.one*40,Vector2.one*80));
            var node=new GameObject("Stage "+(index+1)+" "+(current?"current":done?"cleared "+earned:"untouched"));node.transform.SetParent(Markers.transform,false);node.transform.SetPositionAndRotation(anchor,cam.transform.rotation);
            float unit=2*cam.orthographicSize/720;
            var glyphGo=new GameObject("Stage glyph",typeof(RectTransform),typeof(Canvas));glyphGo.transform.SetParent(node.transform,false);var gc=glyphGo.GetComponent<Canvas>();gc.renderMode=RenderMode.WorldSpace;gc.worldCamera=cam;gc.sortingOrder=30;
            var gr=(RectTransform)gc.transform;gr.sizeDelta=new Vector2(80,80);gr.localScale=Vector3.one*unit;gr.localPosition=Vector3.zero;
            var hitRoot=Rect(canvas,"80px stage target "+(index+1),p.x,p.y,80,80);var hitImage=hitRoot.gameObject.AddComponent<Image>();hitImage.color=Color.clear;var button=hitRoot.gameObject.AddComponent<Button>();button.transition=Selectable.Transition.None;
            if(direction==1)
            {
                Slab(node.transform,done?new Color(.40f,.43f,.36f):current?new Color(.35f,.40f,.36f):new Color(.17f,.22f,.23f),50*unit,62*unit);
                GlyphLine(gr,27,24,27,39,2,current?gold:done?cream:dim);GlyphLine(gr,27,24,53,24,2,current?gold:done?cream:dim);GlyphLine(gr,53,24,53,39,2,current?gold:done?cream:dim);
                if(done)Stars(gr,earned,58);else if(current)Scarf(gr,50);else Image(gr,"Untouched entry",null,40,48,12,2,dim);
            }
            else if(direction==2)
            {
                if(done){Image(gr,"Cleared light",Ui.Circle,40,29,9,9,gold);Stars(gr,earned,52,14);}
                else if(current){Scarf(gr,36);Ring(gr,40,35,20,1.6f,gold);}
                else Image(gr,"Untouched star",empty,40,36,25,25,cream);
            }
            else if(direction==3)
            {
                Foot(gr,32,29,done||current,current?scarves[0]:done?cream:dim);Foot(gr,47,36,done||current,current?scarves[1]:done?cream:dim);
                if(done)Stars(gr,earned,61);else if(current){Image(gr,"Third scarf foot",Ui.Circle,43,58,6,6,scarves[2]);Image(gr,"Current underline",null,40,66,30,2,cream);}
            }
            else
            {
                float w=current?30:25,h=current?35:28;Color color=current?gold:done?cream:dim;
                // A hollow architectural threshold: painting/sky remains visible through the centre.
                Segment(node.transform,new Vector2(-w,-h),new Vector2(-w,h*.55f),3*unit,color,unit);
                Segment(node.transform,new Vector2(-w,h*.55f),new Vector2(-w*.55f,h),3*unit,color,unit);
                Segment(node.transform,new Vector2(-w*.55f,h),new Vector2(w*.55f,h),3*unit,color,unit);
                Segment(node.transform,new Vector2(w*.55f,h),new Vector2(w,h*.55f),3*unit,color,unit);
                Segment(node.transform,new Vector2(w,h*.55f),new Vector2(w,-h),3*unit,color,unit);
                if(done)Stars(gr,earned,57,14);else if(current)Scarf(gr,50);else GlyphLine(gr,34,45,46,45,2,dim);
            }
            if(focus)
            {
                Color c=cream;GlyphLine(gr,3,14,3,3,1.6f,c);GlyphLine(gr,3,3,14,3,1.6f,c);
                GlyphLine(gr,66,77,77,77,1.6f,c);GlyphLine(gr,77,77,77,66,1.6f,c);
            }
        }
        void Slab(Transform parent,Color color,float width,float height)
        {
            var v=new List<Vector3>();var t=new List<int>();Vector2[] p={new Vector2(-.42f,-.5f),new Vector2(.37f,-.5f),new Vector2(.5f,-.32f),new Vector2(.48f,.33f),new Vector2(.32f,.5f),new Vector2(-.35f,.48f),new Vector2(-.5f,.29f),new Vector2(-.49f,-.32f)};
            for(int s=0;s<2;s++)for(int i=0;i<8;i++)v.Add(new Vector3(p[i].x*width,p[i].y*height,s==0?.012f:.09f));
            for(int i=1;i<7;i++){t.Add(0);t.Add(i+1);t.Add(i);}for(int i=0;i<8;i++){int j=(i+1)%8;t.Add(i);t.Add(j);t.Add(j+8);t.Add(i);t.Add(j+8);t.Add(i+8);}
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();var go=new GameObject("Original carved waystone",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=Mat(color);
        }
        void Segment(Transform parent,Vector2 a,Vector2 b,float width,Color color,float unit)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Original portal lintel";go.transform.SetParent(parent,false);
            var d=b-a;go.transform.localPosition=new Vector3((a.x+b.x)*unit/2,(a.y+b.y)*unit/2,.035f);go.transform.localScale=new Vector3(d.magnitude*unit,width,.06f);go.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);go.GetComponent<Renderer>().sharedMaterial=Mat(color);UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        void GlyphLine(Transform parent,float x,float y,float xx,float yy,float width,Color color)
        {
            var d=new Vector2(xx-x,yy-y);var rt=Rect(parent,"Engraved stroke",(x+xx)/2,(y+yy)/2,d.magnitude,width);rt.pivot=Vector2.one*.5f;rt.anchoredPosition=new Vector2((x+xx)/2,-(y+yy)/2);rt.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);var im=rt.gameObject.AddComponent<Image>();im.color=color;im.raycastTarget=false;
        }
        void Ring(Transform parent,float x,float y,float radius,float width,Color color)
        {
            for(int i=0;i<36;i++){float a=i*Mathf.PI/18,b=(i+1)*Mathf.PI/18;GlyphLine(parent,x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius,width,color);}
        }
        void Foot(Transform parent,float x,float y,bool fill,Color color)
        {
            if(fill){var im=Image(parent,"Brother footprint",Ui.Circle,x,y,10,18,color);im.rectTransform.localRotation=Quaternion.Euler(0,0,-16);Image(parent,"Toe",Ui.Circle,x-2,y-13,7,7,color);}
            else{Ring(parent,x,y,6,1.3f,color);Image(parent,"Unvisited toe",Ui.Circle,x-2,y-12,4,4,color);}
        }
        void Connect(int a,int b)
        {
            var pa=Centres[a];var pb=Centres[b];Color c=a<4?new Color(.83f,.72f,.48f,.65f):new Color(.54f,.65f,.64f,.45f);
            if(direction==1||direction==3)
            {
                int n=7;for(int i=1;i<n;i++)
                {
                    var p=Vector2.Lerp(pa,pb,i/(float)n);var ray=cam.ViewportPointToRay(new Vector3(p.x/1280,1-p.y/720));
                    var collider=body.GetComponent<MeshCollider>();Vector3 anchor=collider.Raycast(ray,out var hit,100)?hit.point-cam.transform.forward*.12f:World(p,28);
                    var g=new GameObject("Faint footprint trail",typeof(RectTransform),typeof(Canvas));g.transform.SetParent(Markers.transform,false);var cv=g.GetComponent<Canvas>();cv.renderMode=RenderMode.WorldSpace;cv.worldCamera=cam;
                    var rt=(RectTransform)g.transform;rt.sizeDelta=new Vector2(8,8);rt.SetPositionAndRotation(anchor,cam.transform.rotation);rt.localScale=Vector3.one*(2*cam.orthographicSize/720);
                    Image(g.transform,"Trail step",Ui.Circle,4,4,2.3f,3.5f,c);
                }
            }
            else
            {
                var go=new GameObject("Constellation or orbit link",typeof(LineRenderer));go.transform.SetParent(Markers.transform,false);var line=go.GetComponent<LineRenderer>();line.useWorldSpace=false;
                line.sharedMaterial=Mat(c);line.startWidth=line.endWidth=2*cam.orthographicSize/720*.9f;line.positionCount=2;
                var aa=Vector2.Lerp(pa,pb,.20f);var bb=Vector2.Lerp(pa,pb,.80f);line.SetPosition(0,Markers.transform.InverseTransformPoint(World(aa,25.1f)));line.SetPosition(1,Markers.transform.InverseTransformPoint(World(bb,25.1f)));
            }
        }
        void Nav(string id,float x,float y)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Shared/Art/UI/U5/4x/"+id+".png");Image(canvas,id,sprite,x,y,48,48,cream);
            var hit=Rect(canvas,id+" 80px target",x,y,80,80);hit.gameObject.AddComponent<Image>().color=Color.clear;hit.gameObject.AddComponent<Button>();Targets.Add(new Rect(x-40,y-40,80,80));
        }
        void FocusLine(string title)
        {
            string value="03 · "+title;
            var rt=Rect(canvas,"One focus line",600,670,550,28);var text=rt.gameObject.AddComponent<Text>();text.font=font;text.fontSize=18;text.text=value;text.color=cream;text.alignment=TextAnchor.MiddleLeft;text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;
            float width=text.preferredWidth+32+64;if(width>630)Issues.Add("Focus line overflow "+width);width=Mathf.Clamp(width,280,630);
            var plate=Image(canvas,"Single focus plate",null,640,670,width,42,new Color(.035f,.075f,.095f,.9f));plate.transform.SetAsFirstSibling();
            rt.anchoredPosition=new Vector2(640-width/2+16,-656);rt.sizeDelta=new Vector2(width-96,28);
            for(int i=0;i<3;i++)Image(canvas,"Focused best star",i<2?filled:empty,640+width/2-58+i*18,670,16,16,i<2?gold:cream);
        }
        public void Dispose(){UnityEngine.Object.DestroyImmediate(Markers);UnityEngine.Object.DestroyImmediate(Overlay.gameObject);foreach(var m in materials.Values)UnityEngine.Object.DestroyImmediate(m);}
    }
}
