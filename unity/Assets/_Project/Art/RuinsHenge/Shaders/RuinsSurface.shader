Shader "SevenSteps/RuinsSurface"
{
 Properties {
  _BaseMap("Atlas",2D)="white"{} _BaseColor("Tint",Color)=(1,1,1,1)
  _UseAtlas("Use Atlas",Float)=1 _UseMoss("Vertex R Moss",Float)=0
  _MossColor("Moss",Color)=(0.435,0.545,0.306,1)
  _WallColor("Cut Slate",Color)=(.33,.41,.49,1)
  _AmbientFloor("Shadow Floor",Float)=.3
  _WeatherMap("Weather: stone / moss / wood",2D)="gray"{}
  _EnvironmentDetail("Environment detail",Range(0,1))=0
  _EnvironmentWood("Timber",Range(0,1))=0 _EnvironmentMoss("Patina",Range(0,1))=0
  _WeatherTint("Patina tint",Color)=(.24,.30,.21,1)
  _UseVertexColor("Vertex Color",Float)=0 _Unlit("Unlit",Float)=0
  _BoardShapeMap("BoardShapeA color + baked AO",2D)="white"{}
  _BoardShapeNormal("BoardShapeA baked normal",2D)="bump"{}
  _BoardRockTint("BoardShapeA rock tint",Color)=(1,1,1,1)
  _BoardMoss("BoardShapeA seam moss",Range(0,1))=1
  _BoardNormalStrength("BoardShapeA normal strength",Range(0,1))=.45
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
   TEXTURE2D(_WeatherMap); SAMPLER(sampler_WeatherMap);
   TEXTURE2D(_BoardShapeMap); SAMPLER(sampler_BoardShapeMap);
   TEXTURE2D(_BoardShapeNormal); SAMPLER(sampler_BoardShapeNormal);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_BaseColor,_MossColor,_WallColor,_WeatherTint;
   float4 _BoardRockTint;
   float _BoardMoss,_BoardNormalStrength;
   float _EnvironmentDetail,_EnvironmentWood,_EnvironmentMoss;
   float _UseAtlas,_UseMoss,_UseVertexColor,_Unlit,_AmbientFloor;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;float2 style:TEXCOORD1;float4 color:COLOR;};
   struct Varyings {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float2 uv:TEXCOORD1;float4 color:COLOR;float3 positionWS:TEXCOORD2;float fog:TEXCOORD3;float2 style:TEXCOORD4;half3 fill:TEXCOORD5;float4 tangentWS:TEXCOORD6;};
   Varyings Vert(Attributes a){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.tangentWS=float4(TransformObjectToWorldDir(a.tangentOS.xyz),a.tangentOS.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.color=a.color;o.style=a.style;o.fill=VertexLighting(p.positionWS,o.normalWS);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 Frag(Varyings i):SV_Target {
    half3 color;
    half3 n=normalize(i.normalWS);
    if(i.style.x>3.5)
    {
     float2 uv=i.uv;
     // Frost replaces the shared narrow moss swatch, including every floor seam.
     bool seam=uv.y>.5&&uv.x>.5&&uv.x<.75;
     if(seam&&_BoardMoss<.01)uv.x+=.25;
     half4 baked=SAMPLE_TEXTURE2D(_BoardShapeMap,sampler_BoardShapeMap,uv);
     color=baked.rgb*i.color.rgb;
     if(i.uv.y<.5&&i.uv.x>.25&&i.uv.x<.5){color*=_BoardRockTint.rgb;half patina=saturate((1-baked.a)*3)*saturate(n.y)*saturate(i.color.r-.8)*5*_BoardMoss*smoothstep(.19,.31,baked.r);color=lerp(color,half3(.18,.25,.12),patina*.35);}
     if(seam&&_BoardMoss>.01)color=lerp(half3(.34,.36,.30),color,_BoardMoss);
     half3 detail=UnpackNormalScale(SAMPLE_TEXTURE2D(_BoardShapeNormal,sampler_BoardShapeNormal,uv),_BoardNormalStrength);
     half3 tangent=normalize(i.tangentWS.xyz);
     n=normalize(tangent*detail.x+cross(n,tangent)*i.tangentWS.w*detail.y+n*detail.z);
    }
    else if(i.style.x>1.5&&i.style.x<2.5)
    {
     // Environment only. Board tiles keep their existing atlas and clear numeric surfaces.
     half4 weather=SAMPLE_TEXTURE2D(_WeatherMap,sampler_WeatherMap,i.positionWS.xz*.45);
     half grain=lerp(weather.r,weather.b,_EnvironmentWood);
     color=i.color.rgb*lerp(1,lerp(.52,1.20,grain),_EnvironmentDetail);
     half patina=smoothstep(.54,.77,weather.g)*_EnvironmentMoss*saturate(i.normalWS.y)*(1-_EnvironmentWood);
     color=lerp(color,_WeatherTint.rgb,patina);
    }
    else
    {
     color=lerp(half3(1,1,1),SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb,_UseAtlas)*_BaseColor.rgb;
     color*=lerp(half3(1,1,1),i.color.rgb,_UseVertexColor);
     color=lerp(color,_MossColor.rgb,saturate(i.color.r*_UseMoss)*(1-step(.5,i.style.x)));
     if(i.style.x>.5&&i.style.x<1.5){
      float noise=frac(sin(dot(floor(i.positionWS.xz*8),float2(12.9898,78.233)))*43758.5453);
      float edge=step(.38,max(abs(frac(i.positionWS.x+.5)-.5),abs(frac(i.positionWS.z+.5)-.5)));
      color=_WallColor.rgb*lerp(.68,1.28,saturate(i.style.y))*(.97+.06*noise);
      color=lerp(color,_MossColor.rgb,edge*step(.62,i.style.y)*smoothstep(.6,.9,noise)*.35);
     }
     if(i.style.x>2.5)color=lerp(color,half3(.70,.66,.56),step(.70,i.style.y)*.85);
    }
    Light l=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half3 lighting=SampleSH(n)+i.fill+l.color*saturate(dot(n,l.direction))*l.shadowAttenuation;
    // Neutral stone fill keeps baked contrast visible without realtime shadows or SSAO.
    if(i.style.x>3.5){lighting=half3(.22,.24,.27)+i.fill*.25+l.color*(.85*saturate(dot(n,l.direction)));color*=lerp(.72,1,saturate(n.y*1.5));}
    half floorValue=i.style.x>3.5?.16:_AmbientFloor;
    color*=lerp(max(lighting,floorValue.xxx),half3(1,1,1),_Unlit);
    if(i.style.x>3.5){half3 v=GetWorldSpaceNormalizeViewDir(i.positionWS);half spec=pow(saturate(dot(n,normalize(v+l.direction))),24);color+=l.color*spec*.065;}
    return half4(MixFog(color,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
