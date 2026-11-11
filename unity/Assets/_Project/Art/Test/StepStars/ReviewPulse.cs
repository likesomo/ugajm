using UnityEngine;
namespace SevenSteps.Art.StepStars
{
 // Last empty star breathes at 0.8 Hz; this assembly never enters player builds.
 public sealed class ReviewPulse : MonoBehaviour
 {
  LineRenderer[] strokes;float[] widths;
  void Start(){strokes=GetComponentsInChildren<LineRenderer>();widths=new float[strokes.Length];for(int i=0;i<strokes.Length;i++)widths[i]=strokes[i].startWidth;}
  void Update(){float f=.65f+.5f*(1+Mathf.Sin(Time.unscaledTime*5));for(int i=0;i<strokes.Length;i++)strokes[i].startWidth=strokes[i].endWidth=widths[i]*f;}
 }
}
