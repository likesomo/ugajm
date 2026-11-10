using System.Collections.Generic;
using SevenSteps.Contracts;
using UnityEngine;
namespace SevenSteps.Art
{
    public sealed class RuinsHengePlanet : MonoBehaviour, IStageEnvironment
    {
        public Material surface;
        public string chapterId="moss";
        public Color summitTint=new Color32(198,202,182,255),rimTint=new Color32(116,141,87,255),rockTint=new Color32(88,107,128,255),moonTint=new Color32(126,172,178,255);
        GameObject terrain,decor;
        void Awake(){if(Application.absoluteURL.Contains("artperf=1"))gameObject.AddComponent<RuinsFrameSample>();}
        Color Tint(Color c)=>QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;
        static void Dispose(Object o){if(!o)return;if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
        void OnDestroy(){foreach(var f in GetComponentsInChildren<MeshFilter>())Dispose(f.sharedMesh);}
        public void Fit(BoardGrid grid){transform.position=grid.Origin;transform.localScale=Vector3.one*grid.CellSize;Rebuild(grid.W,grid.H);}
        public void Preview(int w,int h)=>Rebuild(w,h);
        Mesh Make(List<Vector3> v,List<int> t,List<Color> colors)
        {
            var mesh=new Mesh{name="Ruins_"+chapterId};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetColors(colors);
            var uv=new Vector2[v.Count];var style=new Vector2[v.Count];for(int i=0;i<v.Count;i++){uv[i]=new Vector2(.3125f,.5f);style[i]=new Vector2(2,0);}
            mesh.uv=uv;mesh.uv2=style;mesh.RecalculateNormals();return mesh;
        }
        GameObject Draw(string name,List<Vector3> v,List<int> t,List<Color> colors)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);go.GetComponent<MeshFilter>().sharedMesh=Make(v,t,colors);go.GetComponent<MeshRenderer>().sharedMaterial=surface;return go;
        }
        void Clear(GameObject go){if(!go)return;Dispose(go.GetComponent<MeshFilter>().sharedMesh);Dispose(go);}
        void Rebuild(int w,int h)
        {
            Clear(terrain);Clear(decor);foreach(var m in GetComponentsInChildren<RuinsMoon>())Clear(m.gameObject);
            // Flat supporting summit stays below y=0. Silhouette and strata change below it.
            const int n=64;float rx=w*.5f+2.2f,rz=h*.5f+2.2f,depth=Mathf.Clamp(w*.42f,2.5f,4);
            float[] ys={-.12f,-.35f,-.8f,-1.25f,-depth*.72f,-depth,-depth*1.2f};
            float[] scales=chapterId=="carved"?new[]{1f,1.12f,.88f,1.0f,.70f,.80f,.02f}:chapterId=="altar"?new[]{1f,1.08f,.86f,.94f,.70f,.80f,.02f}:new[]{1f,1.03f,1.01f,.93f,.8f,.55f,.02f};
            var v=new List<Vector3>{new Vector3(0,-.12f,0)};var t=new List<int>();var colors=new List<Color>{Tint(summitTint)};
            for(int ring=0;ring<ys.Length;ring++)for(int i=0;i<n;i++)
            {
                float a=i*Mathf.PI*2/n,c=Mathf.Cos(a),s=Mathf.Sin(a);
                bool angular=chapterId=="joinery"||chapterId=="crumble";
                float x=(angular?Mathf.Sign(c)*Mathf.Sqrt(Mathf.Abs(c)):c)*rx*scales[ring],z=(angular?Mathf.Sign(s)*Mathf.Sqrt(Mathf.Abs(s)):s)*rz*scales[ring];
                if(chapterId=="crumble"&&ring>0){x*=1+.15f*Mathf.Sin(i*3.7f);z*=1+.15f*Mathf.Cos(i*2.9f);}
                v.Add(new Vector3(x,ys[ring],z));
                colors.Add(Tint(ring==0?summitTint:Color.Lerp(rockTint,rimTint,ring%2==0?.15f:.65f)));
            }
            if(chapterId!="hollow")for(int i=0;i<n;i++)t.AddRange(new[]{0,1+(i+1)%n,1+i});
            else
            {
                // Two visible apertures in the outer crust. The central rectangle under all board cells is continuous.
                const int cells=48;
                for(int x=0;x<cells;x++)for(int z=0;z<cells;z++)
                {
                    float xx=-rx+x*rx*2/cells,zz=-rz+z*rz*2/cells,dx=rx*2/cells,dz=rz*2/cells;
                    float cx=xx+dx*.5f,cz=zz+dz*.5f;
                    if(cx*cx/(rx*rx)+cz*cz/(rz*rz)>.94f)continue;
                    if(Mathf.Abs(cx)>w*.5f+.45f&&Mathf.Abs(cx)<w*.5f+1.65f&&Mathf.Abs(cz)<.8f)continue;
                    int start=v.Count;v.AddRange(new[]{new Vector3(xx,-.12f,zz),new Vector3(xx+dx,-.12f,zz),new Vector3(xx,-.12f,zz+dz),new Vector3(xx+dx,-.12f,zz+dz)});
                    t.AddRange(new[]{start,start+2,start+1,start+1,start+2,start+3});for(int q=0;q<4;q++)colors.Add(Tint(summitTint));
                }
            }
            for(int j=0;j<ys.Length-1;j++)for(int i=0;i<n;i++)
            {
                // Large windows through the purple crust, while the summit continues supporting the board.
                if(chapterId=="hollow"&&j>0&&j<5&&i%16<10)continue;
                int a=1+j*n+i,b=1+j*n+(i+1)%n;t.AddRange(new[]{a,b,a+n,a+n,b,b+n});
            }
            terrain=Draw("PlanetHull_"+chapterId,v,t,colors);
            v=new List<Vector3>();t=new List<int>();colors=new List<Color>();
            // Every decorative vertex lies outside the board rectangle expanded by one cell.
            // Tall features only occupy lateral wings to preserve screen-space visibility as well.
            float side=w*.5f+1.75f;
            for(int sign=-1;sign<=1;sign+=2)for(int i=0;i<3;i++)
            {
                var p=new Vector3(sign*side,-.05f,(i-1)*h*.32f);
                if(chapterId=="frost")for(int k=0;k<3;k++)Crystal(p+new Vector3(sign*k*.3f,0,k*.24f),.24f,1.1f+k*.3f,moonTint,v,t,colors);
                else if(chapterId=="altar")Box(p+Vector3.up*.7f,new Vector3(.60f,1.6f,.52f),rimTint,v,t,colors);
                else if(chapterId=="joinery")
                {
                    Box(p,new Vector3(.8f,.28f,.28f),rockTint,v,t,colors);
                    Box(p+new Vector3(sign*.3f,.17f,0),new Vector3(.14f,.4f,.48f),rimTint,v,t,colors);
                }
                else if(chapterId=="carved")Box(p+Vector3.up*.22f,new Vector3(.45f,.55f,.5f),rimTint,v,t,colors);
                else if(chapterId=="crumble")Box(p+new Vector3(sign*.4f,-.8f-i*.3f,.2f),new Vector3(.6f,.65f,.5f),rockTint,v,t,colors);
                else if(chapterId=="moss")
                {
                    Box(p,new Vector3(.6f,.2f,.7f),rimTint,v,t,colors);
                    for(int k=0;k<4;k++)Crystal(p+new Vector3(k*.08f,0,0),.025f,.20f,rimTint,v,t,colors);
                }
            }
            // Timber construction replaces the rounded lower crust with layered joists.
            if(chapterId=="joinery")for(int i=-2;i<=2;i++)
            {
                // These structural beams are strictly underneath the supporting planet, not on board cells.
                // They belong to the hull mesh so the prop-clearance assertion applies only to exterior props.
                var hv=new List<Vector3>();var ht=new List<int>();var hc=new List<Color>();
                Box(new Vector3(0,-1.0f,i*rz*.36f),new Vector3(rx*2,.5f,.5f),rockTint,hv,ht,hc);
                var beam=Draw("UndersideJoist",hv,ht,hc);beam.AddComponent<RuinsMoon>();
            }
            decor=Draw("ExteriorProps_"+chapterId,v,t,colors);
            foreach(var point in v)if(Mathf.Abs(point.x)<w*.5f+1&&Mathf.Abs(point.z)<h*.5f+1)throw new System.InvalidOperationException("Decor entered one-cell board exclusion zone: "+chapterId);
            for(int i=0;i<2;i++)
            {
                var mv=new List<Vector3>();var mt=new List<int>();var mc=new List<Color>();Crystal(Vector3.zero,1,1.6f,moonTint,mv,mt,mc);
                var moon=Draw("DistantWorld_"+chapterId,mv,mt,mc);moon.AddComponent<RuinsMoon>();moon.transform.localPosition=new Vector3((i==0?-1:1)*(w*.5f+3.8f),1.0f,h*.5f+2.5f+i);moon.transform.localScale=Vector3.one*(i==0?.7f:.45f);
            }
        }
        void Box(Vector3 p,Vector3 size,Color color,List<Vector3> v,List<int> t,List<Color> colors)
        {
            int start=v.Count;foreach(var corner in new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,-1,1),new Vector3(-1,-1,1),new Vector3(-1,1,-1),new Vector3(1,1,-1),new Vector3(1,1,1),new Vector3(-1,1,1)}){v.Add(p+Vector3.Scale(corner,size)*.5f);colors.Add(Tint(color));}
            var faces=new[]{0,1,4,1,5,4,1,2,5,2,6,5,2,3,6,3,7,6,3,0,7,0,4,7,4,5,7,5,6,7,0,3,1,1,3,2};for(int i=0;i<faces.Length;i+=3)t.AddRange(new[]{start+faces[i],start+faces[i+2],start+faces[i+1]});
        }
        void Crystal(Vector3 p,float radius,float height,Color color,List<Vector3> v,List<int> t,List<Color> colors)
        {
            int k=v.Count;v.Add(p+Vector3.up*height);colors.Add(Tint(color));for(int i=0;i<6;i++){float a=i*Mathf.PI/3;v.Add(p+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));colors.Add(Tint(color)*(.75f+i*.035f));}for(int i=0;i<6;i++)t.AddRange(new[]{k,k+1+(i+1)%6,k+1+i});
        }
    }
    public sealed class RuinsMoon : MonoBehaviour {}
}
