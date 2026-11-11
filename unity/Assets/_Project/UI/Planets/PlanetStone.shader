// Stage select v4b planet bodies and ground marks (runtime port of the editor-only review shader
// Art/Test/StageSelect/Editor/PlanetDepthV4b.shader, feat/art-stages dd6592c): vertex colour with a fixed warm key
// light, a cool ambient floor and a blue rim, independent of scene lights (the Title scene has none).
// _Unlit 1 = the plain vertex colour (ground marks). _BaseColor tints (unvisited planets dim through a property block).
Shader "SevenSteps/PlanetStone"
{
 Properties {
  _BaseColor("Tint",Color)=(1,1,1,1) _Unlit("Unlit",Float)=0 _SurfaceAlpha("Alpha",Float)=1
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;};
   struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;half4 color:COLOR;};
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;half _Unlit,_SurfaceAlpha;
   CBUFFER_END
   V Vert(A a){V o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.normalOS);o.color=a.color;return o;}
   half4 Frag(V i):SV_Target {
    half3 n=normalize(i.normal),v=normalize(GetWorldSpaceViewDir(i.world));
    half key=saturate(dot(n,normalize(half3(-.65,.8,-.5))));
    half rim=pow(1-saturate(dot(n,v)),3)*(.35+.65*saturate(dot(n,normalize(half3(.8,.1,.65)))));
    half3 stone=i.color.rgb*_BaseColor.rgb;
    half3 shaded=stone*(half3(.19,.23,.29)+half3(1.08,.91,.71)*key)+half3(.17,.30,.43)*rim*.48*_BaseColor.rgb;
    return half4(lerp(shaded,stone,_Unlit),_SurfaceAlpha);
   }
   ENDHLSL
  }
 }
}
