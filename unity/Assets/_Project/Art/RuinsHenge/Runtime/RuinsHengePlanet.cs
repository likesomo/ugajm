using System.Collections.Generic;
using SevenSteps.Contracts;
using UnityEngine;
namespace SevenSteps.Art {
 public sealed class RuinsHengePlanet : MonoBehaviour, IStageEnvironment {
  public Material surface;
  GameObject terrain,decor;
  static Color C(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var c);return QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;}
  public void Fit(BoardGrid grid){transform.position=grid.Origin;transform.localScale=Vector3.one*grid.CellSize;Rebuild(grid.W,grid.H);}
  void OnDestroy(){foreach(var f in GetComponentsInChildren<MeshFilter>())if(f.sharedMesh)Destroy(f.sharedMesh);}        Mesh Mesh(List<Vector3> v,List<int> t,List<Color> colors)
        {
            var mesh=new Mesh{name="RuinsPreviewMesh"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetColors(colors);
            var uv=new Vector2[v.Count];var style=new Vector2[v.Count];for(int i=0;i<v.Count;i++){uv[i]=new Vector2(.3125f,.5f);style[i]=new Vector2(2,0);}
            mesh.uv=uv;mesh.uv2=style;mesh.RecalculateNormals();return mesh;
        }
        GameObject Draw(string name,Mesh mesh,Material material,Transform parent=null)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent?parent:transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;return go;
        }
        void Rebuild(int w,int h)
        {
            if(terrain){Destroy(terrain.GetComponent<MeshFilter>().sharedMesh);Destroy(terrain);}if(decor){Destroy(decor.GetComponent<MeshFilter>().sharedMesh);Destroy(decor);}

            const int n=48;float rx=w*.5f+.6f,rz=h*.5f+1.2f,centerZ=-.5f;
            var v=new List<Vector3>{new Vector3(0,-.12f,centerZ)};var t=new List<int>();var colors=new List<Color>{C("C6CAB6")};
            float depth=Mathf.Clamp(w*.28f,1.7f,3.4f);
            float[] ys={-.12f,-.3f,-.75f,-depth*.65f,-depth,-depth*1.2f};float[] scales={1,1.035f,1.02f,.9f,.56f,.01f};
            for(int ring=0;ring<ys.Length;ring++)for(int i=0;i<n;i++)
            {
                float a=i*Mathf.PI*2/n,c=Mathf.Cos(a),s=Mathf.Sin(a);
                float x=Mathf.Sign(c)*Mathf.Sqrt(Mathf.Abs(c))*rx*scales[ring],z=Mathf.Sign(s)*Mathf.Sqrt(Mathf.Abs(s))*rz*scales[ring];
                v.Add(new Vector3(x,ys[ring],z+centerZ));
                colors.Add(ring==0?C("748D57"):Color.Lerp(C("586B80"),C("A5AC9B"),(.5f+.5f*Mathf.Sin(i*2.71f+ring))* .65f));
            }
            for(int i=0;i<n;i++)t.AddRange(new[]{0,1+(i+1)%n,1+i});
            for(int j=0;j<ys.Length-1;j++)for(int i=0;i<n;i++){int a=1+j*n+i,b=1+j*n+(i+1)%n;t.AddRange(new[]{a,b,a+n,a+n,b,b+n});}
            terrain=Draw("StageSized_RockPlanet",Mesh(v,t,colors),surface);
            v=new List<Vector3>();t=new List<int>();colors=new List<Color>();
            for(int i=0;i<74;i++)
            {
                float a=i*Mathf.PI*2/74,c=Mathf.Cos(a),s=Mathf.Sin(a);
                var p=new Vector3(Mathf.Sign(c)*Mathf.Sqrt(Mathf.Abs(c))*rx*.99f,-.1f,centerZ+Mathf.Sign(s)*Mathf.Sqrt(Mathf.Abs(s))*rz*.99f);
                float ht=.10f+.07f*(i%3);int k=v.Count;
                v.AddRange(new[]{p+new Vector3(-.05f,0,0),p+new Vector3(.05f,0,0),p+new Vector3(.035f,ht,.02f)});t.AddRange(new[]{k,k+2,k+1});for(int q=0;q<3;q++)colors.Add(C(i%2==0?"879B58":"B2AF6C"));
                if(i%4==0)Rock(p+new Vector3(0,-.04f,0),v,t,colors,i);
            }
            decor=Draw("MossRim_GrassAndRock",Mesh(v,t,colors),surface);
            foreach(var moon in GetComponentsInChildren<RuinsMoon>()){Destroy(moon.GetComponent<MeshFilter>().sharedMesh);Destroy(moon.gameObject);}
            for(int i=0;i<3;i++)
            {
                var moon=Draw("DistantMoon"+i,Octahedron(i),surface);moon.AddComponent<RuinsMoon>();
                moon.transform.localPosition=new Vector3(i==0?-w*.5f-.1f:i==1?w*.5f+.05f:w*.2f,i==2?1.6f:.9f,h*.5f+1.2f+i*.25f);
                moon.transform.localScale=Vector3.one*(i==0?.65f:i==1?.43f:.20f);
            }
        }
        void Rock(Vector3 p,List<Vector3> v,List<int> t,List<Color> colors,int seed)
        {
            int k=v.Count;float sx=.14f+(seed%3)*.04f;
            Vector3[] b={new Vector3(-sx,-.05f,-.12f),new Vector3(sx,-.05f,-.12f),new Vector3(sx,-.05f,.12f),new Vector3(-sx,-.05f,.12f),new Vector3(-sx*.7f,.13f,-.10f),new Vector3(sx*.7f,.13f,-.10f),new Vector3(sx*.7f,.13f,.10f),new Vector3(-sx*.7f,.13f,.10f)};
            foreach(var x in b){v.Add(p+x);colors.Add(C(seed%2==0?"829096":"A0AA9C"));}
            foreach(int q in new[]{0,4,1,1,4,5,1,5,2,2,5,6,2,6,3,3,6,7,3,7,0,0,7,4,4,7,5,5,7,6})t.Add(k+q);
        }
        Mesh Octahedron(int variant)
        {
            var v=new List<Vector3>();var t=new List<int>();var colors=new List<Color>();
            Vector3[] p={Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};
            int[] faces={0,4,3,0,2,4,0,5,2,0,3,5,1,3,4,1,4,2,1,2,5,1,5,3};
            for(int i=0;i<faces.Length;i+=3){Vector3 a=p[faces[i]],b=p[faces[i+1]],c=p[faces[i+2]];Tri(a,(a+b).normalized,(a+c).normalized);Tri((a+b).normalized,b,(b+c).normalized);Tri((a+c).normalized,(b+c).normalized,c);Tri((a+b).normalized,(b+c).normalized,(a+c).normalized);}
            void Tri(Vector3 a,Vector3 b,Vector3 c){int k=v.Count;v.AddRange(new[]{a,b,c});t.AddRange(new[]{k,k+1,k+2});var color=C(variant==0?"7EACB2":variant==1?"B39A69":"9A8DB8")*(.8f+.2f*(k%7)/6f);colors.AddRange(new[]{color,color,color});}
            return Mesh(v,t,colors);
        }

 }
 public sealed class RuinsMoon : MonoBehaviour {} 
}