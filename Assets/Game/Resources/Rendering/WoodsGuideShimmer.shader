Shader "An Echo Has No Shape/Woods Guide Shimmer"
{
    Properties { _Color("Color", Color) = (0.65,0.95,0.76,0.8) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Motion;
                float4 _RouteFade;
            CBUFFER_END
            float _WoodsCompletionActive;
            float3 _WoodsCompletionOrigin;
            float4 _WoodsCompletionWave;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionWS = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); o.uv = v.uv; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float elapsed = max(0, _Time.y - _Motion.z);
                float phase = frac((i.uv.x - elapsed * _Motion.x) / _Motion.y);
                // A bright leading head and long soft tail travel toward increasing spline distance.
                float pulse = smoothstep(0.1, 0.75, phase) * (1 - smoothstep(0.8, 1, phase));
                float edge = 1 - smoothstep(0.15, 0.5, abs(i.uv.y - 0.5));
                float arrivalAge = (_Time.y - _WoodsCompletionWave.x)
                    - distance(i.positionWS, _WoodsCompletionOrigin) / max(0.1, _WoodsCompletionWave.y);
                float fade = _WoodsCompletionActive * lerp(smoothstep(0, _Motion.w, arrivalAge), 1, _WoodsCompletionWave.z);
                float endpointFade = _RouteFade.y > 0.0001
                    ? smoothstep(0, _RouteFade.y, min(i.uv.x, _RouteFade.x - i.uv.x)) : 1;
                return half4(_Color.rgb, _Color.a * edge * (0.12 + pulse * 0.88) * fade * endpointFade);
            }
            ENDHLSL
        }
    }
}
