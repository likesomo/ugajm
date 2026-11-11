using System;
using UnityEngine;
namespace SevenSteps.Art.StageSelectReview
{
    public sealed partial class PlanetBodyStudy
    {
        public static bool UseV4;
        public static bool PolishV4b;
        static float Noise(Vector3 n,float scale)=>Mathf.PerlinNoise(n.x*scale+n.z*scale*.37f+19,n.y*scale+n.z*scale*.71f+31);
        static Vector3 Pore(int i,int count)
        {float y=1-2*(i+.5f)/count;float a=i*2.39996323f;return new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(a),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(a));}
        static float Pit(Vector3 n,int count,out float rim)
        {float depth=0;rim=0;for(int i=0;i<count;i++){float r=.065f+(i%5)*.018f;float d=Vector3.Distance(n,Pore(i,count))/r;if(d<1)depth=Mathf.Max(depth,Mathf.Pow(1-d*d,1.2f));if(d>.82f&&d<1.18f)rim=Mathf.Max(rim,1-Mathf.Abs(d-1)*5);}return depth;}
        static Vector3 StonePoint(int p,Vector3 n,bool seat=false)
        {
            float grain=(Noise(n,17)-.5f)*.055f+(Noise(n,5)-.5f)*.09f;float radius=3.65f+grain;
            if(p==1){radius-=Pit(n,100,out var rim)*.34f;radius+=rim*.035f;float d=Vector3.Distance(n,new Vector3(0,.64f,-.77f).normalized);if(d<.48f)radius-=.85f*Mathf.Pow(1-d/.48f,1.2f);}
            if(p==2)radius+=Mathf.Sin(n.y*47+n.z*1.8f)*.045f+Mathf.Sin(n.y*21)*.045f;
            if(PolishV4b&&p==2){float ledge=Mathf.Repeat(n.y*6,1);radius=3.65f+(Noise(n,6)-.5f)*.025f+.18f*(1-Mathf.SmoothStep(.05f,.38f,ledge))-.02f;}
            if(p==3)radius+=Mathf.Sin(n.x*29+n.z*17)*.015f;
            if(p==4)radius-=Mathf.Pow(1-Mathf.Abs(Mathf.Sin(n.x*16+n.z*7)),14)*.035f;
            if(p==5){radius-=Pit(n,85,out var rim)*.14f;radius+=Mathf.Sin(n.y*33+Mathf.Sin(Mathf.Atan2(n.z,n.x)*13)*.6f)*.10f;radius-=Mathf.Pow(Mathf.Max(0,Mathf.Sin(Mathf.Atan2(n.z,n.x)*19+n.y*3)),10)*.11f;}
            if(PolishV4b&&p==5){float phase=Mathf.Repeat(n.y*5.5f+.045f*Mathf.Sin(Mathf.Atan2(n.z,n.x)*13),1);radius=3.65f+.15f*(1-Mathf.SmoothStep(.10f,.42f,phase))-Pit(n,65,out var lip)*.20f;radius-=Mathf.Pow(Mathf.Max(0,Mathf.Sin(Mathf.Atan2(n.z,n.x)*17+n.y*1.8f)),10)*.14f;}
            var q=Vector3.Scale(n*radius,new Vector3(1,.88f,1));
            if(seat&&p==0&&Mathf.Abs(q.x)<2.72f&&Mathf.Abs(q.z)<1.12f&&q.y>2.25f)q.y=2.25f;
            return q;
        }
        static Color StoneColor(int p,Vector3 n)
        {
            float grain=Noise(n,35),broad=Noise(n,5),mid=Noise(n,13);Color color;
            switch(p)
            {
                case 0:color=Color.Lerp(Hex("77786F"),Hex("C1B9A2"),grain);if(broad>.54f)color=Color.Lerp(Hex("616836"),Hex("929345"),mid);else if(grain>.83f)color=Hex("DDD4BC");break;
                case 1:float pit=Pit(n,100,out var rim);color=Color.Lerp(Hex("6C6057"),Hex("292A29"),pit);color=Color.Lerp(color,Hex("95816A"),rim*.3f);if(Vector3.Distance(n,new Vector3(0,.64f,-.77f).normalized)<.29f)color=Hex("22262A");break;
                case 2:float band=Mathf.Sin(n.y*47+n.z*1.8f);color=Color.Lerp(Hex("8E452F"),Hex("CA8159"),.5f+band*.32f+grain*.18f);if(Mathf.Abs(Mathf.Sin(n.y*23+n.x))<.08f)color*=.74f;break;
                case 3:float stream=Mathf.Abs(n.z+.32f*Mathf.Sin(n.x*4));color=Color.Lerp(Hex("C6D0CD"),Hex("F0EEE0"),grain);if(stream<.16f)color=Color.Lerp(Hex("79AEC5"),Hex("BEDCE2"),stream/.16f);break;
                case 4:float plate=Mathf.Abs(Mathf.Sin(n.x*15+n.z*6)*Mathf.Sin(n.y*13-n.z*8));color=Color.Lerp(Hex("343F49"),Hex("687079"),grain);if(plate<.06f)color*=.56f;float strap=Mathf.Min(Mathf.Abs(n.y-.36f),Mathf.Abs(n.y+.36f));float diagonal=Mathf.Abs(Mathf.Sin(Mathf.Atan2(n.z,n.x)*2+n.y*2));if(strap<.07f||diagonal<.07f)color=Hex("99703E");if((strap>.048f&&strap<.073f)||(diagonal>.048f&&diagonal<.078f))color=Hex("C4A05B");break;
                case 5:float pore=Pit(n,85,out var lip);float terrace=Mathf.Sin(n.y*33+Mathf.Sin(Mathf.Atan2(n.z,n.x)*13)*.6f);color=Color.Lerp(Hex("B39B72"),Hex("E0CC9E"),grain);color*=1-pore*.38f;if(terrace<-.62f)color*=.72f;else if(terrace>.70f)color=Color.Lerp(color,Hex("EAD8AE"),.65f);break;
                default:color=Color.Lerp(Hex("999178"),Hex("C2AE74"),grain);if(broad>.51f)color=Color.Lerp(Hex("9B9444"),Hex("B9AE64"),mid);break;
            }
            if(PolishV4b&&p==2){float phase=Mathf.Repeat(n.y*6,1);color=Color.Lerp(Hex("9A4E35"),Hex("CB8056"),.40f+grain*.15f);if(phase<.13f)color*=.58f;else if(phase<.33f)color=Hex("DD996A");}
            if(PolishV4b&&p==5){float phase=Mathf.Repeat(n.y*5.5f+.045f*Mathf.Sin(Mathf.Atan2(n.z,n.x)*13),1);color=Color.Lerp(Hex("B49A73"),Hex("E7D2AB"),grain);if(phase<.16f)color*=.64f;else if(phase<.36f)color=Hex("EAD9B5");color*=1-Pit(n,65,out var lip)*.36f;}
            if(PolishV4b&&p==6){color=Color.Lerp(Hex("88887C"),Hex("BDB29A"),grain);if(broad>.61f)color=Color.Lerp(Hex("9C914A"),Hex("B0A35A"),mid);}
            return color;
        }
        void StoneTri(int p,Vector3 a,Vector3 b,Vector3 d,bool seat)
        {int start=v.Count;foreach(var n in new[]{a,b,d}){v.Add(StonePoint(p,n,seat));var color=StoneColor(p,n);c.Add(QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color);}t.AddRange(new[]{start,start+1,start+2});}
        static GameObject BuildV4(int p,Material material,Transform parent,float yaw,bool seat)
        {
            var m=new PlanetBodyStudy();float g=(1+Mathf.Sqrt(5))/2;
            var points=new[]{new Vector3(-1,g,0),new Vector3(1,g,0),new Vector3(-1,-g,0),new Vector3(1,-g,0),new Vector3(0,-1,g),new Vector3(0,1,g),new Vector3(0,-1,-g),new Vector3(0,1,-g),new Vector3(g,0,-1),new Vector3(g,0,1),new Vector3(-g,0,-1),new Vector3(-g,0,1)};
            int[] faces={0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1};
            void Sub(Vector3 a,Vector3 b,Vector3 d,int depth)
            {if(depth==0){m.StoneTri(p,a,b,d,seat);return;}var ab=(a+b).normalized;var bd=(b+d).normalized;var da=(d+a).normalized;Sub(a,ab,da,depth-1);Sub(b,bd,ab,depth-1);Sub(d,da,bd,depth-1);Sub(ab,bd,da,depth-1);}
            for(int i=0;i<faces.Length;i+=3)Sub(points[faces[i]].normalized,points[faces[i+1]].normalized,points[faces[i+2]].normalized,4);
            if(p==0)
            {int flowers=0;for(int i=0;i<160&&flowers<24;i++){var n=Pore(i,160);if(Noise(n,5)<.55f||n.y<-.15f)continue;var pos=StonePoint(p,n,seat)+n*.025f;var rot=Quaternion.FromToRotation(Vector3.up,n);for(int k=0;k<5;k++){float a=k*Mathf.PI*2/5;var petal=pos+rot*new Vector3(Mathf.Cos(a)*.047f,0,Mathf.Sin(a)*.047f);m.Disc(petal,n,.033f,Hex("F3EEDC"),4);}m.Disc(pos+n*.004f,n,.018f,Hex("C4AE64"),5);flowers++;}}
            if(p==4)for(int row=0;row<2;row++)for(int k=0;k<16;k++)
            {float a=k*Mathf.PI/8;float y=row==0?.36f:-.36f;var n=new Vector3(Mathf.Cos(a)*Mathf.Sqrt(1-y*y),y,Mathf.Sin(a)*Mathf.Sqrt(1-y*y));m.Box(StonePoint(p,n)+n*.065f,Vector3.one*.105f,Hex("CCAC67"),Quaternion.FromToRotation(Vector3.up,n));}
            if(p==4)for(int row=0;row<2;row++)for(int segment=0;segment<32;segment++)
            {
                float center=row==0?.36f:-.36f,a=segment*Mathf.PI/16,b=(segment+1)*Mathf.PI/16;
                Vector3 StrapPoint(float y,float angle){var n=new Vector3(Mathf.Cos(angle)*Mathf.Sqrt(1-y*y),y,Mathf.Sin(angle)*Mathf.Sqrt(1-y*y));return StonePoint(p,n)+n*.05f;}
                void Band(float lo,float hi,Color color){var v0=StrapPoint(lo,a);var v1=StrapPoint(lo,b);var v2=StrapPoint(hi,b);var v3=StrapPoint(hi,a);m.Tri(v0,v3,v2,color);m.Tri(v0,v2,v1,color);}
                Band(center-.045f,center+.045f,Hex("9C7648")*(.94f+.06f*Mathf.Sin(segment*4)));
                Band(center-.054f,center-.045f,Hex("C5A05A"));Band(center+.045f,center+.054f,Hex("C5A05A"));
            }
            if(p==6)for(int k=0;k<7;k++){float a=k*Mathf.PI*2/7;float r=PolishV4b?1.50f:1.22f;m.Box(new Vector3(Mathf.Cos(a)*r,PolishV4b?3.15f:3.20f,Mathf.Sin(a)*r),PolishV4b?new Vector3(.46f,.72f,.40f):new Vector3(.24f,.42f,.27f),PolishV4b?Hex("CAC0A5"):Hex("BFB28B"),Quaternion.Euler(0,k*51,0));}
            if(p==3)for(int k=0;k<6;k++){float a=k*Mathf.PI/3;var pos=new Vector3(Mathf.Cos(a)*1.5f,2.80f,Mathf.Sin(a)*1.5f);m.Crystal(pos,.40f,Hex("D1EBEA"));}
            if(p==3)for(int line=0;line<5;line++)for(int k=0;k<24;k++)
            {
                Vector3 Crack(float u){float a=-2.8f+u*2.5f;return Quaternion.Euler(0,line*71,0)*new Vector3(Mathf.Cos(a),Mathf.Sin(u*5+line)*.35f,Mathf.Sin(a)).normalized;}
                var n=Crack(k/24f);var n2=Crack((k+1)/24f);var a=StonePoint(p,n)+n*.028f;var b=StonePoint(p,n2)+n2*.028f;var side=Vector3.Cross(n,b-a).normalized*.025f;
                m.Tri(a-side,b-side,b+side,Hex("5F91A9"));m.Tri(a-side,b+side,a+side,Hex("5F91A9"));
            }
            if(p==2&&!PolishV4b)for(int k=0;k<8;k++){var n=new Vector3(-.6f+k*.17f,.20f,-1).normalized;var n2=(n+new Vector3(.03f,.065f,0)).normalized;var pos=StonePoint(p,n)+n*.024f;var end=StonePoint(p,n2)+n2*.024f;var delta=end-pos;m.Box((pos+end)*.5f,new Vector3(.018f,.018f,delta.magnitude),Hex("E1A16A"),Quaternion.LookRotation(delta));}
            if(p==2&&PolishV4b)for(int glyph=0;glyph<3;glyph++)
            {
                Vector3 Mark(float x,float y){var n=new Vector3(x,y,-1).normalized;return StonePoint(p,n)+n*.07f;}
                void Stroke(Vector3 a,Vector3 b){var side=Vector3.Cross((a+b).normalized,b-a).normalized*.035f;m.Tri(a-side,b+side,b-side,Hex("542F29"));m.Tri(a-side,a+side,b+side,Hex("542F29"));}
                float x=-.45f+glyph*.43f;Stroke(Mark(x,.20f),Mark(x,.48f));Stroke(Mark(x-.10f,.42f),Mark(x+.10f,.32f));Stroke(Mark(x-.10f,.28f),Mark(x+.10f,.38f));
            }
            if(p==5&&PolishV4b)for(int row=0;row<3;row++)for(int k=0;k<10;k++)
            {
                float a=k*Mathf.PI/5+.17f*row;float y=.45f-row*.36f;var n=new Vector3(Mathf.Cos(a)*Mathf.Sqrt(1-y*y),y,Mathf.Sin(a)*Mathf.Sqrt(1-y*y));var center=StonePoint(p,n)+n*.09f;var side=Vector3.Cross(Vector3.up,n).normalized*.07f;var tip=center+Vector3.down*(.30f+(k%3)*.08f)+n*.045f;
                m.Tri(center-side,tip,center+n*.09f,Hex("E9D5AF"));m.Tri(center+n*.09f,tip,center+side,Hex("C8AF87"));
            }
            var go=m.Draw("Procedural stone planet / "+p,material,parent);go.transform.localRotation=Quaternion.Euler(0,yaw,0);int tris=go.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3;if(tris<4000||tris>6000)throw new Exception("V4 body outside 4-6k triangle budget: "+tris);return go;
        }
    }
}
