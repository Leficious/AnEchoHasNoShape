Shader "An Echo Has No Shape/Glacier Aurora Ribbon"
{
    Properties
    {
        [HDR] _LowerColor("Lower Color", Color) = (0.05, 0.31, 0.48, 1)
        [HDR] _UpperColor("Upper Color", Color) = (0.3, 0.84, 0.75, 1)
        _Opacity("Opacity", Range(0, 1)) = 0.42
        _NoiseScale("Noise Scale", Float) = 0.035
        _NoiseSpeed("Noise Speed", Float) = 0.16
        _Displacement("Displacement", Float) = 2.4
        _Emission("Emission", Float) = 2.6
        _Seed("Seed", Float) = 1
        [HideInInspector] _AreaBlend("Glacier Area Blend", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-20"
        }

        Pass
        {
            Name "AuroraRibbon"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LowerColor;
                half4 _UpperColor;
                float _Opacity;
                float _NoiseScale;
                float _NoiseSpeed;
                float _Displacement;
                float _Emission;
                float _Seed;
                float _AreaBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32 + _Seed);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                [unroll]
                for (int octave = 0; octave < 4; octave++)
                {
                    value += ValueNoise(p) * amplitude;
                    p = mul(float2x2(1.57, 1.21, -1.21, 1.57), p) + 4.17;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                float time = _Time.y * _NoiseSpeed;
                float broadWave = sin(
                    positions.positionWS.x * _NoiseScale * 1.7
                    + positions.positionWS.y * _NoiseScale * 0.43
                    + time + _Seed) * 0.58;
                float counterWave = sin(
                    positions.positionWS.x * _NoiseScale * 0.71
                    - positions.positionWS.y * _NoiseScale * 1.15
                    - time * 0.73 + _Seed * 2.13) * 0.42;
                float verticalEnvelope = sin(saturate(input.uv.y) * PI);
                positions.positionWS += normals.normalWS
                    * (broadWave + counterWave)
                    * _Displacement
                    * verticalEnvelope;
                output.positionWS = positions.positionWS;
                output.positionHCS = TransformWorldToHClip(positions.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y * _NoiseSpeed;
                float2 flowUV = float2(
                    input.uv.x * 3.1 + time * 0.31 + _Seed,
                    input.uv.y * 1.4 - time * 0.13);
                float broadNoise = Fbm(flowUV);
                float detailNoise = Fbm(flowUV * 2.3 + float2(-time * 0.17, time * 0.09));
                float curtain = smoothstep(0.12, 0.68, broadNoise * 0.78 + detailNoise * 0.32);
                float wispBody = lerp(0.32, 1.0, curtain);

                float horizontalEdge = smoothstep(0.0, 0.13, input.uv.x)
                    * smoothstep(0.0, 0.13, 1.0 - input.uv.x);
                float bottomFade = smoothstep(0.0, 0.12, input.uv.y);
                float topFade = 1.0 - smoothstep(0.68, 1.0, input.uv.y);
                float verticalBody = bottomFade * lerp(1.0, topFade, 0.58);
                float flowingBands = 0.72 + 0.28 * sin(
                    input.uv.y * 17.0
                    + broadNoise * 5.2
                    - time * 0.9
                    + _Seed);

                half3 gradient = lerp(_LowerColor.rgb, _UpperColor.rgb, saturate(input.uv.y));
                half alpha = saturate(
                    _Opacity
                    * _AreaBlend
                    * horizontalEdge
                    * verticalBody
                    * wispBody
                    * flowingBands);
                half3 color = gradient * _Emission * (0.95h + broadNoise * 0.85h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
