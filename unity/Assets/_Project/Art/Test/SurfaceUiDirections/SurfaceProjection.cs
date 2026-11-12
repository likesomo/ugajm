using System;
using System.Linq;
using SevenSteps.Game.UI.Planets;
using UnityEngine;
using UnityEngine.Rendering;

namespace SevenSteps.Art.SurfaceUiReview
{
    // Adapted read-only from art-stages/Art/Test/StageSurfaceV7/StageSurfaceV7PlayCapture.ApplyProjection.
    // No importer or protected metadata is accessed. The existing PNG is read into a runtime texture.
    public static class SurfaceProjection
    {
        public static Texture2D Apply(PlanetBody body,Camera camera,string id)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,true,false);
            string file="C:/unityjam/shared-assets/seven-steps/Assets/Art/Map/PlanetsV7Surface/"+id+".png";
            if(!ImageConversion.LoadImage(texture,System.IO.File.ReadAllBytes(file)))throw new Exception("Missing S2 art "+file);
            texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;texture.anisoLevel=4;texture.Apply(true,false);
            body.transform.localRotation=Quaternion.Euler(0,-8,0);
            var filter=body.GetComponent<MeshFilter>();var source=filter.sharedMesh;var mesh=UnityEngine.Object.Instantiate(source);
            mesh.name="S2 projected crust review "+id;mesh.triangles=source.triangles.Take(Math.Min(5120*3,source.triangles.Length)).ToArray();
            var vertices=mesh.vertices;var uv=new Vector2[vertices.Length];float diameter=body.Radius*2*PlanetBody.SpriteFill;
            for(int i=0;i<vertices.Length;i++)
            {
                var p=body.transform.TransformPoint(vertices[i])-body.transform.position;
                uv[i]=new Vector2(.5f+Vector3.Dot(p,camera.transform.right)/diameter,.5f+Vector3.Dot(p,camera.transform.up)/diameter);
            }
            mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();filter.sharedMesh=mesh;
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="S2 projected V7 "+id,renderQueue=(int)RenderQueue.Transparent};
            material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);material.SetFloat("_Surface",1);
            material.SetFloat("_Blend",0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",1);material.SetFloat("_Cull",(float)CullMode.Back);material.SetFloat("_Smoothness",.35f);
            material.SetOverrideTag("RenderType","Transparent");material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHATEST_ON");
            body.Renderer.sharedMaterial=material;
            var light=new GameObject("S2 cool rim",typeof(Light));light.transform.SetParent(body.transform.parent,false);
            light.transform.rotation=Quaternion.Euler(20,150,0);var l=light.GetComponent<Light>();l.type=LightType.Directional;l.color=new Color(.55f,.72f,1);l.intensity=.45f;l.shadows=LightShadows.None;
            return texture;
        }
    }
}
