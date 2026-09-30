Shader "An Echo Has No Shape/Interaction Hover Overlay"
{
    Properties
    {
        _HighlightColor ("Highlight Color", Color) = (0.78, 0.92, 1.0, 0.42)
        _HighlightStrength ("Highlight Strength", Range(0, 2)) = 1
        _ShimmerSpeed ("Shimmer Speed", Range(0.1, 8)) = 1.8
        _Visibility ("Visibility", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "Interaction Highlight"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HighlightColor;
                float _HighlightStrength;
                float _ShimmerSpeed;
                float _Visibility;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float spatialPhase = dot(input.positionWS, float3(0.31, 0.68, 0.17));
                float shimmer = sin(spatialPhase * 2.1 + _Time.y * _ShimmerSpeed) * 0.5 + 0.5;
                float subtleVariation = lerp(0.9, 1.08, shimmer);
                half4 color = _HighlightColor;
                color.rgb *= subtleVariation * _HighlightStrength * _Visibility;
                color.a *= subtleVariation * saturate(_HighlightStrength) * _Visibility;
                return color;
            }
            ENDHLSL
        }
    }
}
