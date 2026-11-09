using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace SevenSteps.Art
{
    /// <summary>Art-only bilingual layouts with real embedded fonts. No localization/game-flow state is changed.</summary>
    public sealed class HandwrittenUiPreview : MonoBehaviour
    {
        public Font englishTitle,englishBody,koreanTitle,koreanBody,numbers;
        public Sprite background;
        public RuinsHengeUiResources kit;
        Canvas canvas;RectTransform root;bool ko;
        Color ink=new Color32(238,230,212,255),gold=new Color32(201,171,107,255),navy=new Color32(16,28,50,240),dim=new Color32(160,176,186,255);
        string L(string en,string kr)=>ko?kr:en;
        Font Title=>ko?koreanTitle:englishTitle;
        Font Body=>ko?koreanBody:englishBody;
        string[] ids={"moss","hollow","carved","frost","joinery","crumble","altar"};
        string[] enNames={"Moss Orbit","Hollow Moon","Carved World","Frost Valley","Timber Crossing","Crumbling Star","Circle of Stones"};
        string[] koNames={"이끼의 궤도","별구멍 달","새김돌 행성","서리 골짜기","짜맞춤 별","무너지는 별","선돌의 제단"};
        void Awake()
        {
            Application.targetFrameRate=60;
            canvas=new GameObject("BilingualPreview",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scale=canvas.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);scale.matchWidthOrHeight=.5f;
            Show("title",false);
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int k=Array.IndexOf(args,"-ui-capture");
            if(k<0||k+1>=args.Length)yield break;
            string output=args[k+1];
            foreach(bool language in new[]{false,true})foreach(string screen in new[]{"title","stage-select","hud","result","loading","chapter-entry","language","settings"})
            {
                Show(screen,language);yield return null;yield return new WaitForEndOfFrame();
                string folder=System.IO.Path.Combine(output,language?"ko":"en");System.IO.Directory.CreateDirectory(folder);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,screen+".png"));yield return new WaitForSecondsRealtime(.25f);
                if(screen=="result")foreach(int earned in new[]{1,3})
                {
                    int star=0;foreach(var image in root.GetComponentsInChildren<Image>())if(image.name=="RewardStar")image.sprite=star++<earned?kit.starFilled:kit.starEmpty;
                    yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"result-"+earned+".png"));yield return new WaitForSecondsRealtime(.25f);
                }
            }
            Debug.Log("[HandwrittenUI] Captured 20 actual-font screens at "+Screen.width+"x"+Screen.height);yield return new WaitForSecondsRealtime(.5f);Application.Quit();
        }
        RectTransform Rect(string name,float x,float y,float w,float h,Transform parent=null)
        {
            var go=new GameObject(name,typeof(RectTransform));var r=go.GetComponent<RectTransform>();r.SetParent(parent?parent:root,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        Image Image(string name,float x,float y,float w,float h,Sprite sprite,Color color)
        {
            var r=Rect(name,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=color;image.raycastTarget=false;if(sprite)image.type=UnityEngine.UI.Image.Type.Sliced;return image;
        }
        Text Text(string value,float x,float y,float w,float h,int size=25,Font font=null,Color? color=null,TextAnchor align=TextAnchor.UpperLeft)
        {
            var r=Rect("Text",x,y,w,h);var text=r.gameObject.AddComponent<Text>();text.font=font?font:Body;text.text=value;text.fontSize=size;text.color=color??ink;text.alignment=align;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.supportRichText=false;text.raycastTarget=false;text.lineSpacing=ko?1.10f:.90f;return text;
        }
        void Button(string value,float x,float y,float w=330,bool bright=false,Font font=null)
        {
            Image("Button",x,y,w,55,bright?kit.buttonHover:kit.buttonDefault,Color.white);Text(value,x+12,y+8,w-24,40,25,font,bright?gold:ink,TextAnchor.MiddleCenter);
        }
        void Stars(int count,float x,float y,float size=28)
        {for(int i=0;i<3;i++)Image("RewardStar",x+i*(size+9),y,size,size,i<count?kit.starFilled:kit.starEmpty,Color.white);}
        public void Show(string screen,bool korean)
        {
            ko=korean;if(root)Destroy(root.gameObject);root=Rect("Layout",0,0,1280,720,canvas.transform);
            Image("Background",0,0,1280,720,background,Color.white).type=UnityEngine.UI.Image.Type.Simple;
            Image("NavyWash",0,0,1280,720,null,new Color(0.04f,.08f,.15f,screen=="title"?.12f:screen=="stage-select"?.97f:.76f));
            Text("EN / KO",1130,22,120,28,20,numbers,gold,TextAnchor.UpperRight);
            if(screen=="title")TitleScreen();else if(screen=="stage-select")SelectScreen();else if(screen=="hud")Hud();else if(screen=="result")Result();else if(screen=="loading")Loading();else if(screen=="chapter-entry")Chapter();else Language(screen=="settings");
            Text(L("Handwritten UI study | Art preview","손글씨 UI 시안 | 아트 프리뷰"),34,686,700,24,16,null,dim);
        }
        void Header(string heading,string detail="") {Text(heading,40,30,1070,72,48,Title);if(detail!="")Text(detail,42,108,1160,48,24,null,dim);}
        void TitleScreen()
        {
            Image("MenuPanel",32,40,450,624,kit.panelStone,Color.white);
            Text(L("Seven Steps","일곱 걸음"),62,67,390,105,ko?66:70,Title,gold);
            Text(L("Three wanderers. One sky.","세 방랑자, 하나의 하늘."),64,178,390,46,25);
            string[] labels=ko?new[]{"시작","이어하기","스테이지","설정","크레딧"}:new[]{"Begin the Journey","Continue Journey","Choose a Stage","Settings","Credits"};
            for(int i=0;i<labels.Length;i++)Button(labels[i],70,257+i*70,370,i==0);
            Text(L("English / 한국어","한국어 / English"),72,620,370,30,22,numbers,dim,TextAnchor.MiddleCenter);
        }
        void SelectScreen()
        {
            Header(L("Choose a planet","행성 고르기"),L("All seven worlds are open. Stars record your journey.","일곱 행성은 모두 열려 있습니다. 별은 여정의 기록입니다."));
            Vector2 P(int i)=>new Vector2(60+kit.dipper[i].x*1140,190+kit.dipper[i].y*650);
            foreach(var edge in new[]{(0,1),(1,2),(2,3),(3,0),(3,4),(4,5),(5,6)})
            {
                var start=P(edge.Item1);var end=P(edge.Item2);var line=Rect("PlanetConnection",start.x,start.y,Vector2.Distance(start,end),2);
                line.pivot=new Vector2(0,.5f);line.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(end.y-start.y,end.x-start.x)*Mathf.Rad2Deg);line.gameObject.AddComponent<Image>().color=dim;
            }
            for(int i=0;i<7;i++)
            {
                var p=P(i);Image("PlanetCard_"+ids[i],p.x-65,p.y-24,130,48,kit.panelWood,i==0?gold:Color.white);
                Text((i+1).ToString(),p.x-14,p.y-18,28,36,25,numbers,gold,TextAnchor.MiddleCenter);
                Text((ko?koNames:enNames)[i],p.x-90,p.y+31,180,66,23,Body,ink,TextAnchor.UpperCenter);
                Stars(i<4?3:2,p.x-38,p.y+95,20);
            }
            var summary=Text(L("Chapter stars:","\uCC55\uD130 \uBCC4:"),440,582,400,40,30,Body,gold);
            summary.horizontalOverflow=HorizontalWrapMode.Overflow;summary.alignment=TextAnchor.MiddleLeft;float labelWidth=summary.preferredWidth;
            summary.rectTransform.sizeDelta=new Vector2(labelWidth+2,40);
            Text("18 / 21",440+labelWidth+12,582,160,40,27,numbers,gold,TextAnchor.MiddleLeft);
            Button(L("Back","뒤로"),40,624,230);Text(L("No locks. Replay any chapter.","잠금 없이 어느 챕터든 다시 도전할 수 있습니다."),310,636,850,35,24);
        }
        void Dipper(float x,float y,int lit)
        {
            Vector2 P(int i)=>new Vector2(x+kit.dipper[i].x*330,y+kit.dipper[i].y*150);
            foreach(var edge in new[]{(0,1),(1,2),(2,3),(3,0),(3,4),(4,5),(5,6)})
            {
                var a=P(edge.Item1);var b=P(edge.Item2);var r=Rect("DipperLine",a.x,a.y,Vector2.Distance(a,b),2);r.pivot=new Vector2(0,.5f);r.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);r.gameObject.AddComponent<Image>().color=dim;
            }
            for(int i=0;i<7;i++){var p=P(i);Image("Star_"+(i+1),p.x-12,p.y-12,24,24,i<lit?kit.starFilled:kit.starEmpty,Color.white);Text((i+1).ToString(),p.x-10,p.y+15,25,25,18,numbers);}
        }
        void Hud()
        {
            Text(L("08  Lowering the wall","08  내리는 벽"),35,26,440,66,39,Title,gold);Dipper(445,16,5);
            Text(L("Next step:","다음 걸음:"),486,126,220,38,26,null,ink,TextAnchor.MiddleRight);Text("6",724,127,40,38,27,numbers,gold);
            string[] states=ko?new[]{"사용 중","남음","선돌"}:new[]{"Active","Ready","Stone"};
            var sprites=new[]{kit.bodyA,kit.bodyB,kit.bodyStone};for(int i=0;i<3;i++){Image("Body",927+i*105,39,44,44,sprites[i],Color.white);Text(states[i],902+i*105,89,97,32,21,null,ink,TextAnchor.MiddleCenter);}
            // Board is an illustrative, countable diagram rather than a playable level.
            for(int yy=0;yy<5;yy++)for(int xx=0;xx<7;xx++){bool wall=yy==0||yy==4||xx==0||xx==6;Image("Cell",361+xx*78,170+yy*70,73,65,null,wall?new Color32(96,119,138,255):new Color32(214,208,177,255));}
            Image("Actor",602,313,47,54,kit.bodyA,Color.white);Image("KEEP",445,244,45,54,kit.bodyStone,Color.white);Text("= 2",755,312,66,44,29,numbers,navy);
            Text(L("The seventh step\nchanges the world.","일곱 번째 걸음만\n세상을 바꿉니다."),44,210,290,180,28);
            Text(L("Numbers stay clear","숫자는 또렷하게"),44,390,280,36,22,null,dim);Text("1 2 3 4 5 6 7\n= 0  = 1  = 2  = 3",44,437,294,80,27,numbers,gold);
            Text(L("Step on the lever on your seventh step.\nThe raised stone wall will move down.","일곱 번째 걸음에 레버를 밟으세요.\n올라온 석재 벽이 내려갑니다."),44,531,1160,76,22);
            string[] labels=ko?new[]{"되돌리기","다시 시작","힌트","설정"}:new[]{"Undo last step","Restart stage","Hint","Settings"};for(int i=0;i<4;i++)Button(labels[i],40+i*302,612,282);
        }
        void Result()
        {
            Image("ResultPanel",280,83,720,558,kit.panelStone,Color.white);Text(L("A path among the stars","별 사이로 열린 길"),320,110,640,80,48,Title,gold,TextAnchor.MiddleCenter);
            Stars(2,455,220,110);Text(L("Clear        Second goal        Third goal","클리어        두 번째 목표        세 번째 목표"),332,355,620,68,25,null,ink,TextAnchor.MiddleCenter);
            Text(L("Three-star criteria under review","3별 평가 기준 검토 중"),360,430,560,40,23,null,dim,TextAnchor.MiddleCenter);
            Button(L("Next stage","다음 스테이지"),318,526,202,true);Button(L("Try again","재도전"),536,526,202);Button(L("Stage select","스테이지"),754,526,202);
        }
        void Loading(){Header(L("Reading the star map","별지도를 펼치는 중"));Dipper(420,290,4);Text("4 / 7",570,425,200,48,34,numbers,gold);Text(L("A body left behind can become a bridge.","남겨진 몸이 다음 길의 다리가 됩니다."),190,553,900,64,29,null,ink,TextAnchor.MiddleCenter);}
        void Chapter(){Header(L("Chapter 05","챕터 05"));Text(L("Timber Crossing","짜맞춤 별"),80,205,1080,100,64,Title,gold,TextAnchor.MiddleCenter);for(int i=0;i<7;i++){Image(ids[i],56+i*174,397,145,85,kit.panelWood,i==4?gold:Color.white);Text((ko?koNames:enNames)[i],62+i*174,414,133,63,21,null,ink,TextAnchor.MiddleCenter);}Text(L("Follow the three wanderers to the next world.","세 방랑자와 함께 다음 행성으로 나아갑니다."),150,553,980,70,28,null,ink,TextAnchor.MiddleCenter);}
        void Language(bool settings)
        {
            Header(settings?L("Settings","설정"):L("Choose your language","언어를 선택하세요"));Image("LanguagePanel",305,191,670,367,kit.panelStone,Color.white);
            Text("English / 한국어",350,224,580,72,42,numbers,gold,TextAnchor.MiddleCenter);Button("English",350,325,270,!ko,englishBody);Button("한국어",652,325,270,ko,koreanBody);
            Text(L("You can change this later in Settings.","설정에서 언제든 언어를 바꿀 수 있습니다."),353,418,568,70,27,null,ink,TextAnchor.MiddleCenter);Button(L("Continue","계속"),482,585,316,true);
        }
    }
}
