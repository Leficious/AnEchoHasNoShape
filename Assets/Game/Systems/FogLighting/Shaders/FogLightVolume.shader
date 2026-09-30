Shader "Hidden/An Echo Has No Shape/Fog Light Volume"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (1, 1, 1, 1)
        _Density("Density", Range(0, 1.5)) = 0.22
        _Brightness("Brightness", Range(0, 8)) = 0.8
        _Falloff("Radial Falloff", Range(0.25, 6)) = 1.8
        _NoiseAmount("Noise Amount", Range(0, 1)) = 0.22
        _NoiseScale("Noise Scale", Range(0.05, 3)) = 0.35
        _NoiseSpeed("Noise Speed", Range(0, 2)) = 0.08
        _DepthOffset("Depth Offset", Range(0, 0.25)) = 0.025
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+50"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FogLightVolume"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            Cull Front
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float3 _VolumeCenter;
                float _VolumeRadius;
                float _Density;
                float _Brightness;
                float _Falloff;
                float _NoiseAmount;
                float _NoiseScale;
                float _NoiseSpeed;
                float _DepthOffset;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                return output;
            }

            float FogNoise(float3 positionWS)
            {
                float3 p = positionWS * _NoiseScale;
                float movement = _Time.y * _NoiseSpeed;
                float first = sin(dot(p, float3(1.17, 1.73, 1.31)) + movement);
                float second = sin(dot(p, float3(-1.91, 0.83, 1.47)) - movement * 0.71);
                float third = sin(dot(p, float3(0.61, -1.53, 2.03)) + movement * 0.43);
                return saturate((first + second + third) * 0.166667 + 0.5);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 cameraPosition = GetCameraPositionWS();
                float3 rayDirection = normalize(input.positionWS - cameraPosition);
                float3 centerOffset = cameraPosition - _VolumeCenter;

                float projectedCenter = dot(centerOffset, rayDirection);
                float centerDistanceSquared = dot(centerOffset, centerOffset) - _VolumeRadius * _VolumeRadius;
                float discriminant = projectedCenter * projectedCenter - centerDistanceSquared;
                clip(discriminant);

                float intersection = sqrt(max(discriminant, 0.0));
                float nearDistance = max(-projectedCenter - intersection, 0.0);
                float farDistance = -projectedCenter + intersection;

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);

                #if UNITY_REVERSED_Z
                    bool hasOpaqueSurface = rawDepth > 0.00001;
                    float deviceDepth = rawDepth;
                #else
                    bool hasOpaqueSurface = rawDepth < 0.99999;
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                if (hasOpaqueSurface)
                {
                    float3 surfacePosition = ComputeWorldSpacePosition(screenUV, deviceDepth, UNITY_MATRIX_I_VP);
                    float surfaceDistance = distance(cameraPosition, surfacePosition);
                    farDistance = min(farDistance, max(surfaceDistance - _DepthOffset, 0.0));
                }

                float segmentLength = farDistance - nearDistance;
                clip(segmentLength);

                const int StepCount = 12;
                float stepLength = segmentLength / StepCount;
                float accumulatedDensity = 0.0;

                [unroll]
                for (int stepIndex = 0; stepIndex < StepCount; stepIndex++)
                {
                    float distanceAlongRay = nearDistance + (stepIndex + 0.5) * stepLength;
                    float3 samplePosition = cameraPosition + rayDirection * distanceAlongRay;
                    float normalizedRadius = distance(samplePosition, _VolumeCenter) / max(_VolumeRadius, 0.0001);
                    float radialDensity = pow(saturate(1.0 - normalizedRadius), _Falloff);
                    float noise = lerp(1.0, FogNoise(samplePosition), _NoiseAmount);
                    accumulatedDensity += radialDensity * noise;
                }

                float opticalDepth = accumulatedDensity * stepLength * _Density;
                float opacity = saturate(1.0 - exp(-opticalDepth));
                float luminance = opacity * _Brightness;

                // Premultiplied output keeps the volume luminous while retaining a soft fog veil.
                return half4(_Color.rgb * luminance, opacity);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
