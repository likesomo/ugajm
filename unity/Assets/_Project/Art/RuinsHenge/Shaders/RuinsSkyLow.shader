Shader "SevenSteps/RuinsSkyLow"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Background"}
        Pass
        {
            Name "VertexColorSky"
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;half4 color:COLOR;};
            Varyings Vert(Attributes a){Varyings o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.color=a.color;return o;}
            half4 Frag(Varyings i):SV_Target{return half4(i.color.rgb,1);}
            ENDHLSL
        }
    }
}
