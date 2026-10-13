Shader "An Echo Has No Shape/Beacon Beam"
{
    Properties { _Tint("Color", Color) = (0.55,0.9,1,0.7) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+30" "RenderType"="Transparent" }
        Pass
        {
            // Use the existing post-fog pass; retain scene-depth occlusion.
            Tags { "LightMode"="EchoRevealPostFog" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _Settings;
                float4 _DistanceFade;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 origin = TransformObjectToWorld(float3(0,0,0));
                float3 toward = _WorldSpaceCameraPos - origin;
                float3 side = float3(toward.z, 0, -toward.x);
                side = dot(side,side) > 0.0001 ? normalize(side) : float3(1,0,0);
                float3 world = origin + side * input.positionOS.x + float3(0,input.positionOS.y,0);
                o.positionCS = TransformWorldToHClip(world);
                o.positionWS = world;
                o.uv = input.uv;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float radius = abs(input.uv.x * 2 - 1);
                float edge = 1 - smoothstep(0.15, 1, radius);
                float core = exp2(-radius * radius * 32);
                float flow = 1 - _Settings.z * (0.5 + 0.5 * sin(input.uv.y * _Settings.w * 1.5 - _Time.y * _Settings.y * 1.5));
                float topFade = 1 - smoothstep(0.85, 1, input.uv.y);
                // Evaluate per fragment: interpolating vertex distances on a
                // 2000m-tall quad would give incorrect distances near its base.
                float cameraDistance = distance(input.positionWS, _WorldSpaceCameraPos);
                float distanceFade = 1 - smoothstep(_ProjectionParams.z * _DistanceFade.x,
                    _ProjectionParams.z * _DistanceFade.y, cameraDistance);
                return half4(_Tint.rgb * _Settings.x * (0.4 + core * 0.6), _Tint.a * edge * flow * topFade * distanceFade);
            }
            ENDHLSL
        }
    }
}
