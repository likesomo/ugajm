using System;
using System.Collections.Generic;
using UnityEngine;
namespace SevenSteps.Art.StageSelectReview
{
    // Review-only faceted celestial bodies: vertex-colour geometry, no tile grid.
    public sealed class PlanetBodyStudy
    {
        readonly List<Vector3> v=new List<Vector3>();readonly List<int> t=new List<int>();readonly List<Color> c=new List<Color>();
        public Func<Vector3,Vector3> Project;
        public float Lift;
        static readonly Color[] Palette={Hex("7A897B"),Hex("424958"),Hex("B56645"),Hex("D0DCDF"),Hex("596777"),Hex("C3A564"),Hex("969782")};
        static Color Hex(string h){ColorUtility.TryParseHtmlString("#"+h,out var c);return c;}
        void RawTri(Vector3 a,Vector3 b,Vector3 d,Color color){int n=v.Count;v.AddRange(new[]{a,b,d});t.AddRange(new[]{n,n+1,n+2});for(int k=0;k<3;k++)c.Add(QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color);}
        void Tri(Vector3 a,Vector3 b,Vector3 d,Color color)
        {
            if(Project==null){RawTri(a,b,d,color);return;}
            Vector3 P(Vector3 point){var q=Project(point);return q+q.normalized*Lift;}
            var ab=P((a+b)*.5f);var bd=P((b+d)*.5f);var da=P((d+a)*.5f);a=P(a);b=P(b);d=P(d);
            RawTri(a,ab,da,color);RawTri(ab,b,bd,color);RawTri(da,bd,d,color);RawTri(ab,bd,da,color);
        }
        public void Box(Vector3 p,Vector3 size,Color color,Quaternion rotation)
        {
            var q=new Vector3[8];for(int k=0;k<8;k++)q[k]=p+rotation*Vector3.Scale(size,new Vector3((k&1)==0?-.5f:.5f,(k&2)==0?-.5f:.5f,(k&4)==0?-.5f:.5f));
            int[] f={0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5};
            for(int k=0;k<f.Length;k+=3)Tri(q[f[k]],q[f[k+1]],q[f[k+2]],color);
        }
        public void Disc(Vector3 p,Vector3 normal,float radius,Color color,int sides=16)
        {var r=Quaternion.FromToRotation(Vector3.up,normal);for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;Tri(p,p+r*new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius),p+r*new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),color);}}
        public void Star(Vector3 p,Vector3 normal,float radius,Color color)
        {var r=Quaternion.FromToRotation(Vector3.up,normal);for(int k=0;k<10;k++){float a=k*Mathf.PI/5,b=(k+1)*Mathf.PI/5;Tri(p,p+r*new Vector3(Mathf.Cos(b)*radius*((k+1)%2==0?1:.4f),0,Mathf.Sin(b)*radius*((k+1)%2==0?1:.4f)),p+r*new Vector3(Mathf.Cos(a)*radius*(k%2==0?1:.4f),0,Mathf.Sin(a)*radius*(k%2==0?1:.4f)),color);}}
        void Crystal(Vector3 p,float height,Color color)
        {for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5,b=(i+1)*Mathf.PI*2/5;Tri(p+new Vector3(Mathf.Cos(a)*.22f,0,Mathf.Sin(a)*.22f),p+Vector3.up*height,p+new Vector3(Mathf.Cos(b)*.22f,0,Mathf.Sin(b)*.22f),color*(.8f+i*.04f));}}
        static Vector3 Surface(int p,float lat,float lon)
        {
            var n=new Vector3(Mathf.Cos(lat)*Mathf.Cos(lon),Mathf.Sin(lat),Mathf.Cos(lat)*Mathf.Sin(lon));
            float noise=1+Mathf.Cos(lat)*(.045f*Mathf.Sin(lon*7+lat*9+p*2)+.035f*Mathf.Cos(lat*11-lon*3));
            if(p==2)noise*=1+.055f*Mathf.Floor(Mathf.Sin(lat)*7);
            if(p==5)noise+=.07f*Mathf.Cos(lat)*Mathf.Sin(lon*11+lat*9);
            if(p==1)
                foreach(var crater in new[]{new Vector3(-.45f,.6f,-.7f).normalized,new Vector3(.5f,.1f,-.85f).normalized,new Vector3(.35f,.8f,.1f).normalized,new Vector3(-.9f,-.1f,-.3f).normalized})
                {float d=Vector3.Distance(n,crater);if(d<.36f)noise-=.24f*Mathf.Pow(1-d/.36f,1.3f);else if(d<.46f)noise+=.035f;}
            return Vector3.Scale(n*3.65f*noise,new Vector3(1,.88f,1));
        }
        public static GameObject Build(int p,Material material,Transform parent,float yaw=0,bool clearSummit=false)
        {
            var m=new PlanetBodyStudy();const int rings=16,segs=32;
            for(int y=0;y<rings;y++)for(int x=0;x<segs;x++)
            {
                float lat0=-Mathf.PI/2+y*Mathf.PI/rings,lat1=-Mathf.PI/2+(y+1)*Mathf.PI/rings,a=x*Mathf.PI*2/segs,b=(x+1)*Mathf.PI*2/segs;
                var color=Palette[p]*(.94f+.09f*Mathf.Cos((lat0+lat1)*.5f)*Mathf.Sin(x*17+y*23)*Mathf.Sin(x*7+y));
                if(p==0&&y>10&&Mathf.Sin(x*7+y*3)>.05f)color=Hex("607A4A");
                if(p==0&&y>=14)color=Hex("607A4A");
                if(p==2)color=Color.Lerp(Hex("834633"),Hex("CD8962"),(y%3)/2f);
                if(p==3&&y%3==0)color=Hex("9BBBC5");
                m.Tri(Surface(p,lat0,a),Surface(p,lat1,a),Surface(p,lat1,b),color);
                m.Tri(Surface(p,lat0,a),Surface(p,lat1,b),Surface(p,lat0,b),color);
            }
            for(int i=0;i<(p==6?7:5);i++)
            {
                float a=i*Mathf.PI*2/(p==6?7:5);var pos=new Vector3(Mathf.Cos(a)*1.9f,2.7f,Mathf.Sin(a)*1.9f);
                if(clearSummit&&pos.z<.8f)continue;
                if(p==0||p==6)m.Box(pos+Vector3.up*.45f,new Vector3(.32f,p==6?1.4f:1,.40f),p==0?Hex("A7AB91"):Hex("ADA991"),Quaternion.Euler(0,i*47,5*Mathf.Sin(i)));
                if(p==3)for(int k=0;k<2;k++)m.Crystal(pos+new Vector3(k*.25f,0,0),1.3f+k*.5f,Hex("A3DBE6"));
            }
            if(p==4)
            {
                for(int row=0;row<3;row++)for(int side=0;side<4;side++)
                {float a=side*Mathf.PI/2;var r=Quaternion.Euler(0,-side*90,0);m.Box(new Vector3(Mathf.Cos(a)*3.1f,(row-1)*.9f,Mathf.Sin(a)*3.1f),new Vector3(.35f,.28f,4.4f),Hex("987748"),r);}
                for(int i=-1;i<=1;i++)m.Box(new Vector3(i*1.4f,2.9f,0),new Vector3(.23f,.27f,4),Hex("B08B50"),Quaternion.identity);
            }
            if(p==5)for(int i=0;i<12;i++)
            {float a=i*Mathf.PI/6;var pos=new Vector3(Mathf.Cos(a)*4.05f,Mathf.Sin(i*3.7f)*1.4f-1,Mathf.Sin(a)*4.05f);m.Box(pos,Vector3.one*(.25f+i%3*.14f),Hex("C7AB70"),Quaternion.Euler(i*41,i*31,i*17));}
            if(p==1)for(int i=0;i<10;i++)
            {float a=-2.7f+i*.24f,lat=.15f+(i%3)*.24f;var pos=Surface(p,lat,a);m.Disc(pos+pos.normalized*.015f,pos.normalized,.13f+(i%3)*.025f,Hex("232B38"),8);}
            var go=m.Draw("Planet body / "+p,material,parent);go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            if(go.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3>4000)throw new Exception("Planet body exceeds 4k triangles");return go;
        }
        public GameObject Draw(string name,Material material,Transform parent)
        {var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetColors(c);mesh.RecalculateNormals();mesh.RecalculateBounds();var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;}
    }
}
