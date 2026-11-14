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
                if(set.layout){PlaceLayout(parent,root,colliders,set,w,h);return root;}
                int[][] scatter={new[]{0,1,4,3,0,1,3,0},new[]{0,1,2,3,4,0,2,1},new[]{0,1,2,3,4,0,1,2},new[]{0,1,3,0,1,3,0,2},new[]{0,4,2,4,0,2,4,1},new[]{0,1,3,1,4,3,1,3},new[]{1,0,2,3,1,0,2,4}};
                string[] planets={"moss","hollow","carved","frost","joinery","crumble","altar"};
                int planetIndex=Array.IndexOf(planets,set.planetId);if(planetIndex<0)planetIndex=0;
                int hero=planetIndex==3?0:planetIndex==5||planetIndex==6?1:0;
                for(int i=0;i<set.modules.Length+8;i++)
                {
                    bool extra=i>=set.modules.Length;int moduleIndex=extra?scatter[planetIndex][i-set.modules.Length]:i;
                    var module=set.modules[moduleIndex];if(!module)continue;
                    // The hero, five source silhouettes and eight scatter instances occupy lateral crater shoulders.
                    // At pitch 50 / yaw 0, lateral placement cannot project over the board or the exit.
                    float moduleScale=extra?.70f+(i%3)*.14f:i==hero?2.05f:1.45f;
                    bool icicles=module.name.Contains("IcicleFringe");
                    float x=(i%2==0?-1:1)*(w*.5f+(extra?1.25f+(i%3)*.45f:1.70f));
                    float z=extra?((i-set.modules.Length)/2-1.5f)*(h*.30f+.22f):i%2==0?(i/2-1)*h*.30f:(i==1?-1:1)*h*.27f;
                    if(icicles)
                    {
                        var hull=terrain.GetComponent<MeshFilter>().sharedMesh.bounds;
                        x=i%2==0?hull.min.x+.07f:hull.max.x-.07f;
                    }
                    var rotation=Quaternion.Euler(0,icicles?90:(set.planetId=="carved"?180:0)+(i%2==0?-12:16)+(extra?(i%3-1)*23:0),0);
                    float outwardRadius=0,moduleHeight=0;
                    foreach(var filter in module.GetComponentsInChildren<MeshFilter>())
                        foreach(var vertex in filter.sharedMesh.vertices)
                        {
                            var local=module.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
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
                        Debug.LogWarning("[PlanetDressing] No crust beneath "+set.planetId+"/"+module.name+"; module omitted");
                        continue;
                    }
                    var go=UnityEngine.Object.Instantiate(module,root.transform);
                    go.name=module.name+(extra?"_Scatter_"+i:i==hero?"_Hero":"");
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

        [Serializable] sealed class LayoutFile { public LayoutProp[] props; }
        [Serializable] sealed class LayoutProp { public string kind, name, mesh; public float[] position, rotationEulerDegrees, scale; }

        /// The props of <see cref="PlanetDressingSet.layout"/> (altar A, 2026-11-14): each at its fitted crust-local X/Z
        /// (CraterCrust.FitAxis per board size), set on the crust by a downward ray, its model centred and its bottom sunk
        /// a little into the ground. A prop that would enter the board clearance on a small board is left out.
        static void PlaceLayout(Transform parent, GameObject root, System.Collections.Generic.List<MeshCollider> colliders, PlanetDressingSet set, int w, int h)
        {
            var file = JsonUtility.FromJson<LayoutFile>(set.layout.text.TrimStart('﻿'));
            if (file == null || file.props == null) throw new InvalidOperationException("[PlanetDressing] layout has no props: " + set.layout.name);
            GameObject Find(string mesh)
            {
                string key = System.IO.Path.GetFileNameWithoutExtension(mesh ?? "");
                foreach (var m in set.layoutModels ?? new GameObject[0]) if (m && m.name == key) return m;
                foreach (var m in set.modules ?? new GameObject[0]) if (m && m.name == key) return m;
                return null;
            }
            int placed = 0, skipped = 0;
            foreach (var p in file.props)
            {
                var model = Find(p.mesh);
                if (model == null || p.position == null || p.position.Length < 3) { Debug.LogWarning("[PlanetDressing] layout prop without a model: " + p.name + " (" + p.mesh + ")"); skipped++; continue; }
                float x = CraterCrust.FitAxis(p.position[0], w), z = CraterCrust.FitAxis(p.position[2], h);
                var ray = new Ray(parent.TransformPoint(new Vector3(x, 6, z)), parent.TransformDirection(Vector3.down));
                float ground = float.NegativeInfinity;
                foreach (var c in colliders) if (c.Raycast(ray, out var hit, 14 * parent.lossyScale.y)) ground = Mathf.Max(ground, parent.InverseTransformPoint(hit.point).y);
                if (float.IsNegativeInfinity(ground)) { skipped++; continue; }
                var go = UnityEngine.Object.Instantiate(model, root.transform);
                go.name = p.name;
                go.transform.localPosition = Vector3.zero;
                var e = p.rotationEulerDegrees != null && p.rotationEulerDegrees.Length >= 3 ? new Vector3(p.rotationEulerDegrees[0], p.rotationEulerDegrees[1], p.rotationEulerDegrees[2]) : Vector3.zero;
                go.transform.localRotation = Quaternion.Euler(e) * model.transform.localRotation;
                var s = p.scale != null && p.scale.Length >= 3 ? new Vector3(p.scale[0], p.scale[1], p.scale[2]) : Vector3.one;
                go.transform.localScale = Vector3.Scale(model.transform.localScale, s);
                // Centre the model on (x, z) and stand its bottom on the ground, slightly sunk.
                Bounds b = default; bool any = false;
                foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                if (!any) { UnityEngine.Object.Destroy(go); skipped++; continue; }
                var centre = parent.InverseTransformPoint(b.center);
                var bottom = parent.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z)).y;
                float sink = p.kind == "standingStone" ? .12f : .03f;
                go.transform.localPosition = new Vector3(x - centre.x, ground - bottom - sink, z - centre.z);
                bool clear = true;
                foreach (var renderer in go.GetComponentsInChildren<Renderer>())
                {
                    var mat = p.kind == "standingStone" && set.stoneMaterial ? set.stoneMaterial : set.material;
                    var materials = renderer.sharedMaterials;
                    for (int m = 0; m < materials.Length; m++) materials[m] = mat;
                    renderer.sharedMaterials = materials; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter && filter.sharedMesh) foreach (var vertex in filter.sharedMesh.vertices)
                    {
                        var point = parent.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                        if (Mathf.Abs(point.x) < w * .5f + .20f && Mathf.Abs(point.z) < h * .5f + .20f) { clear = false; break; }
                    }
                }
                if (!clear)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(go); else UnityEngine.Object.DestroyImmediate(go);
                    skipped++;
                    continue;
                }
                placed++;
            }
            Debug.Log($"[PlanetDressing] {set.planetId} layout {w}x{h}: {placed} placed, {skipped} left out");
        }
    }
}
