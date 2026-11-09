using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

namespace SevenSteps.Art
{
    /// <summary>Preview-only diagnostics; never changes game configuration or body rules.</summary>
    public sealed class RuinsHengeProbe : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2);
            var draw=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1);
            double start=Time.realtimeSinceStartupAsDouble;int frames=0;
            while(Time.realtimeSinceStartupAsDouble-start<5){frames++;yield return null;}
            double duration=Time.realtimeSinceStartupAsDouble-start;
            Debug.Log($"[RuinsHengeMetrics] fps={frames/duration:0.0} sampleSeconds={duration:0.00} frames={frames} memoryMiB={Profiler.GetTotalAllocatedMemoryLong()/1048576.0:0.0} drawCalls={(draw.Valid?draw.LastValue:0)} (0=counter unavailable) pipeline={UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name}");
            draw.Dispose();
            var args=Environment.GetCommandLineArgs();int k=Array.IndexOf(args,"-artperf");
            if(k>=0&&k+1<args.Length){ScreenCapture.CaptureScreenshot(args[k+1]);yield return new WaitForSecondsRealtime(.8f);Application.Quit();}
        }
    }
}
