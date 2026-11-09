Shader "SevenSteps/RuinsSurface"
{
 Properties {
  _BaseMap("Atlas",2D)="white"{} _BaseColor("Tint",Color)=(1,1,1,1)
  _UseAtlas("Use Atlas",Float)=1 _UseMoss("Vertex R Moss",Float)=0
  _MossColor("Moss",Color)=(0.435,0.545,0.306,1)
  _WallColor("Cut Slate",Color)=(.33,.41,.49,1)
  _UseVertexColor("Vertex Color",Float)=0 _Unlit("Unlit",Float)=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass {
   Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_BaseColor,_MossColor,_WallColor;
   float _UseAtlas,_UseMoss,_UseVertexColor,_Unlit;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float2 style:TEXCOORD1;float4 color:COLOR;};
   struct Varyings {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float2 uv:TEXCOORD1;float4 color:COLOR;float3 positionWS:TEXCOORD2;float fog:TEXCOORD3;float2 style:TEXCOORD4;half3 fill:TEXCOORD5;};
   Varyings Vert(Attributes a){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.color=a.color;o.style=a.style;o.fill=VertexLighting(p.positionWS,o.normalWS);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 Frag(Varyings i):SV_Target {
    half3 color=lerp(half3(1,1,1),SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb,_UseAtlas)*_BaseColor.rgb;
    color*=lerp(half3(1,1,1),i.color.rgb,_UseVertexColor);
    color=lerp(color,_MossColor.rgb,saturate(i.color.r*_UseMoss)*(1-step(.5,i.style.x)));
    if(i.style.x>.5&&i.style.x<1.5)color=_WallColor.rgb*lerp(.72,1.28,saturate(i.style.y));
    if(i.style.x>1.5)color=i.color.rgb;
    Light l=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half3 n=normalize(i.normalWS);
    half3 lighting=SampleSH(n)+i.fill+l.color*saturate(dot(n,l.direction))*l.shadowAttenuation;
    color*=lerp(max(lighting,half3(.3,.3,.3)),half3(1,1,1),_Unlit);
    return half4(MixFog(color,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
