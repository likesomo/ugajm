using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace SevenSteps.Art {
 public sealed class RuinsHengeSky : MonoBehaviour {
  public Material space;
  public Color nebulaTint=new Color32(56,81,119,255), backgroundTint=new Color32(9,21,44,255), secondaryTint=new Color32(88,59,105,255);
  Color Tint(Color c)=>QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;
  Camera cam;Transform sky;
  static Color C(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var c);return QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;}
  void Start(){cam=GetComponentInParent<Camera>();if(!cam)cam=Camera.main;MakeSky();}
  void LateUpdate(){if(cam&&sky){sky.localPosition=new Vector3(0,0,90);float h=cam.orthographic?cam.orthographicSize:35;sky.localScale=new Vector3(h*cam.aspect,h,1);}}
  void OnDestroy(){if(sky){Destroy(sky.GetComponent<MeshFilter>().sharedMesh);Destroy(sky.gameObject);}}        Mesh Mesh(List<Vector3> v,List<int> t,List<Color> colors)
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
        void MakeSky()
        {
            if(!cam)return;var v=new List<Vector3>();var t=new List<int>();var colors=new List<Color>();const int nx=32,ny=20;
            for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++){float xx=x/(float)nx,yy=y/(float)ny;v.Add(new Vector3(xx*2-1,yy*2-1,0));float haze=Mathf.PerlinNoise(xx*4+17,yy*5+3)*Mathf.Exp(-Mathf.Pow((yy-.4f)-.30f*Mathf.Sin(xx*5),2)*12);colors.Add(Color.Lerp(Tint(backgroundTint),Color.Lerp((QualitySettings.activeColorSpace==ColorSpace.Linear?nebulaTint.linear:nebulaTint),Tint(secondaryTint),xx),haze*.95f));}
            for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){int k=y*(nx+1)+x;t.AddRange(new[]{k,k+nx+2,k+1,k,k+nx+1,k+nx+2});}
            var random=new System.Random(74);for(int i=0;i<125;i++){float x=(float)random.NextDouble()*2-1,y=(float)random.NextDouble()*2-1,s=.001f+(float)random.NextDouble()*.0022f;int k=v.Count;v.AddRange(new[]{new Vector3(x-s,y-s,-.01f),new Vector3(x+s,y-s,-.01f),new Vector3(x+s,y+s,-.01f),new Vector3(x-s,y+s,-.01f)});t.AddRange(new[]{k,k+2,k+1,k,k+3,k+2});for(int q=0;q<4;q++)colors.Add(C(i%3==0?"FFDDB2":"C0D9ED")*(i%12==0?2.1f:1.1f));}
            var go=Draw("NebulaAndStars",Mesh(v,t,colors),space,cam.transform);sky=go.transform;
            var r=go.GetComponent<MeshRenderer>();r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }

 }
}
