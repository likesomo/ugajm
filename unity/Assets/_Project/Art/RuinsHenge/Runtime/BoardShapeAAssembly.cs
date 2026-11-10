using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SevenSteps.Art
{
    // Builds one opaque renderer, once per Fit. Sources are Blender FBXs, not runtime primitives.
    public static class BoardShapeAAssembly
    {
        public static GameObject Build(Transform parent, BoardShapeAKit kit, Material material, string chapter, int w, int h)
        {
            if (w <= 0 || h <= 0 || !kit || !material || kit.crust == null || kit.crust.Length != 3)
                throw new InvalidOperationException("BoardShapeA requires positive bounds and a complete imported kit");
            var parts = new List<CombineInstance>();
            var staticShadows=new List<(Vector3 p,Vector3 scale,int profile)>();
            float Noise(int seed){uint n=unchecked((uint)seed*2654435761u);n^=n>>15;return (n&65535)/65535f;}
            void Add(Mesh mesh, Vector3 p, Vector3 scale, float yaw = 0, bool decoration = false)
            {
                if (!mesh || !mesh.isReadable) throw new InvalidOperationException("Missing/read-protected BoardShapeA source mesh");
                var matrix = Matrix4x4.TRS(p, Quaternion.Euler(0,yaw,0), scale);
                if (decoration)
                    foreach (var vertex in mesh.vertices)
                    {
                        var point=matrix.MultiplyPoint3x4(vertex);
                        if (Mathf.Abs(point.x) < w*.5f+1 && Mathf.Abs(point.z) < h*.5f+1)
                            throw new InvalidOperationException("BoardShapeA decoration inside one-cell clearance: "+mesh.name);
                    }
                parts.Add(new CombineInstance {mesh=mesh,transform=matrix});
                // Bake receivers use a shared profile, so props retain one combined draw.
                if(mesh==kit.menhirs[0]||mesh==kit.menhirs[1])staticShadows.Add((p,scale,1));
                else if(mesh==kit.chest)staticShadows.Add((p,scale,2));
                else if(mesh==kit.wall)staticShadows.Add((p,scale,3));
                else if(mesh==kit.backGate)staticShadows.Add((p,scale,4));
                else if(mesh==kit.rubble&&scale.y>1.8f)staticShadows.Add((p,scale,5));
            }
            // 42% of interior seam intersections get actual raised cushions. The clean
            // tile centre remains untouched; frost contains no vegetation geometry.
            if(chapter!="frost")
            {
                float amount=chapter=="moss"?.42f:chapter=="altar"?.36f:chapter=="joinery"?.12f:.22f;
                for(int z=0;z<h-1;z++)for(int x=0;x<w-1;x++)if(Noise(173+x*17+z*53)<amount)
                    Add(kit.mossPatch,new Vector3(x-(w-2)*.5f,-.018f,z-(h-2)*.5f),new Vector3(.30f,.55f,.30f),(x*37+z*19)%360);
                for(int sign=-1;sign<=1;sign+=2)
                {
                    for(int x=0;x<w;x++)if(Noise(x*71+sign*13)<.65f)
                        Add(kit.mossPatch,new Vector3(x-(w-1)*.5f,-.16f,sign*(h*.5f+.48f)),new Vector3(.75f,.55f,.40f),x*17);
                    for(int z=0;z<h;z++)if(Noise(z*59+sign*23)<.65f)
                        Add(kit.mossPatch,new Vector3(sign*(w*.5f+.48f),-.16f,z-(h-1)*.5f),new Vector3(.40f,.55f,.75f),z*13);
                }
            }
            for (int sign=-1;sign<=1;sign+=2)
            {
                for(int x=0;x<w;x++)Add(kit.kerb,new Vector3(x-(w-1)*.5f,0,sign*(h*.5f+.22f)),Vector3.one,sign<0?0:180);
                for(int z=0;z<h;z++)Add(kit.kerb,new Vector3(sign*(w*.5f+.22f),0,z-(h-1)*.5f),Vector3.one,sign*90);
                for(int x=-1;x<=1;x+=2)Add(kit.corner,new Vector3(x*(w*.5f+.22f),0,sign*(h*.5f+.22f)),Vector3.one);
            }
            Add(kit.medallion,new Vector3(0,.12f,-h*.5f-.46f),new Vector3(1.6f,1.6f,1));
            for(int side=0;side<4;side++)
            {
                float length=(side<2?w:h)+2.2f;int count=Mathf.CeilToInt(length/1.65f);
                for(int i=0;i<count;i++)
                {
                    float a=(i-(count-1)*.5f)*length/count;
                    var p=side<2?new Vector3(a,0,(side==0?-1:1)*(h*.5f+.72f)):new Vector3((side==2?-1:1)*(w*.5f+.72f),0,a);
                    int seed=side*97+i*31;float depth=.66f+Noise(seed)*.39f;
                    if(chapter=="carved")depth=.62f;
                    if(chapter=="altar")depth=.65f;
                    if(chapter=="frost")depth=.85f+Noise(seed+11)*.30f;
                    if(chapter=="crumble"){p.x+=(Noise(seed+4)-.5f)*.42f;p.z+=(Noise(seed+7)-.5f)*.35f;depth=.44f+Noise(seed)*.40f;}
                    bool cave=chapter=="hollow"&&side==0&&(i==1||i==count-2);
                    if(cave)Add(kit.hollowArch,p,new Vector3(length/count*.94f,1,1),0);
                    else Add(kit.crust[(i+side)%3],p+Vector3.down*(Noise(seed+9)*.10f),new Vector3(length/count*(1.27f+Noise(seed+2)*.12f),depth,1.40f+Noise(seed+3)*.18f),(side<2?0:90)+(Noise(seed+5)-.5f)*20);
                    if(chapter=="carved")for(int tier=0;tier<3;tier++)
                    {
                        var outward=side<2?new Vector3(0,0,side==0?-1:1):new Vector3(side==2?-1:1,0,0);
                        Add(kit.terrace,p+Vector3.down*(.56f+tier*.47f)+outward*(.18f+tier*.21f),new Vector3(length/count*1.10f,1,1.15f),side<2?(side==0?0:180):(side==2?-90:90));
                    }
                    if(chapter=="joinery"&&i%2==0)
                    {
                        var outward=side<2?new Vector3(0,0,side==0?-1:1):new Vector3(side==2?-1:1,0,0);
                        Add(kit.timberFrame,p+Vector3.down*.65f+outward*.82f,new Vector3(length/count*.86f,1,1.3f),side<2?0:90);
                    }
                    if(chapter=="crumble"&&side==0&&i%2==0)
                        Add(kit.crust[i%3],new Vector3(a+Noise(seed+20)*.40f,-.80f-Noise(seed+22)*.35f,-h*.5f-2.02f),new Vector3(.45f+Noise(seed)*.2f,.22f,.48f),i*37);
                    if(chapter!="frost"&&Noise(seed+41)<(chapter=="moss"?.8f:.38f))
                        Add(kit.mossPatch,p+new Vector3((Noise(seed+43)-.5f)*.4f,-.16f,(Noise(seed+47)-.5f)*.5f),new Vector3(.70f,.5f,.60f),seed*13);
                }
            }
            for(int sign=-1;sign<=1;sign+=2)
            {
                float side=w*.5f+1.60f;
                if(chapter=="moss")Add(kit.menhirs[sign<0?0:1],new Vector3(sign*side,-.12f,h*.16f),new Vector3(1.1f,1.1f,1.1f),sign*13,true);
                Add(kit.chest,new Vector3(sign*side,-.12f,-h*.20f),new Vector3(1.2f,1.15f,1.2f),sign*9,true);
                if(chapter=="altar")for(int k=0;k<3;k++)
                    Add(kit.menhirs[k%2],new Vector3(sign*(side+.05f),-.1f,(k-1)*h*.33f),new Vector3(1.2f,1.3f+k%2*.25f,1.15f),sign*k*17,true);
                if(chapter=="frost")for(int k=0;k<3;k++)
                    Add(kit.frostSpire,new Vector3(sign*(side+.04f),-.12f,(k-1)*h*.31f),new Vector3(1.15f,1.1f+k%2*.35f,1.1f),sign*k*21,true);
                if(chapter=="crumble")Add(kit.crust[sign<0?1:2],new Vector3(sign*(side+.55f),-.95f,h*.22f),new Vector3(.52f,.31f,.55f),sign*27);
            }
            // Broken ledge stones occupy the same clearance zone as the other props.
            // They share this combined draw; frost deliberately has no vegetation geometry.
            for(int i=0;i<5;i++)
            {
                float x=(i-2.1f)*w/5+(Noise(i*19+6)-.5f)*.40f;
                var p=new Vector3(x,-.10f,-h*.5f-1.50f);
                Add(kit.fragment,p+Vector3.up*(Noise(i+7)*.14f),new Vector3(.9f,1,1),i*41,true);
                Add(kit.rubble,p+new Vector3(.15f,.09f,-.02f),new Vector3(1.3f+Noise(i+2)*.5f,1.0f+Noise(i+9)*1.4f,1.1f),i*53,true);
                if(chapter=="moss"||chapter=="altar")
                {
                    Add(kit.mossPatch,p+new Vector3(-.16f,.09f,.07f),new Vector3(.65f,1,.65f),i*29,true);
                    Add(kit.grass,p+new Vector3(-.16f,.14f,.02f),new Vector3(1.3f,1.4f,1.3f),i*31,true);
                }
            }
            for(int sign=-1;sign<=1;sign+=2)for(int k=-1;k<=1;k++)
            {
                var p=new Vector3(sign*(w*.5f+1.60f),-.02f,k*h*.36f);
                Add(kit.rubble,p,new Vector3(2.0f,1.9f+Noise(k+7)*1.2f,1.7f),k*sign*29,true);
                Add(kit.rubble,p+new Vector3(sign*.12f,.22f,.17f),new Vector3(1.1f,1.4f,1),k*sign*51,true);
                if(chapter=="moss")Add(kit.mossPatch,p+new Vector3(-sign*.10f,.24f,.09f),new Vector3(.7f,1,.7f),k*37,true);
            }
            float back=h*.5f+1.4f;
            Add(kit.backGate,new Vector3(0,-.12f,back),new Vector3(1.25f,1.1f,1.2f),0,true);
            int wallCount=Mathf.Max(1,(w-3)/2);
            for(int sign=-1;sign<=1;sign+=2)for(int i=0;i<wallCount;i++)
                Add(kit.wall,new Vector3(sign*(1.70f+i),-.12f,back),new Vector3(1,1.05f+i%2*.16f,1),sign*2,true);
            var mesh=new Mesh{name="BoardShapeA_"+chapter+"_"+w+"x"+h,indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(parts.ToArray(),true,true,false);
            if(mesh.triangles.Length/3>25000)throw new InvalidOperationException("BoardShapeA environment exceeds 25k tris");
            mesh.RecalculateBounds();mesh.UploadMeshData(true);
            var go=new GameObject("BoardShapeA_Combined",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var shadows=go.AddComponent<BoardShapeAShadowField>();
            foreach(var shadow in staticShadows)shadows.Add(shadow.p,shadow.scale,shadow.profile);
            // A continuous one-metre kerb profile wraps the two edges facing the sun.
            for(int x=0;x<w;x++)shadows.Add(new Vector3(x-(w-1)*.5f,0,h*.5f+.22f),Vector3.one,0);
            for(int z=0;z<h;z++)shadows.Add(new Vector3(-w*.5f-.22f,0,z-(h-1)*.5f),Vector3.one,6);
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return go;
        }
    }
}
