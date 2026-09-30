Shader "An Echo Has No Shape/World Reverberation Water"
{
    Properties
    {
        _SurfaceOffset("Surface Offset", Float) = 0.018
        _RippleScale("Ripple Scale", Float) = 0.28
        _RippleThickness("Ripple Thickness", Float) = 1.1
        _RevealStrength("Reveal Strength", Float) = 0.72
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+12"
        }

        Pass
        {
            Name "WorldReverbWater"
            Tags { "LightMode" = "EchoRevealPostFog" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _SurfaceOffset;
                float _RippleScale;
                float _RippleThickness;
                float _RevealStrength;
            CBUFFER_END

            float3 _WorldReverbOrigin;
            float _WorldReverbRadius;
            float _WorldReverbWidth;
            float _WorldReverbTrailLength;
            float _WorldReverbIntensity;
            half4 _WorldReverbColor;
            float _WorldReverbActive;
            float _WorldReverbRange;
            float _WorldReverbFadeDistance;

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

            float3 _WaterEntryOrigin;
            float _WaterEntryStartTime;
            float _WaterEntryActive;

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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                output.positionWS = positions.positionWS + normals.normalWS * _SurfaceOffset;

                // The source water is intentionally simple. While submerged,
                // gently displace this echo overlay so its underside no longer
                // reads as a perfectly flat invisible ceiling.
                float cameraBelowSurface = step(
                    _WorldSpaceCameraPos.y + 0.02,
                    output.positionWS.y);
                float entryWave = (
                    sin(output.positionWS.x * 0.115 + _Time.y * 1.35)
                    + sin(output.positionWS.z * 0.16 - _Time.y * 0.92) * 0.65
                    + sin((output.positionWS.x + output.positionWS.z) * 0.071 + _Time.y * 0.54) * 0.42)
                    * 0.11;
                output.positionWS += normals.normalWS
                    * entryWave
                    * cameraBelowSurface
                    * _WaterEntryActive;
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = normals.normalWS;
                return output;
            }

            float RippleLine(float coordinate)
            {
                float distanceToLine = abs(frac(coordinate + 0.5) - 0.5);
                float antialiasWidth = max(fwidth(coordinate) * _RippleThickness, 0.0005);
                return 1.0 - smoothstep(antialiasWidth, antialiasWidth * 1.8, distanceToLine);
            }

            float CalculateWaterPattern(float3 positionWS, float3 rippleOrigin)
            {
                float2 scaled = positionWS.xz * _RippleScale;
                float time = _Time.y;

                // Long warped contour bands suggest a moving surface without
                // forming the regular intersections of a grid.
                float flowCoordinate = scaled.x
                    + sin(scaled.y * 0.72 + time * 0.62) * 0.38
                    + sin(scaled.y * 1.41 - time * 0.31) * 0.13;
                float flowLine = RippleLine(flowCoordinate + time * 0.075);

                // Break the bands into uneven reflective fragments, like stylized
                // caustics rather than continuous ruled lines.
                float fragmentWave = sin(scaled.y * 2.3 - time * 0.8)
                    + sin(scaled.x * 0.67 + scaled.y * 0.91 + time * 0.35);
                float fragments = smoothstep(-0.9, 0.35, fragmentWave);
                float flowingBands = flowLine * lerp(0.24, 1.0, fragments);

                // A faint circular family radiates from the reverberation source,
                // reading as interference ripples rather than a second grid axis.
                float radialDistance = distance(positionWS.xz, rippleOrigin.xz);
                float radialWarp = sin(scaled.x * 0.44 + time * 0.5) * 0.16;
                float radialLine = RippleLine(
                    radialDistance * _RippleScale * 0.47
                    + radialWarp
                    - time * 0.055);

                return saturate(max(flowingBands, radialLine * 0.34));
            }

            float RingWeight(int ringIndex)
            {
                return ringIndex == 0 ? 1.0 : (ringIndex == 1 ? 0.20 : 0.12);
            }

            float RingWidthScale(int ringIndex)
            {
                return ringIndex == 0 ? 1.20 : (ringIndex == 1 ? 0.55 : 0.45);
            }

            float CalculateLocalPulseFollow(
                float3 positionWS,
                float3 origin,
                float radius,
                float width,
                float trailLength,
                float pulseCount,
                float pulseSpacing,
                float pulseRange,
                float fadeDistance,
                out float frontAmount)
            {
                float distanceFromOrigin = distance(positionWS, origin);
                frontAmount = 0.0;
                float trailingAmount = 0.0;
                float accumulatedAmount = 0.0;
                float totalWeight = 0.0;
                float safeFadeDistance = max(fadeDistance, 0.001);

                [unroll]
                for (int ringIndex = 0; ringIndex < 3; ringIndex++)
                {
                    float enabled = step((float)ringIndex + 0.5, pulseCount);
                    float offset = ringIndex == 0
                        ? 0.0
                        : (ringIndex == 1 ? pulseSpacing : pulseSpacing * 2.35);
                    float ringRadius = radius - offset;
                    float started = step(0.0, ringRadius);
                    float weight = RingWeight(ringIndex);
                    float ringWidth = max(width * RingWidthScale(ringIndex), 0.001);
                    float distanceFromFront = abs(distanceFromOrigin - ringRadius);
                    float core = 1.0 - smoothstep(ringWidth * 0.28, ringWidth * 1.2, distanceFromFront);
                    float halo = 1.0 - smoothstep(ringWidth * 1.2, ringWidth * 2.0, distanceFromFront);
                    float behindFront = ringRadius - distanceFromOrigin;
                    float trail = step(0.0, behindFront)
                        * saturate(1.0 - behindFront / max(trailLength, 0.001));
                    trail *= trail;
                    float ringFade = 1.0 - smoothstep(
                        max(0.0, pulseRange - safeFadeDistance),
                        pulseRange,
                        ringRadius);

                    frontAmount += (core + halo * 0.24)
                        * weight * enabled * started * ringFade;
                    trailingAmount += trail * 0.16
                        * weight * enabled * started * ringFade;
                    accumulatedAmount += step(distanceFromOrigin, ringRadius)
                        * weight * enabled * started;
                    totalWeight += weight * enabled;
                }

                accumulatedAmount /= max(totalWeight, 0.001);
                float finalOffset = pulseCount > 2.5
                    ? pulseSpacing * 2.35
                    : (pulseCount > 1.5 ? pulseSpacing : 0.0);
                float eventEnd = pulseRange + finalOffset;
                float eventFade = 1.0 - smoothstep(
                    eventEnd,
                    eventEnd + safeFadeDistance * 2.0,
                    radius);

                frontAmount = saturate(frontAmount) * eventFade;
                return max(saturate(trailingAmount), accumulatedAmount * 0.28) * eventFade;
            }

            float CalculateSirenOwnership(float3 positionWS)
            {
                float distanceFromOrigin = distance(positionWS, _SirenEchoPulseOrigin);
                float takeoverWidth = max(_SirenEchoPulseWidth * 3.0, 0.25);
                float arrival = smoothstep(
                    -takeoverWidth,
                    takeoverWidth,
                    _SirenEchoPulseRadius - distanceFromOrigin);
                float finalOffset = _SirenEchoPulseCount > 2.5
                    ? _SirenEchoPulseSpacing * 2.35
                    : (_SirenEchoPulseCount > 1.5 ? _SirenEchoPulseSpacing : 0.0);
                float eventEnd = _SirenEchoPulseRange + finalOffset;
                float safeFadeDistance = max(_SirenEchoFadeOutDistance, 0.001);
                float eventFade = 1.0 - smoothstep(
                    eventEnd,
                    eventEnd + safeFadeDistance * 2.0,
                    _SirenEchoPulseRadius);

                return saturate(arrival) * eventFade * _SirenEchoPulseActive;
            }

            float CalculateWaterEntryRipple(float3 positionWS)
            {
                float elapsed = max(0.0, _Time.y - _WaterEntryStartTime);
                float distanceFromImpact = distance(positionWS.xz, _WaterEntryOrigin.xz);
                float primaryRadius = elapsed * 7.5;
                float secondaryRadius = max(0.0, primaryRadius - 0.8);
                float primary = 1.0 - smoothstep(0.06, 0.34, abs(distanceFromImpact - primaryRadius));
                float secondary = 1.0 - smoothstep(0.08, 0.42, abs(distanceFromImpact - secondaryRadius));
                float fade = 1.0 - smoothstep(0.25, 1.15, elapsed);
                return saturate(primary + secondary * 0.42) * fade * _WaterEntryActive;
            }

            half3 CalculateUnderwaterSurface(Varyings input)
            {
                float cameraBelowSurface = step(
                    _WorldSpaceCameraPos.y + 0.02,
                    input.positionWS.y);
                float underwater = cameraBelowSurface * _WaterEntryActive;

                float3 viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
                float facing = saturate(abs(dot(normalize(input.normalWS), viewDirection)));
                float grazingReflection = pow(1.0 - facing, 2.2);

                float2 surface = input.positionWS.xz * 0.19;
                float movingField =
                    sin(surface.x * 1.08 + sin(surface.y * 0.71 + _Time.y * 0.8) * 1.15 + _Time.y * 1.12)
                    + sin(surface.y * 1.37 - sin(surface.x * 0.52 - _Time.y * 0.46) * 0.9 - _Time.y * 0.73);
                float brokenShimmer = smoothstep(0.64, 1.58, movingField);

                // Broad, slowly moving dark gaps keep the underside organic and
                // prevent it from becoming a uniformly tinted geometric plane.
                float broadVariation = saturate(
                    sin(surface.x * 0.23 + _Time.y * 0.21)
                    * sin(surface.y * 0.31 - _Time.y * 0.17)
                    * 0.5 + 0.5);
                float depthBelowSurface = max(0.0, input.positionWS.y - _WorldSpaceCameraPos.y);
                float depthVisibility = 1.0 - smoothstep(9.0, 22.0, depthBelowSurface);
                float amount = underwater
                    * depthVisibility
                    * (0.075 + grazingReflection * 0.38 + brokenShimmer * 0.22)
                    * lerp(0.62, 1.0, broadVariation);

                half3 deepColor = half3(0.025h, 0.17h, 0.22h);
                half3 surfaceLight = half3(0.25h, 0.78h, 0.86h);
                return lerp(deepColor, surfaceLight, brokenShimmer * 0.78 + grazingReflection * 0.3)
                    * amount;
            }

            float CalculateWorldPulseFollow(float3 positionWS, out float frontAmount)
            {
                float distanceFromOrigin = distance(positionWS, _WorldReverbOrigin);
                float distanceFromFront = abs(distanceFromOrigin - _WorldReverbRadius);
                float core = 1.0 - smoothstep(
                    _WorldReverbWidth * 0.18,
                    _WorldReverbWidth * 1.05,
                    distanceFromFront);
                float halo = 1.0 - smoothstep(
                    _WorldReverbWidth * 1.05,
                    _WorldReverbWidth * 2.15,
                    distanceFromFront);

                float distanceBehindFront = _WorldReverbRadius - distanceFromOrigin;
                float trail = step(0.0, distanceBehindFront)
                    * saturate(1.0 - distanceBehindFront / max(_WorldReverbTrailLength, 0.001));
                trail *= trail;

                float eventFade = 1.0 - smoothstep(
                    _WorldReverbRange,
                    _WorldReverbRange + _WorldReverbFadeDistance,
                    _WorldReverbRadius);

                frontAmount = saturate(core + halo * 0.3);

                // Once the world front has crossed this fragment of water, retain
                // a quieter pattern until the same event fade used by land ends.
                float passedByPulse = step(distanceFromOrigin, _WorldReverbRadius);
                float accumulatedAmount = passedByPulse * 0.28;

                frontAmount *= eventFade * _WorldReverbActive;
                return max(trail * 0.14, accumulatedAmount)
                    * eventFade
                    * _WorldReverbActive;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float worldFront;
                float worldFollow = CalculateWorldPulseFollow(input.positionWS, worldFront);
                float playerFront;
                float playerFollow = CalculateLocalPulseFollow(
                    input.positionWS,
                    _EchoPulseOrigin,
                    _EchoPulseRadius,
                    _EchoPulseWidth,
                    _EchoTrailLength,
                    _EchoPulseCount,
                    _EchoPulseSpacing,
                    _EchoPulseRange,
                    _EchoFadeOutDistance,
                    playerFront)
                    * _EchoPulseActive
                    * _EchoPulseIntensity;
                playerFront *= _EchoPulseActive * _EchoPulseIntensity;

                float sirenFront;
                float sirenFollow = CalculateLocalPulseFollow(
                    input.positionWS,
                    _SirenEchoPulseOrigin,
                    _SirenEchoPulseRadius,
                    _SirenEchoPulseWidth,
                    _SirenEchoTrailLength,
                    _SirenEchoPulseCount,
                    _SirenEchoPulseSpacing,
                    _SirenEchoPulseRange,
                    _SirenEchoFadeOutDistance,
                    sirenFront)
                    * _SirenEchoPulseActive
                    * _SirenEchoPulseIntensity;
                sirenFront *= _SirenEchoPulseActive * _SirenEchoPulseIntensity;

                // Match the rest of the echo system: once a siren front reaches
                // the water it suppresses the player's color instead of stacking.
                float sirenOwnership = CalculateSirenOwnership(input.positionWS);
                playerFront *= 1.0 - sirenOwnership;
                playerFollow *= 1.0 - sirenOwnership;

                float worldPattern = CalculateWaterPattern(input.positionWS, _WorldReverbOrigin);
                float playerPattern = CalculateWaterPattern(input.positionWS, _EchoPulseOrigin);
                float sirenPattern = CalculateWaterPattern(input.positionWS, _SirenEchoPulseOrigin);
                half3 color = (
                    _WorldReverbColor.rgb
                        * (worldFront * 1.35 + worldFollow * worldPattern)
                        * _WorldReverbIntensity
                    + _EchoPulseColor.rgb * (playerFront * 1.2 + playerFollow * playerPattern)
                    + _SirenEchoPulseColor.rgb * (sirenFront * 1.12 + sirenFollow * sirenPattern))
                    * _RevealStrength;
                float entryRipple = CalculateWaterEntryRipple(input.positionWS);
                color += half3(0.36h, 0.9h, 1.0h) * entryRipple * 1.35h;
                color += CalculateUnderwaterSurface(input);
                return half4(color, max(color.r, max(color.g, color.b)));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
