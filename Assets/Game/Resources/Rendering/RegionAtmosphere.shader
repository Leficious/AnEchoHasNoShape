Shader "An Echo Has No Shape/Region Atmosphere"
{
    Properties { _Tint("Tint", Color) = (0.7,0.85,0.8,0.3) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+25" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="EchoRevealPostFog" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Game/Systems/Echolocation/Shaders/CityEcho.hlsl"
            float _WoodsReplyActive;
            float3 _WoodsReplyOrigin;
            float4 _WoodsReplySettings;
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            float4 _Fade, _Style;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; half4 color : COLOR; };
            V Vert(A v)
            {
                V o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv; o.color = v.color;
                return o;
            }
            half4 Frag(V i) : SV_Target
            {
                float fade = pow(saturate(1 - smoothstep(_Fade.x, _Fade.y, distance(i.positionWS, _WorldSpaceCameraPos))), max(0.1, _Fade.z));
                float edge = 1 - smoothstep(0.1, 0.5, abs(i.uv.x - 0.5));
                float ends = smoothstep(0, 0.15, i.uv.y) * (1 - smoothstep(0.85, 1, i.uv.y));
                if (_Style.x < 0.5 && i.color.r > 0.5)
                {
                    // Keep the central stroke; companion strokes are one-third
                    // its width, with independently staggered ends.
                    float lane = floor(min(i.uv.x * 3, 2.999));
                    float localX = frac(i.uv.x * 3);
                    float lineWidth = lane == 1 ? 1.0 : 0.33;
                    edge *= 1 - smoothstep(0.06 * lineWidth, 0.24 * lineWidth, abs(localX - 0.5));
                    float trim = frac(i.color.g * 7.13 + lane * 0.37) * 0.22;
                    ends *= smoothstep(trim, trim + 0.12, i.uv.y)
                        * (1 - smoothstep(0.88 - trim, 1 - trim, i.uv.y));
                }
                half3 color = _Tint.rgb;
                float response = 0;
                float stateVisibility = 1;
                if (_Style.x < 0.5)
                {
                    half3 held, moving; float amount;
                    CityEchoAt(i.positionWS, held, amount, moving);
                    // Only sustained city reveals enable streaks. Ordinary
                    // player echoes in the unrevealed/default city do not.
                    stateVisibility = saturate(amount);
                    // Neutral is white, not the art-direction tint. Follow each
                    // authored city wave spatially, including permanent gold.
                    half3 stateColor = half3(1, 1, 1);
                    for (int wave = 0; wave < _CityWaveCount; wave++)
                    {
                        float radius = max(0, _Time.y - _CityWaveOrigins[wave].w) * _CityWaveSettings[wave].x;
                        float reached = smoothstep(0, max(0.1, _CityWaveSettings[wave].z),
                            radius - distance(i.positionWS, _CityWaveOrigins[wave].xyz));
                        stateColor = lerp(stateColor, _CityWaveColors[wave].rgb, reached);
                    }
                    color = lerp(half3(1, 1, 1), stateColor,
                        saturate(_CitySequenceVisibility * CityFailureVisibilityAt(i.positionWS)));
                    response = saturate(max(moving.r, max(moving.g, moving.b)));
                }
                else
                {
                    float radius = max(0, _Time.y - _WoodsReplySettings.x) * _WoodsReplySettings.y;
                    response = _WoodsReplyActive * (1 - smoothstep(0, 8, abs(distance(i.positionWS, _WoodsReplyOrigin) - radius)));
                }
                half alpha = saturate(_Tint.a * i.color.a * fade * edge * ends * _Fade.w * stateVisibility * (1 + response * _Style.y));
                clip(alpha - 0.001);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
