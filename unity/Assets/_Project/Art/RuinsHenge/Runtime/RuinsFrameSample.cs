using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace SevenSteps.Art
{
    // Opt-in diagnostic, identical in before/after players; normal gameplay pays no sampling cost.
    public sealed class RuinsFrameSample : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(10);
            var samples=new List<double>();double start=Time.realtimeSinceStartupAsDouble,last=start;
            while(Time.realtimeSinceStartupAsDouble-start<10)
            {
                yield return null;double now=Time.realtimeSinceStartupAsDouble;samples.Add((now-last)*1000);last=now;
            }
            samples.Sort();double elapsed=last-start;var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            // Count once after the timed interval; no traversal is added to ordinary gameplay or sampled frames.
            long sceneTris=0,environmentTris=0;
            foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>())
            {
                var renderer=filter.GetComponent<Renderer>();var mesh=filter.sharedMesh;
                if(!mesh||!renderer||!renderer.enabled||!filter.gameObject.activeInHierarchy)continue;
                for(int sm=0;sm<mesh.subMeshCount;sm++){long tris=mesh.GetIndexCount(sm)/3;sceneTris+=tris;if(filter.transform.IsChildOf(transform))environmentTris+=tris;}
            }
            Debug.Log("[ArtPerf] "+JsonUtility.ToJson(new Result{chapter=GetComponent<RuinsHengePlanet>().chapterId,frames=samples.Count,seconds=elapsed,fps=samples.Count/elapsed,meanMs=elapsed*1000/samples.Count,medianMs=samples[samples.Count/2],p95Ms=samples[(int)((samples.Count-1)*.95)],width=Screen.width,height=Screen.height,hdr=pipeline&&pipeline.supportsHDR,device=SystemInfo.graphicsDeviceName,sceneTris=sceneTris,environmentTris=environmentTris}));
        }
        [Serializable] sealed class Result {public string chapter,device;public int frames,width,height;public long sceneTris,environmentTris;public double seconds,fps,meanMs,medianMs,p95Ms;public bool hdr;}
    }
}
