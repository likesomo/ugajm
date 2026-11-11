using UnityEngine;
namespace SevenSteps.Art {
    // The FBX is authored for a 5x5 board. Preserve its two-metre apron when fitting other boards.
    public static class CraterCrust {
        static float FitAxis(float value,int cells) => Mathf.Abs(value)<=2.5f?value*cells/5f:Mathf.Sign(value)*(cells*.5f+Mathf.Abs(value)-2.5f);
        public static Mesh Fit(Mesh source,int w,int h,Matrix4x4 sourceTransform) {
            if(!source||!source.isReadable||source.triangles.Length/3<20000)throw new System.InvalidOperationException("Crater crust requires a readable 20k+ triangle FBX");
            var mesh=Object.Instantiate(source);mesh.name="CraterCrust_Fitted";var v=mesh.vertices;var uv=new Vector2[v.Length];var normals=mesh.normals;
            if(sourceTransform==default)sourceTransform=Matrix4x4.identity;
            var normalTransform=sourceTransform.inverse.transpose;
            for(int i=0;i<v.Length;i++){v[i]=sourceTransform.MultiplyPoint3x4(v[i]);normals[i]=normalTransform.MultiplyVector(normals[i]);if(Mathf.Abs(v[i].x)<=2.5f)normals[i].x*=5f/w;if(Mathf.Abs(v[i].z)<=2.5f)normals[i].z*=5f/h;normals[i].Normalize();uv[i]=new Vector2((.05f+.90f*(v[i].x/10.6f+.5f))/4f,(.05f+.90f*(v[i].z/10.6f+.5f))/4f);v[i].x=FitAxis(v[i].x,w);v[i].z=FitAxis(v[i].z,h);}
            // Keep the stone atlas coordinates continuous across the centre and crater rims.
            mesh.vertices=v;mesh.uv=uv;mesh.normals=normals;mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        // Highest intersection of the actual triangular surface. Used only while constructing the combined environment.
        public static float Height(Vector3[] vertices,int[] indices,float x,float z) {
            float highest=-10;
            for(int i=0;i<indices.Length;i+=3){var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(det)<.000001f)continue;
                float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/det;
                float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/det;
                if(u>=0&&v>=0&&u+v<=1)highest=Mathf.Max(highest,u*a.y+v*b.y+(1-u-v)*c.y);
            }
            return highest>-9?highest:-.17f;
        }
    }
}
