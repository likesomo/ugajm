using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.UI;

namespace SevenSteps.Art.SurfaceUiReview
{
    // Original review-only vector glyphs. No shared imports or production UI are changed.
    public sealed class ConstellationLayout : IDisposable
    {
        public readonly GameObject Markers;
        public readonly Canvas Overlay;
        public readonly List<Rect> Targets=new List<Rect>();
        public readonly List<string> Issues=new List<string>();
        public readonly List<Vector2> Centres=new List<Vector2>();
        public readonly int[] Scores;
        public readonly int Cleared,Current,Focus;
        readonly Camera cam;readonly Transform canvas;readonly int variant,count;readonly string planet,state,phase;
        readonly Color cream=new Color32(238,231,208,255),gold=new Color32(237,194,115,255),dim=new Color32(137,159,169,255),ink=new Color32(19,26,36,255);
        readonly Color[] scarves={new Color32(86,140,136,255),new Color32(185,154,87,255),new Color32(185,108,89,255)};
        readonly List<Sprite> sprites=new List<Sprite>();readonly List<Texture2D> textures=new List<Texture2D>();
        readonly Sprite halo,star,diamond;readonly bool finale;
        public ConstellationLayout(Camera camera,GameAssets assets,string planetId,int mode,int stageCount,string progress,bool english,string title,string visualPhase="focused")
        {
            cam=camera;variant=mode;count=stageCount;planet=planetId;state=progress;phase=visualPhase;finale=state=="perfect";
            Cleared=state=="early"?1:state=="mid"?4:count;Current=Cleared<count?Cleared:-1;
            Focus=phase=="focused"?2:Mathf.Max(Current,0);
            Scores=new int[count];for(int i=0;i<count;i++)Scores[i]=i<Cleared?(finale?3:state=="early"?1:i%4):-1;
            Markers=new GameObject("D2"+(char)('a'+mode)+" "+planet+" "+state);
            var go=new GameObject("Constellation overlay",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));Overlay=go.GetComponent<Canvas>();Overlay.renderMode=RenderMode.WorldSpace;Overlay.worldCamera=cam;Overlay.sortingOrder=200;canvas=go.transform;SurfaceDirectionLayout.PlaceCanvas(Overlay,cam,.8f);
            halo=Raster("Soft celestial halo",(x,y)=>{float r=Mathf.Sqrt(x*x+y*y);return Mathf.Pow(Mathf.Clamp01(1-r),2.2f);});
            star=Raster("Four ray node",(x,y)=>{float r=Mathf.Abs(x)*Mathf.Abs(y);float bound=Mathf.Pow(Mathf.Abs(x),.6f)+Mathf.Pow(Mathf.Abs(y),.6f);return Mathf.Clamp01((1.03f-bound)*30);});
            diamond=Raster("Carved diamond",(x,y)=>Mathf.Clamp01((1-Mathf.Abs(x)-Mathf.Abs(y))*25));
            foreach(var p in Figure(planet,count))Centres.Add(p);
            for(int i=1;i<count;i++)Link(Centres[i-1],Centres[i],i<=Cleared);
            if(planet=="moss")Link(Centres[6],Centres[3],Cleared>=count);
            if(planet=="altar")Link(Centres[count-1],Centres[0],Cleared>=count);
            for(int i=0;i<count;i++)Node(i);
            Nav("back",52,52);Nav("settings",1228,52);
            string value=(Focus+1).ToString("00")+" · "+title+" · "+(english?"Best ":"최고 ")+Mathf.Max(0,Scores[Focus]);
            var font=assets.uiTheme.FontFor(english?Language.En:Language.Ko,FontRole.Body);
            var label=Rect(canvas,"Focused stage only",640,670,600,30).gameObject.AddComponent<Text>();label.font=font;label.fontSize=18;label.text=value;label.color=cream;label.alignment=TextAnchor.MiddleCenter;label.horizontalOverflow=HorizontalWrapMode.Overflow;
            float width=label.preferredWidth+36;if(width>720)Issues.Add("Caption exceeds 720px");var plate=Image(canvas,"Focus plate",null,640,670,Mathf.Clamp(width,300,720),42,new Color(.035f,.075f,.095f,.92f));plate.transform.SetAsFirstSibling();
            for(int i=0;i<Targets.Count;i++)for(int j=i+1;j<Targets.Count;j++)if(Targets[i].Overlaps(Targets[j]))Issues.Add("Overlap "+i+"/"+j);
            foreach(var r in Targets)if(r.x<0||r.y<0||r.xMax>1280||r.yMax>720)Issues.Add("Target outside screen");
        }
        static Vector2[] Figure(string p,int n)
        {
            // Sky figures echo the landmark; node positions remain outside the enlarged painting.
            Vector2[] v;
            switch(p)
            {
                case "moss":v=new[]{P(200,475),P(250,280),P(410,115),P(880,100),P(1090,155),P(1120,345),P(935,270)};break; // dipper: handle and closed four-light bowl
                case "frost":v=new[]{P(220,440),P(245,155),P(420,64),P(640,112),P(860,64),P(1035,155),P(1060,440)};break; // cleft crown
                case "altar":v=new[]{P(260,500),P(245,260),P(360,125),P(530,65),P(750,65),P(920,125),P(1035,260),P(1020,500)};break; // henge hoop
                case "joinery":v=new[]{P(230,470),P(230,210),P(440,100),P(640,65),P(840,100),P(1050,210),P(1050,470)};break; // dovetail roof
                case "hollow":v=new[]{P(225,470),P(210,230),P(380,100),P(640,65),P(900,100),P(1070,230),P(1055,470)};break;
                case "carved":v=new[]{P(210,470),P(280,220),P(360,95),P(640,95),P(920,95),P(1000,220),P(1070,470)};break;
                default:v=new[]{P(230,500),P(190,270),P(390,90),P(640,65),P(890,90),P(1090,270),P(1050,500)};break;
            }
            if(n>v.Length)throw new Exception("Missing constellation nodes for "+p);if(n==v.Length)return v;var result=new Vector2[n];Array.Copy(v,result,n);return result;
        }
        static Vector2 P(float x,float y)=>new Vector2(x,y);
        Sprite Raster(string name,Func<float,float,float> alpha)
        {
            int size=128;var t=new Texture2D(size,size,TextureFormat.RGBA32,false);t.name=name;t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)pixels[y*size+x]=new Color(1,1,1,alpha((x+.5f-size/2)/(size/2f),(y+.5f-size/2)/(size/2f)));
            t.SetPixels(pixels);t.Apply();textures.Add(t);var s=Sprite.Create(t,new Rect(0,0,size,size),Vector2.one*.5f,128);sprites.Add(s);return s;
        }
        RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var g=new GameObject(name,typeof(RectTransform));g.layer=5;g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x-w/2,-y+h/2);r.sizeDelta=new Vector2(w,h);return r;
        }
        Image Image(Transform parent,string name,Sprite sprite,float x,float y,float w,float h,Color color)
        {
            var im=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Image>();im.sprite=sprite;im.color=color;im.raycastTarget=false;return im;
        }
        Color Alpha(Color c,float a){c.a=a;return c;}
        void Stroke(Transform parent,Vector2 a,Vector2 b,float width,Color color)
        {
            var d=b-a;var r=Rect(parent,"Celestial stroke",(a.x+b.x)/2,(a.y+b.y)/2,d.magnitude,width);r.pivot=Vector2.one*.5f;r.anchoredPosition=new Vector2((a.x+b.x)/2,-(a.y+b.y)/2);r.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);var im=r.gameObject.AddComponent<Image>();im.color=color;im.raycastTarget=false;
        }
        void Arc(Transform parent,float radius,float start,float end,float width,Color color)
        {
            int n=Mathf.CeilToInt((end-start)/7);for(int i=0;i<n;i++){float a=Mathf.Lerp(start,end,i/(float)n)*Mathf.Deg2Rad,b=Mathf.Lerp(start,end,(i+1)/(float)n)*Mathf.Deg2Rad;Stroke(parent,P(40+Mathf.Cos(a)*radius,40+Mathf.Sin(a)*radius),P(40+Mathf.Cos(b)*radius,40+Mathf.Sin(b)*radius),width,color);}
        }
        void Link(Vector2 a,Vector2 b,bool drawn)
        {
            var g=new GameObject(drawn?"Drawn progress link":"Untouched guide",typeof(RectTransform),typeof(Canvas));g.transform.SetParent(Markers.transform,false);var c=g.GetComponent<Canvas>();c.renderMode=RenderMode.WorldSpace;c.worldCamera=cam;c.sortingOrder=20;SurfaceDirectionLayout.PlaceCanvas(c,cam,1.3f);
            // Only faint scaffolding for future stages; completed route is a warm hairline.
            var aa=Vector2.MoveTowards(a,b,40);var bb=Vector2.MoveTowards(b,a,40);Stroke(g.transform,aa,bb,drawn?1.5f:1,Alpha(drawn?gold:dim,drawn?.65f:.20f));
            if(finale)Stroke(g.transform,aa,bb,4,Alpha(gold,.10f));
        }
        void Node(int i)
        {
            bool done=i<Cleared,current=i==Current,focus=i==Focus;int score=Scores[i];Vector2 p=Centres[i];
            var g=new GameObject("Stage "+(i+1)+" "+(done?"cleared "+score:current?"current":"untouched"),typeof(RectTransform),typeof(Canvas));g.transform.SetParent(Markers.transform,false);var c=g.GetComponent<Canvas>();c.renderMode=RenderMode.WorldSpace;c.worldCamera=cam;c.sortingOrder=50;
            var r=(RectTransform)g.transform;r.sizeDelta=new Vector2(80,80);r.SetPositionAndRotation(cam.ViewportToWorldPoint(new Vector3(p.x/1280,1-p.y/720,1.0f)),cam.transform.rotation);float unit=2*cam.orthographicSize/720;
            float scale=focus?(phase=="pressed"?.91f:phase=="hover"?1.17f:phase=="release"?1.09f:phase=="idle"?1:1.12f):1;r.localScale=Vector3.one*unit*scale;
            Color color=done?gold:cream;float glow=current?.95f:done?.70f:.43f;
            Image(r,"Soft touch halo",halo,40,40,78,78,Alpha(current?scarves[0]:color,glow));
            // A dark rim isolates all glyphs on bright frost or V7 painted highlights.
            Image(r,"Contrast underlay",Ui.Circle,40,40,43,43,Alpha(ink,.78f));
            Arc(r,27,0,360,1.2f,Alpha(done?gold:dim,done?.45f:.52f));
            if(variant==0)
            {
                // Score becomes the node's own rays; no secondary score glyph.
                Image(r,"Node core",Ui.Circle,40,40,done?13:8,done?13:8,done?gold:dim);
                if(done)for(int k=0;k<score;k++){float angle=-90+k*120;var v=P(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));Stroke(r,P(40,40)+v*10,P(40,40)+v*23,3,gold);}
                else Arc(r,8,0,360,1.5f,cream);
            }
            else if(variant==1)
            {
                // Four-ray stage body, round orbiting score sparks: distinct visual categories.
                Image(r,"Stage star dark rim",star,40,40,41,41,ink);
                Image(r,"Stage star",star,40,40,done?34:27,done?34:27,done?gold:cream);
                if(!done)Image(r,"Unvisited star centre",Ui.Circle,40,40,7,7,ink);
                if(done)for(int k=0;k<score;k++){float a=(-90+k*120)*Mathf.Deg2Rad;float x=40+Mathf.Cos(a)*25,y=40+Mathf.Sin(a)*25;Image(r,"Score spark halo",halo,x,y,18,18,Alpha(gold,.9f));Image(r,"Earned round spark",Ui.Circle,x,y,7,7,gold);}
            }
            else
            {
                // A stone-like diamond and cumulative crescent bands: score is contour density.
                Image(r,"Diamond rim",diamond,40,40,29,36,done?gold:cream);
                if(!done)Image(r,"Open diamond",diamond,40,40,22,27,ink);
                if(done)for(int k=0;k<score;k++)Arc(r,19+k*5,20,160,2,gold);
            }
            if(current)
            {
                // Three coloured arcs plus a white pointer identify current independently of focus.
                for(int k=0;k<3;k++)Arc(r,33,-85+k*120,5+k*120,3.2f,scarves[k]);
                Image(r,"Current scarf heart",Ui.Circle,40,40,8,8,scarves[2]);Stroke(r,P(34,6),P(40,1),1.8f,cream);Stroke(r,P(40,1),P(46,6),1.8f,cream);
            }
            if(focus&&phase!="idle")
            {
                Arc(r,35,25,65,2,cream);Arc(r,35,145,185,2,cream);Arc(r,35,265,305,2,cream);
                if(phase=="pressed")Image(r,"Pressed core flash",halo,40,40,58,58,Alpha(cream,.9f));
            }
            if(finale){Image(r,"Finale aureole",halo,40,40,80,80,Alpha(gold,.35f));Arc(r,31,0,360,1.2f,Alpha(gold,.8f));}
            // Independent 80px hit box; visible focus expansion never shrinks the touch target.
            var hit=Rect(canvas,"80px selectable stage "+(i+1),p.x,p.y,80,80);hit.gameObject.AddComponent<Image>().color=Color.clear;hit.gameObject.AddComponent<Button>().transition=Selectable.Transition.None;Targets.Add(new Rect(p-Vector2.one*40,Vector2.one*80));
        }
        void Nav(string id,float x,float y)
        {
            var s=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Shared/Art/UI/U5/4x/"+id+".png");Image(canvas,id,s,x,y,48,48,cream);var r=Rect(canvas,id+" 80px target",x,y,80,80);r.gameObject.AddComponent<Image>().color=Color.clear;r.gameObject.AddComponent<Button>();Targets.Add(new Rect(x-40,y-40,80,80));
        }
        public void Dispose(){UnityEngine.Object.DestroyImmediate(Markers);UnityEngine.Object.DestroyImmediate(Overlay.gameObject);foreach(var s in sprites)UnityEngine.Object.DestroyImmediate(s);foreach(var t in textures)UnityEngine.Object.DestroyImmediate(t);}
    }
}
