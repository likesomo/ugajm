using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SevenSteps.Art
{
    /// <summary>Exterior modules are grounded against the actual fitted crust, never a presumed crust shape.</summary>
    public static class PlanetDressingPlacement
    {
        public static GameObject Build(Transform parent, GameObject terrain, PlanetDressingSet set, int w, int h)
        {
            if (!set || set.modules == null || set.modules.Length == 0) return null;
            var root = new GameObject("ThemedDressing_"+set.planetId);
            root.transform.SetParent(parent,false);
            // The combined crust renderer is immutable. A temporary collider supplies exact surface raycasts.
            // It is removed after fitting, so neither physics queries nor gameplay cells gain obstacles.
            var colliders = new System.Collections.Generic.List<MeshCollider>();
            foreach (var filter in terrain.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.sharedMesh) continue;
                var c=filter.gameObject.AddComponent<MeshCollider>();c.sharedMesh=filter.sharedMesh;colliders.Add(c);
            }
            try
            {
                for(int i=0;i<set.modules.Length;i++)
                {
                    if(!set.modules[i])continue;
                    // All five silhouettes live on the lateral wings.
                    // At pitch 50 / yaw 0, lateral placement cannot project over the board or the exit.
                    const float moduleScale=1.45f;
                    bool icicles=set.modules[i].name.Contains("IcicleFringe");
                    float x=(i%2==0?-1:1)*(w*.5f+1.70f);
                    float z=i%2==0?(i/2-1)*h*.30f:(i==1?-1:1)*h*.27f;
                    if(icicles)
                    {
                        var hull=terrain.GetComponent<MeshFilter>().sharedMesh.bounds;
                        x=i%2==0?hull.min.x+.07f:hull.max.x-.07f;
                    }
                    var rotation=Quaternion.Euler(0,icicles?90:(set.planetId=="carved"?180:0)+(i%2==0?-12:16),0);
                    float outwardRadius=0,moduleHeight=0;
                    foreach(var filter in set.modules[i].GetComponentsInChildren<MeshFilter>())
                        foreach(var vertex in filter.sharedMesh.vertices)
                        {
                            var local=set.modules[i].transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                            float vx=(rotation*(local*moduleScale)).x;
                            outwardRadius=Mathf.Max(outwardRadius,i%2==0?vx:-vx);
                            moduleHeight=Mathf.Max(moduleHeight,local.y*moduleScale);
                        }
                    var ray=new Ray(parent.TransformPoint(new Vector3(x,5,z)),parent.TransformDirection(Vector3.down));
                    float best=float.NegativeInfinity;
                    float score=float.NegativeInfinity;
                    float originalX=x,originalZ=z;
                    // Pick a supported shoulder visible from the actual ortho 50 / yaw 0 direction.
                    // This avoids placing small modules behind the cliff crest of an irregular crater mesh.
                    var toViewer=new Vector3(0,Mathf.Sin(50*Mathf.Deg2Rad),-Mathf.Cos(50*Mathf.Deg2Rad));
                    for(int step=0;step<=6;step++)for(int dz=-1;dz<=1;dz++)
                        {
                            float sx=Mathf.Sign(originalX)*Mathf.Max(w*.5f+outwardRadius+.28f,Mathf.Abs(originalX)-step*.10f);
                            float sz=originalZ+dz*.22f;
                            ray.origin=parent.TransformPoint(new Vector3(sx,5,sz));
                            foreach(var collider in colliders)if(collider.Raycast(ray,out var hit,12*parent.lossyScale.y))
                            {
                                var p=parent.InverseTransformPoint(hit.point);
                                var probe=p+Vector3.up*Mathf.Max(.08f,moduleHeight*.58f);
                                var sight=new Ray(parent.TransformPoint(probe+toViewer*20),parent.TransformDirection(-toViewer));
                                bool visible=true;
                                foreach(var blocker in colliders)if(blocker.Raycast(sight,out var blocked,19.98f*parent.lossyScale.y)){visible=false;break;}
                                float candidate=p.y+(visible?100:0)-Mathf.Abs(dz)*.03f;
                                if(icicles)candidate=100-step*.4f+(visible?.1f:0)-Mathf.Abs(dz)*.03f;
                                if(candidate>score){score=candidate;best=p.y;x=sx;z=sz;}
                            }
                        }
                    
                    if(float.IsNegativeInfinity(best))
                    {
                        Debug.LogWarning("[PlanetDressing] No crust beneath "+set.planetId+"/"+set.modules[i].name+"; module omitted");
                        continue;
                    }
                    var go=UnityEngine.Object.Instantiate(set.modules[i],root.transform);
                    go.name=set.modules[i].name;
                    go.transform.localPosition=new Vector3(x,best-.018f,z);
                    // The supporting frost bar overlaps the raycast rim; the teeth overhang the open edge.
                    if(icicles)go.transform.localPosition+=Vector3.right*Mathf.Sign(x)*.14f;
                    go.transform.localRotation=rotation;
                    go.transform.localScale=Vector3.one*moduleScale;
                    foreach(var renderer in go.GetComponentsInChildren<Renderer>())
                    {
                        var materials=renderer.sharedMaterials;
                        for(int m=0;m<materials.Length;m++)materials[m]=set.material;
                        renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                        // A hard world-space board exclusion plus a projected screen-space clearance check.
                        var filter=renderer.GetComponent<MeshFilter>();
                        if(filter)foreach(var vertex in filter.sharedMesh.vertices)
                        {
                            var point=parent.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                            if(Mathf.Abs(point.x)<w*.5f+.20f&&Mathf.Abs(point.z)<h*.5f+.20f)
                                throw new InvalidOperationException("Dressing entered board clearance: "+go.name);
                        }
                    }
                }
            }
            finally
            {
                foreach(var collider in colliders)
                {
                    collider.enabled=false;
                    if(Application.isPlaying)UnityEngine.Object.Destroy(collider);else UnityEngine.Object.DestroyImmediate(collider);
                }
            }
            return root;
        }
    }
}
