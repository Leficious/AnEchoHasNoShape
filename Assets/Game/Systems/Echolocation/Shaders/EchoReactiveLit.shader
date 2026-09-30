Shader "An Echo Has No Shape/Echo Reveal Overlay"
{
    Properties
    {
        _Extrusion("Surface Extrusion", Range(0, 0.15)) = 0.006
        _RevealMode("Reveal Mode", Range(0, 1)) = 0
        _RevealMultiplier("Reveal Multiplier", Range(0, 4)) = 1
        _DiffusionStrength("Surface Diffusion", Range(0, 2)) = 1
        [HDR] _ObjectEchoColor("Object Echo Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _UseObjectEchoColor("Use Object Echo Color", Float) = 0
        [HideInInspector] _UseRevealDurationOverride("Use Reveal Duration Override", Float) = 0
        [HideInInspector] _TimedRevealAmount("Timed Reveal Amount", Range(0, 1)) = 0
        [HideInInspector] _TimedRevealShimmerStrength("Timed Reveal Shimmer Strength", Range(0, 0.5)) = 0.12
        [HideInInspector] _TimedRevealShimmerSpeed("Timed Reveal Shimmer Speed", Float) = 2.2
        [HideInInspector] _PlayerEchoMask("Player Echo Mask", Range(0, 1)) = 1
        [HideInInspector] _SirenEchoMask("Siren Echo Mask", Range(0, 1)) = 1
        [HideInInspector] _SirenSubtractionMask("Siren Subtraction Mask", Range(0, 1)) = 1
        [HideInInspector] _EchoAlphaTexture("Source Alpha", 2D) = "white" {}
        [HideInInspector] _EchoUseAlphaClip("Use Source Alpha Clip", Float) = 0
        [HideInInspector] _EchoAlphaCutoff("Source Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _ObjectWireframeMultiplier("Object Wireframe Multiplier", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
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
            Name "EchoReveal"
            // Drawn explicitly by EchoPostFogRendererFeature after AERO and
            // Phase Veil. Keeping this out of UniversalForward prevents the
            // volumetric fog pass from washing the additive echo away.
            Tags { "LightMode" = "EchoRevealPostFog" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Extrusion;
                float _RevealMode;
                float _RevealMultiplier;
                float _DiffusionStrength;
                half4 _ObjectEchoColor;
                float _UseObjectEchoColor;
                float _UseRevealDurationOverride;
                float _TimedRevealAmount;
                float _TimedRevealShimmerStrength;
                float _TimedRevealShimmerSpeed;
                float _PlayerEchoMask;
                float _SirenEchoMask;
                float _SirenSubtractionMask;
                float _EchoUseAlphaClip;
                float _EchoAlphaCutoff;
                float _ObjectWireframeMultiplier;
                float _Cull;
            CBUFFER_END

            TEXTURE2D(_EchoAlphaTexture);
            SAMPLER(sampler_EchoAlphaTexture);

            float3 _EchoPulseOrigin;
            float _EchoPulseRadius;
            float _EchoPulseWidth;
            float _EchoTrailLength;
            float _EchoPulseIntensity;
            half4 _EchoPulseColor;
            float _EchoPulseActive;
            float _EchoPulseCount;
            float _EchoPulseSpacing;
            float _EchoPulseRange;
            float _EchoFadeOutDistance;
            float _EchoWireframeStrength;
            float _EchoWireframeScale;
            float _EchoWireframeThickness;

            float3 _SirenEchoPulseOrigin;
            float _SirenEchoPulseRadius;
            float _SirenEchoPulseWidth;
            float _SirenEchoTrailLength;
            float _SirenEchoPulseIntensity;
            half4 _SirenEchoPulseColor;
            float _SirenEchoPulseActive;
            float _SirenEchoPulseCount;
            float _SirenEchoPulseSpacing;
            float _SirenEchoPulseRange;
            float _SirenEchoFadeOutDistance;

            float3 _WorldReverbOrigin;
            float _WorldReverbRadius;
            float _WorldReverbWidth;
            float _WorldReverbTrailLength;
            float _WorldReverbIntensity;
            half4 _WorldReverbColor;
            float _WorldReverbActive;
            float _WorldReverbRange;
            float _WorldReverbFadeDistance;

            float CalculateGridWire(float2 gridUV)
            {
                gridUV *= _EchoWireframeScale;

                float2 axisDistance = abs(frac(gridUV + 0.5) - 0.5);
                float2 axisWidth = max(fwidth(gridUV) * _EchoWireframeThickness, 0.0001);
                float axisWire = 1.0 - saturate(min(axisDistance.x / axisWidth.x, axisDistance.y / axisWidth.y));

                float diagonalCoordinate = gridUV.x + gridUV.y;
                float diagonalDistance = abs(frac(diagonalCoordinate + 0.5) - 0.5);
                float diagonalWidth = max(fwidth(diagonalCoordinate) * _EchoWireframeThickness, 0.0001);
                float diagonalWire = 1.0 - saturate(diagonalDistance / diagonalWidth);

                return saturate(max(axisWire, diagonalWire * 0.72));
            }

            float CalculateEchoWireframe(float3 positionWS, half3 normalWS)
            {
                // Smooth triplanar weighting avoids the hard projection switch
                // that produced crawling seams on large/angular icebergs.
                float3 weights = pow(abs(normalize(normalWS)), 4.0);
                weights /= max(weights.x + weights.y + weights.z, 0.0001);
                float wireX = CalculateGridWire(positionWS.zy);
                float wireY = CalculateGridWire(positionWS.xz);
                float wireZ = CalculateGridWire(positionWS.xy);
                return saturate(wireX * weights.x + wireY * weights.y + wireZ * weights.z);
            }

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
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            float RingOffset(int ringIndex)
            {
                return ringIndex == 0
                    ? 0.0
                    : (ringIndex == 1 ? _EchoPulseSpacing : _EchoPulseSpacing * 2.35);
            }

            float RingWeight(int ringIndex)
            {
                return ringIndex == 0 ? 1.0 : (ringIndex == 1 ? 0.20 : 0.12);
            }

            float RingWidthScale(int ringIndex)
            {
                return ringIndex == 0 ? 1.20 : (ringIndex == 1 ? 0.55 : 0.45);
            }

            half3 GetActiveEchoColor()
            {
                return lerp(_EchoPulseColor.rgb, _ObjectEchoColor.rgb, saturate(_UseObjectEchoColor));
            }

            half3 CalculateMovingReveal(float distanceFromOrigin, float diffusion)
            {
                half3 movingReveal = 0.0h;
                half3 activeEchoColor = GetActiveEchoColor();

                [unroll]
                for (int ringIndex = 0; ringIndex < 3; ringIndex++)
                {
                    float ringEnabled = step((float)ringIndex + 0.5, _EchoPulseCount);
                    float ringRadius = _EchoPulseRadius - RingOffset(ringIndex);
                    float ringStarted = step(0.0, ringRadius);
                    float ringWeight = RingWeight(ringIndex);
                    float scaledWidth = _EchoPulseWidth * RingWidthScale(ringIndex);
                    float diffusedWidth = scaledWidth * lerp(1.0, 1.85, diffusion);

                    float distanceFromFront = abs(distanceFromOrigin - ringRadius);
                    float core = 1.0 - smoothstep(scaledWidth * 0.28, diffusedWidth, distanceFromFront);
                    float halo = 1.0 - smoothstep(diffusedWidth, diffusedWidth * 1.75, distanceFromFront);
                    float haloStrength = ringIndex == 0 ? 0.24 : 0.06;
                    float front = saturate(core + halo * haloStrength * diffusion);

                    float distanceBehindFront = ringRadius - distanceFromOrigin;
                    float behindMask = step(0.0, distanceBehindFront);
                    float trail = behindMask * saturate(1.0 - distanceBehindFront / max(_EchoTrailLength, 0.001));
                    trail *= trail;

                    float fadeStart = max(0.0, _EchoPulseRange - _EchoFadeOutDistance);
                    float ringFade = 1.0 - smoothstep(fadeStart, _EchoPulseRange, ringRadius);
                    float trailStrength = ringIndex == 0 ? 0.16 : 0.05;
                    half luminance = dot(activeEchoColor, half3(0.2126h, 0.7152h, 0.0722h));
                    half3 ringColor = ringIndex == 0
                        ? activeEchoColor
                        : lerp(activeEchoColor, luminance.xxx, 0.65h);
                    float ringReveal = (front + trail * trailStrength) * ringWeight * ringEnabled * ringStarted * ringFade;
                    movingReveal += ringColor * ringReveal;
                }

                return saturate(movingReveal) * _EchoPulseActive * _PlayerEchoMask;
            }

            half3 CalculateSirenMovingReveal(float distanceFromOrigin, float diffusion)
            {
                half3 movingReveal = 0.0h;

                [unroll]
                for (int ringIndex = 0; ringIndex < 3; ringIndex++)
                {
                    float ringEnabled = step((float)ringIndex + 0.5, _SirenEchoPulseCount);
                    float ringOffset = ringIndex == 0 ? 0.0 : (ringIndex == 1 ? _SirenEchoPulseSpacing : _SirenEchoPulseSpacing * 2.35);
                    float ringRadius = _SirenEchoPulseRadius - ringOffset;
                    float ringStarted = step(0.0, ringRadius);
                    float ringWeight = RingWeight(ringIndex);
                    float scaledWidth = _SirenEchoPulseWidth * RingWidthScale(ringIndex);
                    float diffusedWidth = scaledWidth * lerp(1.0, 1.85, diffusion);
                    float distanceFromFront = abs(distanceFromOrigin - ringRadius);
                    float core = 1.0 - smoothstep(scaledWidth * 0.28, diffusedWidth, distanceFromFront);
                    float halo = 1.0 - smoothstep(diffusedWidth, diffusedWidth * 1.75, distanceFromFront);
                    float front = saturate(core + halo * (ringIndex == 0 ? 0.24 : 0.06) * diffusion);
                    float distanceBehindFront = ringRadius - distanceFromOrigin;
                    float trail = step(0.0, distanceBehindFront) * saturate(1.0 - distanceBehindFront / max(_SirenEchoTrailLength, 0.001));
                    trail *= trail;
                    float fadeStart = max(0.0, _SirenEchoPulseRange - _SirenEchoFadeOutDistance);
                    float ringFade = 1.0 - smoothstep(fadeStart, _SirenEchoPulseRange, ringRadius);
                    float reveal = (front + trail * (ringIndex == 0 ? 0.16 : 0.05)) * ringWeight * ringEnabled * ringStarted * ringFade;
                    movingReveal += _SirenEchoPulseColor.rgb * reveal;
                }

                return saturate(movingReveal) * _SirenEchoPulseActive * _SirenEchoMask;
            }

            float CalculateAccumulatedReveal(float distanceFromOrigin)
            {
                if (_UseRevealDurationOverride > 0.5)
                {
                    return saturate(_TimedRevealAmount) * _PlayerEchoMask;
                }

                float accumulatedReveal = 0.0;
                float totalRingWeight = 0.0;

                [unroll]
                for (int ringIndex = 0; ringIndex < 3; ringIndex++)
                {
                    float ringEnabled = step((float)ringIndex + 0.5, _EchoPulseCount);
                    float ringRadius = _EchoPulseRadius - RingOffset(ringIndex);
                    float ringStarted = step(0.0, ringRadius);
                    float ringWeight = RingWeight(ringIndex);
                    float hasPassedSurface = step(distanceFromOrigin, ringRadius);

                    accumulatedReveal += hasPassedSurface * ringWeight * ringEnabled * ringStarted;
                    totalRingWeight += ringWeight * ringEnabled;
                }

                accumulatedReveal /= max(totalRingWeight, 0.001);

                float finalRingOffset = _EchoPulseCount > 2.5
                    ? _EchoPulseSpacing * 2.35
                    : (_EchoPulseCount > 1.5 ? _EchoPulseSpacing : 0.0);
                float eventEnd = _EchoPulseRange + finalRingOffset;
                float outlineFadeEnd = eventEnd + _EchoFadeOutDistance * 2.0;
                float eventFade = 1.0 - smoothstep(eventEnd, outlineFadeEnd, _EchoPulseRadius);

                return accumulatedReveal * eventFade * _EchoPulseActive * _PlayerEchoMask;
            }

            float CalculateSirenAccumulatedReveal(float distanceFromOrigin)
            {
                float accumulatedReveal = 0.0;
                float totalRingWeight = 0.0;

                [unroll]
                for (int ringIndex = 0; ringIndex < 3; ringIndex++)
                {
                    float ringEnabled = step((float)ringIndex + 0.5, _SirenEchoPulseCount);
                    float ringOffset = ringIndex == 0 ? 0.0 : (ringIndex == 1 ? _SirenEchoPulseSpacing : _SirenEchoPulseSpacing * 2.35);
                    float ringRadius = _SirenEchoPulseRadius - ringOffset;
                    float ringStarted = step(0.0, ringRadius);
                    float ringWeight = RingWeight(ringIndex);
                    accumulatedReveal += step(distanceFromOrigin, ringRadius) * ringWeight * ringEnabled * ringStarted;
                    totalRingWeight += ringWeight * ringEnabled;
                }

                accumulatedReveal /= max(totalRingWeight, 0.001);
                float finalOffset = _SirenEchoPulseCount > 2.5 ? _SirenEchoPulseSpacing * 2.35 : (_SirenEchoPulseCount > 1.5 ? _SirenEchoPulseSpacing : 0.0);
                float eventEnd = _SirenEchoPulseRange + finalOffset;
                float eventFade = 1.0 - smoothstep(eventEnd, eventEnd + _SirenEchoFadeOutDistance * 2.0, _SirenEchoPulseRadius);
                return accumulatedReveal * eventFade * _SirenEchoPulseActive * _SirenEchoMask;
            }

            float CalculateSirenOwnership(float distanceFromOrigin)
            {
                // The siren takes ownership over a short spatial band instead of
                // switching the player's reveal off on a single frame.
                float takeoverWidth = max(_SirenEchoPulseWidth * 3.0, 0.25);
                float distanceBehindFront = _SirenEchoPulseRadius - distanceFromOrigin;
                float arrival = smoothstep(-takeoverWidth, takeoverWidth, distanceBehindFront);

                float finalOffset = _SirenEchoPulseCount > 2.5
                    ? _SirenEchoPulseSpacing * 2.35
                    : (_SirenEchoPulseCount > 1.5 ? _SirenEchoPulseSpacing : 0.0);
                float eventEnd = _SirenEchoPulseRange + finalOffset;
                float eventFade = 1.0 - smoothstep(
                    eventEnd,
                    eventEnd + _SirenEchoFadeOutDistance * 2.0,
                    _SirenEchoPulseRadius);

                return saturate(arrival)
                    * eventFade
                    * _SirenEchoPulseActive
                    * _SirenSubtractionMask;
            }

            half3 CalculateWorldReverberation(float3 positionWS)
            {
                float distanceFromOrigin = distance(positionWS, _WorldReverbOrigin);
                float distanceFromFront = abs(distanceFromOrigin - _WorldReverbRadius);
                float core = 1.0 - smoothstep(
                    _WorldReverbWidth * 0.22,
                    _WorldReverbWidth * 1.15,
                    distanceFromFront);
                float halo = 1.0 - smoothstep(
                    _WorldReverbWidth * 1.15,
                    _WorldReverbWidth * 2.2,
                    distanceFromFront);

                float distanceBehindFront = _WorldReverbRadius - distanceFromOrigin;
                float behindMask = step(0.0, distanceBehindFront);
                float trail = behindMask * saturate(
                    1.0 - distanceBehindFront / max(_WorldReverbTrailLength, 0.001));
                trail *= trail;

                float eventFade = 1.0 - smoothstep(
                    _WorldReverbRange,
                    _WorldReverbRange + _WorldReverbFadeDistance,
                    _WorldReverbRadius);
                float movingAmount = saturate(core + halo * 0.42 + trail * 0.28);
                float accumulatedAmount = behindMask * 0.22;
                float amount = lerp(movingAmount, accumulatedAmount, step(0.5, _RevealMode));

                return _WorldReverbColor.rgb
                    * amount
                    * eventFade
                    * _WorldReverbActive
                    * _WorldReverbIntensity
                    * _PlayerEchoMask;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                output.positionWS = positions.positionWS + normals.normalWS * _Extrusion;
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = normals.normalWS;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half sourceAlpha = SAMPLE_TEXTURE2D(_EchoAlphaTexture, sampler_EchoAlphaTexture, input.uv).a;
                clip(lerp(1.0h, sourceAlpha - _EchoAlphaCutoff, saturate(_EchoUseAlphaClip)));

                float distanceFromOrigin = distance(input.positionWS, _EchoPulseOrigin);
                half3 normalWS = normalize(input.normalWS);
                float verticality = 1.0 - abs(normalWS.y);
                float normalChange = length(ddx(normalWS)) + length(ddy(normalWS));
                float angularity = saturate(normalChange * 2.5);
                float diffusion = saturate((verticality * 0.62 + angularity) * _DiffusionStrength);
                half3 movingReveal = CalculateMovingReveal(distanceFromOrigin, diffusion);
                float outlineReveal = CalculateAccumulatedReveal(distanceFromOrigin) * 0.72;
                half3 outlineColor = GetActiveEchoColor() * outlineReveal;
                float sirenDistance = distance(input.positionWS, _SirenEchoPulseOrigin);
                half3 sirenMovingReveal = CalculateSirenMovingReveal(sirenDistance, diffusion);
                float sirenOutlineReveal = CalculateSirenAccumulatedReveal(sirenDistance) * 0.72;
                half3 sirenOutlineColor = _SirenEchoPulseColor.rgb * sirenOutlineReveal;
                float outlineMode = step(0.5, _RevealMode);
                half3 playerColor = lerp(movingReveal, outlineColor, outlineMode) * _EchoPulseIntensity;
                half3 sirenColor = lerp(sirenMovingReveal, sirenOutlineColor, outlineMode) * _SirenEchoPulseIntensity;
                float sirenOwnership = CalculateSirenOwnership(sirenDistance);
                playerColor *= 1.0 - sirenOwnership;
                half3 reveal = (playerColor + sirenColor) * _RevealMultiplier;

                float wireframe = CalculateEchoWireframe(input.positionWS, normalWS);
                reveal *= 1.0h + wireframe * _EchoWireframeStrength * _ObjectWireframeMultiplier;
                reveal += CalculateWorldReverberation(input.positionWS) * _RevealMultiplier;

                float shimmerPhase = _Time.y * _TimedRevealShimmerSpeed
                    + dot(input.positionWS, float3(0.47, 0.73, 0.31));
                float shimmerWave = sin(shimmerPhase) * 0.65 + sin(shimmerPhase * 1.73 + 1.2) * 0.35;
                float shimmerMultiplier = 1.0 + shimmerWave * _TimedRevealShimmerStrength;
                float timedOutlineMask = step(0.5, _RevealMode)
                    * step(0.5, _UseRevealDurationOverride)
                    * saturate(_TimedRevealAmount);
                reveal *= lerp(1.0, shimmerMultiplier, timedOutlineMask);

                return half4(reveal, max(reveal.r, max(reveal.g, reveal.b)));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
