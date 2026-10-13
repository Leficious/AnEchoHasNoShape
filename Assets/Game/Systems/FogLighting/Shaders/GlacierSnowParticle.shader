Shader "An Echo Has No Shape/Glacier Snow Particle"
{
    Properties
    {
        _Tint("Tint", Color) = (1, 1, 1, 1)
        _DistanceFade("Distance Fade Start / End / Power", Vector) = (2, 24, 2, 0)
        [HideInInspector] _AreaBlend("Glacier Area Blend", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+25"
        }

        Pass
        {
            Name "SnowParticle"
            Tags { "LightMode" = "EchoRevealPostFog" }

            Blend SrcAlpha OneMinusSrcAlpha
            // Composite after AERO without contributing flake depth to its fog.
            // Scene depth still occludes flakes behind solid geometry.
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float4 _DistanceFade;
                float _AreaBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.color = input.color * _Tint;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = (input.uv - 0.5) * 2.0;
                float radius = length(centered);
                float softDisc = 1.0 - smoothstep(0.42, 1.0, radius);
                float verticalGlint = 1.0 - smoothstep(0.08, 0.42, abs(centered.x));
                float horizontalGlint = 1.0 - smoothstep(0.08, 0.42, abs(centered.y));
                float crystal = saturate(softDisc + max(verticalGlint, horizontalGlint) * 0.22);
                float distanceFade = 1.0 - smoothstep(_DistanceFade.x,
                    max(_DistanceFade.x + 0.01, _DistanceFade.y),
                    distance(input.positionWS, _WorldSpaceCameraPos));
                // Stronger atmospheric attenuation without reintroducing the
                // full-screen fog's sky masking of these transparent flakes.
                distanceFade = pow(saturate(distanceFade), max(0.1, _DistanceFade.z));
                half alpha = input.color.a * crystal * _AreaBlend * distanceFade;
                clip(alpha - 0.001h);
                return half4(input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
