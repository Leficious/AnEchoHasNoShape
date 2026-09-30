Shader "An Echo Has No Shape/Visible Water Surface"
{
    Properties
    {
        _SurfaceOffset("Surface Offset", Float) = 0.01
        _SurfaceOpacity("Surface Opacity", Range(0, 1)) = 0.38
        _SurfaceColor("Surface Color", Color) = (0.045, 0.19, 0.25, 1)
        _HighlightColor("Highlight Color", Color) = (0.24, 0.68, 0.76, 1)
        _WaveScale("Wave Scale", Float) = 0.16
        _WaveHeight("Wave Height", Float) = 0.085
        _FogFadeStart("Fog Fade Start", Float) = 22
        _FogFadeEnd("Fog Fade End", Float) = 85
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+10"
        }

        Pass
        {
            Name "VisibleWaterSurface"
            Tags { "LightMode" = "EchoRevealPostFog" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _SurfaceOffset;
                float _SurfaceOpacity;
                half4 _SurfaceColor;
                half4 _HighlightColor;
                float _WaveScale;
                float _WaveHeight;
                float _FogFadeStart;
                float _FogFadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            void EvaluateWaves(float2 positionXZ, out float height, out float2 slope)
            {
                float2 scaled = positionXZ * _WaveScale;
                float phaseA = scaled.x + scaled.y * 0.37 + _Time.y * 0.72;
                float phaseB = scaled.y * 1.31 - scaled.x * 0.22 - _Time.y * 0.51;
                float phaseC = (scaled.x + scaled.y) * 0.63 + _Time.y * 0.29;

                height = sin(phaseA) * 0.52
                    + sin(phaseB) * 0.31
                    + sin(phaseC) * 0.17;
                slope.x = (cos(phaseA) * 0.52
                    - cos(phaseB) * 0.31 * 0.22
                    + cos(phaseC) * 0.17 * 0.63) * _WaveScale * _WaveHeight;
                slope.y = (cos(phaseA) * 0.52 * 0.37
                    + cos(phaseB) * 0.31 * 1.31
                    + cos(phaseC) * 0.17 * 0.63) * _WaveScale * _WaveHeight;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                float height;
                float2 slope;
                EvaluateWaves(positions.positionWS.xz, height, slope);

                output.positionWS = positions.positionWS
                    + normals.normalWS * (_SurfaceOffset + height * _WaveHeight);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = normalize(float3(-slope.x, 1.0, -slope.y));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float cameraAbove = step(input.positionWS.y + 0.02, _WorldSpaceCameraPos.y);
                clip(cameraAbove - 0.5);

                float3 viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
                float facing = saturate(dot(normalize(input.normalWS), viewDirection));
                float fresnel = pow(1.0 - facing, 2.4);

                float2 surface = input.positionWS.xz * _WaveScale;
                float broadSheen = sin(
                    surface.x * 0.21
                    + surface.y * 0.09
                    + sin(surface.y * 0.13 - _Time.y * 0.11) * 0.8
                    + _Time.y * 0.16) * 0.5 + 0.5;

                float cameraDistance = distance(_WorldSpaceCameraPos, input.positionWS);
                float fogVisibility = 1.0 - smoothstep(
                    _FogFadeStart,
                    max(_FogFadeStart + 0.1, _FogFadeEnd),
                    cameraDistance);
                float highlightAmount = saturate(fresnel * 0.9 + broadSheen * 0.08);
                half3 color = lerp(_SurfaceColor.rgb, _HighlightColor.rgb, highlightAmount);
                color *= lerp(0.94h, 1.06h, broadSheen);
                float alpha = _SurfaceOpacity
                    * fogVisibility
                    * (0.72 + fresnel * 0.52);

                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
