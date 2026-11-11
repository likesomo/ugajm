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
  _BoardStoneSlot("Stone recipe (0 granite, 8 basalt, 9 sandstone, 10 marble, 11 slate, 12 limestone, 13 sarsen)",Float)=0
  _SurfaceAlpha("Surface opacity (independent of packed shadow alpha)",Range(0,1))=1
  _PlanetColor("Planet surface color",2D)="white"{}
  _PlanetDetail("Planet RG normal B roughness A cavity",2D)="gray"{}
  _PlanetDressing("Planet material identity",Range(0,1))=0
  _PlanetIce("Cracked glossy ice",Range(0,1))=0
  _SlideIce("Gameplay sliding ice",Range(0,1))=0
  _PlanetPigment("Stone pigment",Color)=(.58,.61,.55,1)
  [HideInInspector] _SrcBlend("Source blend",Float)=1
  [HideInInspector] _DstBlend("Destination blend",Float)=0
  [HideInInspector] _ZWrite("Depth write",Float)=1
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass {
   Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
   Blend [_SrcBlend] [_DstBlend]
   ZWrite [_ZWrite]
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
   TEXTURE2D(_PlanetColor); SAMPLER(sampler_PlanetColor);
   TEXTURE2D(_PlanetDetail); SAMPLER(sampler_PlanetDetail);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_BaseColor,_MossColor,_WallColor,_WeatherTint;
   float4 _BoardRockTint;
   float _BoardMoss,_BoardNormalStrength,_SurfaceAlpha,_BoardStoneSlot;
   float _EnvironmentDetail,_EnvironmentWood,_EnvironmentMoss;
   float _UseAtlas,_UseMoss,_UseVertexColor,_Unlit,_AmbientFloor;
   float _PlanetDressing,_PlanetIce;
   float _SlideIce;
   float4 _PlanetPigment;
   CBUFFER_END
   float _DressingGlossReview;
   float4x4 _BSAWorldToLocal;
   int _BSAShadowCount;
   float4 _BSAShadowRects[64],_BSAShadowProfiles[64];
   half BoardShadow(float3 world)
   {
    float3 p=mul(_BSAWorldToLocal,float4(world,1)).xyz;
    half shade=0;
    [loop]for(int k=0;k<_BSAShadowCount;k++)
    {
     float4 r=_BSAShadowRects[k],profile=_BSAShadowProfiles[k];
     if(p.y>profile.z+.065||p.y<profile.z-.40)continue;
     float2 q=(p.xz-r.xy)/max(r.zw,float2(.01,.01));
     if(profile.x<0)
     {
      // Soft actor/gimmick blob, stretched diagonally away from the key.
      q=float2(q.x+q.y*.22,q.y);
      shade=max(shade,(1-smoothstep(.34,1,dot(q,q)))*profile.y);
     }
     else if(max(abs(q.x),abs(q.y))<.5)
     {
      float slot=profile.x;
      float2 uv=(float2(fmod(slot,4),floor(slot/4))+q+.5)/4;
      shade=max(shade,SAMPLE_TEXTURE2D(_BoardShapeMap,sampler_BoardShapeMap,uv).a*profile.y);
     }
    }
    return 1-shade;
   }
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;float2 style:TEXCOORD1;float4 color:COLOR;};
   struct Varyings {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float2 uv:TEXCOORD1;float4 color:COLOR;float3 positionWS:TEXCOORD2;float fog:TEXCOORD3;float2 style:TEXCOORD4;half3 fill:TEXCOORD5;float4 tangentWS:TEXCOORD6;};
   Varyings Vert(Attributes a){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.tangentWS=float4(TransformObjectToWorldDir(a.tangentOS.xyz),a.tangentOS.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.color=a.color;o.style=a.style;if(a.style.x>3.5){float3 origin=TransformObjectToWorld(float3(0,0,0));o.style.y=frac(a.style.y+frac(sin(dot(origin.xz,float2(12.9898,78.233)))*43758.5453));}o.fill=VertexLighting(p.positionWS,o.normalWS);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 Frag(Varyings i):SV_Target {
    half3 color;
    half3 n=normalize(i.normalWS);
    half stoneRoughness=.72;
    bool frostSnow=_PlanetIce>.5&&i.style.x>6.5&&i.style.x<7.5;
    if(i.style.x>8.5)
    {
     // Blender modules carry literal per-corner pigment (stone, foliage, timber, brass, ice).
     // All modules still share the chapter's one material and its detail normal.
     half3 weights=pow(abs(i.normalWS),4);weights/=max(dot(weights,half3(1,1,1)),.001);
     float2 px=i.positionWS.zy*.72,py=i.positionWS.xz*.72,pz=i.positionWS.xy*.72;
     half4 pigment=SAMPLE_TEXTURE2D(_PlanetColor,sampler_PlanetColor,px)*weights.x+SAMPLE_TEXTURE2D(_PlanetColor,sampler_PlanetColor,py)*weights.y+SAMPLE_TEXTURE2D(_PlanetColor,sampler_PlanetColor,pz)*weights.z;
     half4 detail=SAMPLE_TEXTURE2D(_PlanetDetail,sampler_PlanetDetail,px)*weights.x+SAMPLE_TEXTURE2D(_PlanetDetail,sampler_PlanetDetail,py)*weights.y+SAMPLE_TEXTURE2D(_PlanetDetail,sampler_PlanetDetail,pz)*weights.z;
     // Unity linearizes material Color properties; FBX vertex pigment remains the authored attribute.
     half3 authored=LinearToSRGB(_PlanetPigment.rgb);
     half pigmentDistance=min(length(i.color.rgb-_PlanetPigment.rgb),length(i.color.rgb-authored));
     half match=1-smoothstep(.04,.23,pigmentDistance);
     color=lerp(i.color.rgb,pigment.rgb,match)*lerp(.85,1.05,detail.a);
     if(_PlanetIce>.5)color=lerp(color,color*lerp(half3(.75,.92,1),half3(1.12,1.08,1.02),pigment.r),.55);
     stoneRoughness=lerp(.78,.14,_PlanetIce);
    }
    else if(i.style.x>3.5)
    {
     float2 uv=i.uv;
     float sourceSlot=floor(uv.x*4)+floor(uv.y*4)*4;
     bool seam=sourceSlot==6;
     if(seam&&_BoardMoss<.01)uv.x+=.25;
     bool stone=sourceSlot<2||sourceSlot>=8;
     float slot=_BoardStoneSlot>0?_BoardStoneSlot:sourceSlot;
     if(stone)
     {
      float2 q=frac(uv*4);
      // Capped crop/offset avoids seams and palette bleed. Joined blocks carry
      // distinct UV2 seeds; individual tiles also get an object-origin seed.
      q=clamp((q-.5)*.85+.5+float2(i.style.y,frac(i.style.y*7.13))*.12-.06,.015,.985);
      uv=(float2(fmod(slot,4),floor(slot/4))+q)/4;
     }
     half4 baked,surface;
     if(stone&&slot>7.5&&slot<8.5)
     {
      // Hollow pumice is world-space triplanar so every perforated face keeps
      // round pores and mineral grains without stretching across the ellipsoid.
      half3 weights=pow(abs(normalize(i.normalWS)),4);
      weights/=max(weights.x+weights.y+weights.z,.001);
      float2 px=i.positionWS.zy*.28;
      float2 py=i.positionWS.xz*.28;
      float2 pz=i.positionWS.xy*.28;
      float2 qx=clamp(frac(px),.015,.985);
      float2 qy=clamp(frac(py),.015,.985);
      float2 qz=clamp(frac(pz),.015,.985);
      float2 ux=(float2(0,2)+qx)/4.0;
      float2 uy=(float2(0,2)+qy)/4.0;
      float2 uz=(float2(0,2)+qz)/4.0;
      // frac() wraps atlas-local coordinates. Use gradients from the unwrapped
      // projections so the derivative does not spike at each repeat boundary.
      float2 dx=ddx(px)/4.0, dy=ddy(px)/4.0;
      baked=SAMPLE_TEXTURE2D_GRAD(_BoardShapeMap,sampler_BoardShapeMap,ux,dx,dy)*weights.x+
            SAMPLE_TEXTURE2D_GRAD(_BoardShapeMap,sampler_BoardShapeMap,uy,ddx(py)/4.0,ddy(py)/4.0)*weights.y+
            SAMPLE_TEXTURE2D_GRAD(_BoardShapeMap,sampler_BoardShapeMap,uz,ddx(pz)/4.0,ddy(pz)/4.0)*weights.z;
      surface=SAMPLE_TEXTURE2D_GRAD(_BoardShapeNormal,sampler_BoardShapeNormal,ux,dx,dy)*weights.x+
              SAMPLE_TEXTURE2D_GRAD(_BoardShapeNormal,sampler_BoardShapeNormal,uy,ddx(py)/4.0,ddy(py)/4.0)*weights.y+
              SAMPLE_TEXTURE2D_GRAD(_BoardShapeNormal,sampler_BoardShapeNormal,uz,ddx(pz)/4.0,ddy(pz)/4.0)*weights.z;
     }
     else
     {
      baked=SAMPLE_TEXTURE2D(_BoardShapeMap,sampler_BoardShapeMap,uv);
      surface=SAMPLE_TEXTURE2D(_BoardShapeNormal,sampler_BoardShapeNormal,uv);
     }
     color=baked.rgb*i.color.rgb;
     if(stone)color*=_BoardRockTint.rgb;
     if(stone&&_PlanetDressing>.5)
     {
      // Three world projections keep crust walls and tile tops at a consistent real texture scale.
      half3 weights=pow(abs(i.normalWS),4);weights/=max(dot(weights,half3(1,1,1)),.001);
      float2 px=i.positionWS.zy*.72,py=i.positionWS.xz*.72,pz=i.positionWS.xy*.72;
      half4 pigment=SAMPLE_TEXTURE2D(_PlanetColor,sampler_PlanetColor,px)*weights.x+SAMPLE_TEXTURE2D(_PlanetColor,sampler_PlanetColor,py)*weights.y+SAMPLE_TEXTURE2D(_PlanetColor,sampler_PlanetColor,pz)*weights.z;
      half4 micro=SAMPLE_TEXTURE2D(_PlanetDetail,sampler_PlanetDetail,px)*weights.x+SAMPLE_TEXTURE2D(_PlanetDetail,sampler_PlanetDetail,py)*weights.y+SAMPLE_TEXTURE2D(_PlanetDetail,sampler_PlanetDetail,pz)*weights.z;
      // Vertex pigment is the production floor/blocker value separation. Never normalize it away.
      color=pigment.rgb*i.color.rgb;surface=micro;
      if(_PlanetIce>.5&&!frostSnow&&_SlideIce<.5)color*=.62;
      if(frostSnow)
      {
       half grain=frac(sin(dot(floor(i.positionWS.xz*180),float2(12.9898,78.233)))*43758.5453);
       color=half3(.52,.55,.58)*i.color.rgb*lerp(.96,1.02,grain);
       surface=half4(.5,.5,.98,1);
      }
     }
     if(seam&&_BoardMoss>.01)color=lerp(half3(.34,.36,.30),color,_BoardMoss);
     // Linear packed RG tangent normal, B roughness, A AO. No importer swizzle.
     half2 normalXY=(surface.rg*2-1)*_BoardNormalStrength;
     half3 detail=half3(normalXY,sqrt(saturate(1-dot(normalXY,normalXY))));
     stoneRoughness=saturate(surface.b+lerp(.20,-.12,saturate(i.normalWS.y)));
     if(frostSnow)stoneRoughness=.98;
     color*=lerp(.80,1,surface.a);
     half3 tangent=normalize(i.tangentWS.xyz);
     if(dot(tangent,tangent)>.01)n=normalize(tangent*detail.x+cross(n,tangent)*i.tangentWS.w*detail.y+n*detail.z);
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
    if(_SlideIce>.5)stoneRoughness=.06;
    if(_DressingGlossReview>.5)return half4((1-stoneRoughness).xxx,1);
    Light l=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half3 lighting=SampleSH(n)+i.fill+l.color*saturate(dot(n,l.direction))*l.shadowAttenuation;
    // Neutral stone fill keeps baked contrast visible without realtime shadows or SSAO.
    if(i.style.x>3.5){lighting=half3(.14,.17,.22)+i.fill*.20+l.color*(1.05*saturate(dot(n,l.direction)));color*=lerp(.48,1,saturate(n.y*1.2));if(i.normalWS.y>.4)color*=BoardShadow(i.positionWS);}
    half floorValue=i.style.x>3.5?.12:_AmbientFloor;
    color*=lerp(max(lighting,floorValue.xxx),half3(1,1,1),_Unlit);
    if(i.style.x>3.5){half3 v=GetWorldSpaceNormalizeViewDir(i.positionWS);half spec=pow(saturate(dot(n,normalize(v+l.direction))),lerp(120,8,stoneRoughness));half pigmentValue=saturate(max(i.color.r,max(i.color.g,i.color.b)));if(!frostSnow)color+=l.color*spec*(1-stoneRoughness)*lerp(.14,.55,_PlanetIce)*pigmentValue;
     if(_PlanetIce>.5&&!frostSnow){half rim=pow(1-saturate(dot(n,v)),3);half veins=SAMPLE_TEXTURE2D(_PlanetColor,sampler_PlanetColor,i.positionWS.xz*.72).r;half rime=smoothstep(.64,.85,veins);color+=half3(.15,.28,.36)*(rim*.40+rime*.13)*pigmentValue;}
    }
    return half4(MixFog(color,i.fog),_SurfaceAlpha);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
