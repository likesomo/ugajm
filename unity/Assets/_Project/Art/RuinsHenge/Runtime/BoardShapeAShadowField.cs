using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenSteps.Art
{
    // Reuses the alpha of the two-sheet Blender bake. One active stage, no shadow draws.
    // Coordinates stay in IStageEnvironment.Fit local metres, including non-unit grids.
    public sealed class BoardShapeAShadowField : MonoBehaviour
    {
        internal readonly List<Vector4> rectangles = new List<Vector4>();
        internal readonly List<Vector4> profiles = new List<Vector4>();
        const int Capacity=64;
        readonly Vector4[] rects=new Vector4[Capacity], masks=new Vector4[Capacity];
        static BoardShapeAShadowField owner;
        internal void Add(Vector3 p,Vector3 scale,int profile,float opacity=.65f)
        {
            if(rectangles.Count>=Capacity-16)throw new InvalidOperationException("BoardShapeA static shadow capacity");
            rectangles.Add(new Vector4(p.x,p.z,Mathf.Abs(scale.x)*4,Mathf.Abs(scale.z)*4));
            profiles.Add(new Vector4(profile,opacity,p.y,0));
        }
        void LateUpdate()
        {
            owner=this;int n=rectangles.Count;
            for(int i=0;i<n;i++){rects[i]=rectangles[i];masks[i]=profiles[i];}
            foreach(var contact in BoardShapeAContact.Active)
            {
                if(!contact||!contact.gameObject.activeInHierarchy)continue;
                if(n==Capacity)break;
                Vector3 p=transform.InverseTransformPoint(contact.transform.position);
                float scale=contact.transform.lossyScale.x/transform.lossyScale.x;
                // Move right/down from the upper-left 40 degree key; opacity is exactly .52.
                rects[n]=new Vector4(p.x+.16f*scale,p.z-.12f*scale,contact.radius.x*scale,contact.radius.y*scale);
                masks[n++]=new Vector4(-1,.52f,p.y,0);
            }
            Shader.SetGlobalMatrix("_BSAWorldToLocal",transform.worldToLocalMatrix);
            Shader.SetGlobalInt("_BSAShadowCount",n);
            Shader.SetGlobalVectorArray("_BSAShadowRects",rects);
            Shader.SetGlobalVectorArray("_BSAShadowProfiles",masks);
        }
        void OnDisable(){if(owner==this){Shader.SetGlobalInt("_BSAShadowCount",0);owner=null;}}
    }
}
