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
                    float depth=.83f+i%3*.07f;
                    // Continuous supporting ledge, with broad openings between hanging columns below.
                    if(chapter=="hollow")depth=i%3==1?.30f:1.1f;
                    if(chapter=="frost")depth*=1.10f;
                    if(chapter=="crumble"){p.x+=(i%3-1)*.11f;p.z+=(i%2==0?.10f:-.10f);depth*=.75f+i%2*.25f;}
                    Add(kit.crust[(i+side)%3],p,new Vector3(length/count*1.20f,depth,1.4f+i%3*.10f),(side<2?0:90)+(i%3-1)*11.46f);
                    if((chapter=="carved"||chapter=="altar")&&i%2==0)
                        Add(kit.crust[(i+side+1)%3],p+Vector3.down*1.12f,new Vector3(length/count*1.22f,.27f,1.55f),(side<2?0:90));
                    if(chapter=="joinery"&&i%2==0)
                    {
                        var outward=side<2?new Vector3(0,0,side==0?-1:1):new Vector3(side==2?-1:1,0,0);
                        Add(kit.joist,p+Vector3.down*.95f+outward*.75f,new Vector3(length/count*1.25f,1.2f,1.2f),side<2?0:90);
                    }
                    if(chapter=="crumble"&&side==0&&i%2==0)
                        Add(kit.crust[i%3],p+new Vector3(.2f,-2.6f,-.30f),new Vector3(.30f,.13f,.32f),i*37);
                }
            }
            for(int sign=-1;sign<=1;sign+=2)
            {
                float side=w*.5f+1.60f;
                Add(kit.menhirs[sign<0?0:1],new Vector3(sign*side,-.12f,h*.16f),new Vector3(1.1f,1.1f,1.1f),sign*13,true);
                Add(kit.chest,new Vector3(sign*side,-.12f,-h*.20f),new Vector3(1.2f,1.15f,1.2f),sign*9,true);
                if(chapter=="altar")for(int z=-1;z<=1;z+=2)
                    Add(kit.menhirs[z<0?0:1],new Vector3(sign*(side+.05f),-.1f,z*h*.35f),new Vector3(1,1.15f,1),sign*z*17,true);
            }
            // Broken ledge stones occupy the same clearance zone as the other props.
            // They share this combined draw; frost deliberately has no vegetation geometry.
            for(int i=0;i<6;i++)
            {
                float x=(i-2.5f)*w/6;
                var p=new Vector3(x,-.09f,-h*.5f-1.46f);
                Add(kit.fragment,p,new Vector3(.8f,1,1),i*41,true);
                Add(kit.rubble,p+new Vector3(.15f,.10f,-.05f),Vector3.one*(.7f+i%3*.17f),i*53,true);
                if(chapter!="frost"&&chapter!="joinery"&&i%2==0)Add(kit.grass,p+new Vector3(-.16f,.08f,.02f),Vector3.one,i*31,true);
            }
            for(int sign=-1;sign<=1;sign+=2)for(int k=-1;k<=1;k+=2)
                Add(kit.rubble,new Vector3(sign*(w*.5f+1.42f),-.015f,k*h*.36f),new Vector3(1,1.4f,1),k*sign*29,true);
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
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return go;
        }
    }
}
