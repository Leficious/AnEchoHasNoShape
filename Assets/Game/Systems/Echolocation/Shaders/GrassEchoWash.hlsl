#ifndef GRASS_ECHO_WASH_INCLUDED
#define GRASS_ECHO_WASH_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "CityEcho.hlsl"

float3 _EchoPulseOrigin, _SirenEchoPulseOrigin, _WorldReverbOrigin;
float _EchoPulseRadius, _EchoPulseWidth, _EchoPulseRange, _EchoFadeOutDistance;
float _EchoPulseActive, _EchoPulseIntensity, _EchoPulseSpeed;
half4 _EchoPulseColor;
float _SirenEchoPulseRadius, _SirenEchoPulseWidth, _SirenEchoPulseRange, _SirenEchoFadeOutDistance;
float _SirenEchoPulseActive, _SirenEchoPulseIntensity, _SirenEchoPulseSpeed;
half4 _SirenEchoPulseColor;
float _WorldReverbRadius, _WorldReverbWidth, _WorldReverbRange, _WorldReverbFadeDistance;
float _WorldReverbActive, _WorldReverbIntensity, _WorldReverbSpeed;
half4 _WorldReverbColor;
float _GrassPlayerEchoValid, _GrassPlayerEchoPublishedAt;

// One broad response per echo, independent of blade edges, normals and secondary rings.
float GrassWashEnvelope(float3 positionWS, float3 origin, float radius, float width,
    float speed, float range, float fadeDistance, float fadeSeconds)
{
    float distanceToSource = distance(positionWS, origin);
    float behind = radius - distanceToSource;
    float arrival = smoothstep(-max(width, 1.5), 0.0, behind);
    // Keep the soft wash at its existing brightness for four seconds, then fade.
    float tail = 1.0 - smoothstep(speed * 4.0, speed * 4.0 + max(speed * fadeSeconds, 0.01), max(0.0, behind));
    float rangeMask = 1.0 - smoothstep(max(0.0, range - max(fadeDistance, 0.01)), max(range, 0.01), distanceToSource);
    return arrival * tail * rangeMask;
}

void AccumulateGrassWash(half3 tint, float weight, inout half3 colorSum,
    inout float weightSum, inout float strongest)
{
    // HDR echo brightness must not turn the grass into solid white.
    tint /= max(1.0h, max(tint.r, max(tint.g, tint.b)));
    colorSum += tint * weight;
    weightSum += weight;
    strongest = max(strongest, weight);
}

half3 ApplyGrassEchoWash(half3 baseColor, float3 positionWS, float strength,
    float fadeSeconds, float permanentStrength)
{
    if (strength <= 0.0) return baseColor;
    half3 tintSum = 0;
    float weightSum = 0, strongest = 0;
    if (_GrassPlayerEchoValid > 0.0)
    {
        // The ring controller stops before the last grass blades finish fading.
        float grassRadius = _EchoPulseRadius + max(0.0, _Time.y - _GrassPlayerEchoPublishedAt) * _EchoPulseSpeed;
        float player = GrassWashEnvelope(positionWS, _EchoPulseOrigin, grassRadius,
            _EchoPulseWidth, _EchoPulseSpeed, _EchoPulseRange, _EchoFadeOutDistance, fadeSeconds)
            * saturate(_EchoPulseIntensity);
        AccumulateGrassWash(_EchoPulseColor.rgb, player, tintSum, weightSum, strongest);
    }
    if (_SirenEchoPulseActive > 0.0)
    {
        float siren = GrassWashEnvelope(positionWS, _SirenEchoPulseOrigin, _SirenEchoPulseRadius,
            _SirenEchoPulseWidth, _SirenEchoPulseSpeed, _SirenEchoPulseRange, _SirenEchoFadeOutDistance, fadeSeconds)
            * saturate(_SirenEchoPulseIntensity);
        AccumulateGrassWash(_SirenEchoPulseColor.rgb, siren, tintSum, weightSum, strongest);
    }
    if (_WorldReverbActive > 0.0)
    {
        // Like accumulated tree reveals: remain lit behind the world front,
        // then fade with the world event itself, not a permanent grass memory.
        float behind = _WorldReverbRadius - distance(positionWS, _WorldReverbOrigin);
        float arrival = smoothstep(-max(_WorldReverbWidth, 1.5), 0.0, behind);
        float eventFade = 1.0 - smoothstep(_WorldReverbRange,
            _WorldReverbRange + max(_WorldReverbFadeDistance, 0.001), _WorldReverbRadius);
        float world = arrival * eventFade * saturate(_WorldReverbIntensity);
        AccumulateGrassWash(_WorldReverbColor.rgb, world, tintSum, weightSum, strongest);
    }

    for (int i = 0; i < _CityWaveCount; i++)
    {
        if (_CityWaveSettings[i].w > 3.5) continue; // Gold uses the world pulse above.
        float speed = _CityWaveSettings[i].x;
        float radius = max(0.0, _Time.y - _CityWaveOrigins[i].w) * speed;
        if (radius > _CityWaveSettings[i].y + speed * (4.0 + fadeSeconds) + _CityWaveSettings[i].z) continue;
        float city = GrassWashEnvelope(positionWS, _CityWaveOrigins[i].xyz, radius,
            _CityWaveSettings[i].z, speed, _CityWaveSettings[i].y, 10.0, fadeSeconds) * _CitySequenceVisibility;
        AccumulateGrassWash(_CityWaveColors[i].rgb, city, tintSum, weightSum, strongest);
    }

    half3 washColor = tintSum / max(weightSum, 0.0001);
    return lerp(baseColor, max(baseColor, washColor), saturate(strongest * strength));
}
#endif
