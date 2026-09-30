Shader "An Echo Has No Shape/Glacier Snow Particle"
{
    Properties
    {
        _Tint("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _AreaBlend("Glacier Area Blend", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest+25"
        }

        Pass
        {
            Name "SnowParticle"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            // AERO samples the camera depth texture after opaque/cutout geometry.
            // Writing the clipped flake depth keeps snow visible against the sky
            // while still allowing the volumetric pass to fog it by distance.
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
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
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
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
                half alpha = input.color.a * crystal * _AreaBlend;
                clip(alpha - 0.035h);
                return half4(input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
