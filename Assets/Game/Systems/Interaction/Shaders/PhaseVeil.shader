Shader "An Echo Has No Shape/Phase Veil"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "PhaseVeilFullscreen"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 _PhaseVeilColor;
            half4 _PhaseShimmerColor;
            float _PhaseOpacity;
            float _PhaseDistortionStrength;
            float _PhaseVignetteStrength;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
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
                    p = mul(float2x2(1.58, 1.19, -1.19, 1.58), p) + 7.13;
                    amplitude *= 0.5;
                }
                return value;
            }

            half4 SampleBlit(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(uv), 0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = input.texcoord;
                half4 originalScene = SampleBlit(screenUV);
                if (_PhaseOpacity <= 0.0001)
                {
                    return originalScene;
                }

                float time = _Time.y;
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float2 centered = screenUV * 2.0 - 1.0;
                centered.x *= aspect;

                float broadFog = Fbm(centered * 1.05 + float2(time * 0.075, -time * 0.045));
                float warp = Fbm(centered * 1.75 + float2(-time * 0.055, time * 0.085) + broadFog * 1.6);
                float fineFog = Fbm(centered * 3.25 + float2(time * 0.11, time * 0.035) + warp * 0.9);
                float warpX = Fbm(centered * 2.15 + float2(time * 0.14, -time * 0.08) + 13.7);
                float warpY = Fbm(centered.yx * 2.05 + float2(-time * 0.1, time * 0.13) + 41.3);

                float contour = abs(frac((warp * 1.65 + fineFog * 0.55 + centered.y * 0.08) - time * 0.045) - 0.5) * 2.0;
                float filament = 1.0 - smoothstep(0.035, 0.115, contour);
                filament *= smoothstep(0.56, 0.79, fineFog);

                float edgeDistance = length(centered / float2(max(aspect, 0.001), 1.0));
                float edgeBloom = smoothstep(0.42, 1.25, edgeDistance);
                float glint = smoothstep(0.76, 0.91, fineFog) *
                    (0.35 + 0.65 * ValueNoise(centered * 15.0 - time * 0.12));
                float edgeNoise = Fbm(centered * 2.6 + float2(-time * 0.12, time * 0.055) + 22.4);
                float vignettePulse = 0.72 + 0.28 * sin(time * 4.2 + edgeNoise * 8.0);
                float shimmeringVignette = saturate(
                    edgeBloom * (0.38 + edgeNoise * 0.82) * vignettePulse * _PhaseVignetteStrength);

                float2 distortionVector = float2(warpX, warpY) * 2.0 - 1.0;
                distortionVector += float2(fineFog - 0.5, broadFog - 0.5) * 0.7;
                float distortionMask = 0.42 + shimmeringVignette * 1.15 + filament * 0.38;
                float2 distortedUV = screenUV + distortionVector *
                    _PhaseDistortionStrength * _PhaseOpacity * distortionMask;
                half3 distortedScene = SampleBlit(distortedUV).rgb;

                float shimmer = saturate(
                    filament * 0.72 + glint * 0.16 + shimmeringVignette * 0.78);
                half3 veilTint = lerp(_PhaseVeilColor.rgb, _PhaseShimmerColor.rgb, shimmer * 0.5);
                veilTint += _PhaseShimmerColor.rgb *
                    (filament * 0.08 + shimmeringVignette * 0.2);
                half tintAmount = saturate(
                    0.24 + broadFog * 0.1 + shimmeringVignette * 0.22);
                half3 effectedScene = lerp(distortedScene, veilTint, tintAmount);

                half alphaShape = saturate(
                    0.64 + broadFog * 0.13 + warp * 0.06 + shimmeringVignette * 0.17);
                half effectAmount = saturate(_PhaseOpacity * alphaShape);
                half3 finalColor = lerp(originalScene.rgb, effectedScene, effectAmount);
                return half4(finalColor, originalScene.a);
            }
            ENDHLSL
        }
    }
}
