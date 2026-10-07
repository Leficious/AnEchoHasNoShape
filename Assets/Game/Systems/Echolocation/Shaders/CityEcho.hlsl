#ifndef CITY_ECHO_INCLUDED
#define CITY_ECHO_INCLUDED
float4 _CityWaveOrigins[5]; // xyz origin, w start time
float4 _CityWaveColors[5];
float4 _CityWaveSettings[5]; // speed, range, width, perspective (0 white, 1 blue, 2 red, 3 green, 4 gold)
int _CityWaveCount;
float _CitySequenceVisibility;

half3 CityCharacterColorAt(float3 positionWS, half3 identityColor)
{
    half3 color = identityColor;
    for (int i = 0; i < _CityWaveCount; i++)
    {
        if (_CityWaveSettings[i].w < 3.5) continue;
        float radius = max(0, _Time.y - _CityWaveOrigins[i].w) * _CityWaveSettings[i].x;
        float reached = smoothstep(0, max(0.1, _CityWaveSettings[i].z), radius - distance(positionWS, _CityWaveOrigins[i].xyz));
        color = lerp(color, _CityWaveColors[i].rgb, reached * _CitySequenceVisibility);
    }
    return color;
}

float CityPerspectiveAt(float3 positionWS, float perspective)
{
    if (perspective < 0.5) return 1;
    float amount = 0;
    for (int i = 0; i < _CityWaveCount; i++)
    {
        float radius = max(0, _Time.y - _CityWaveOrigins[i].w) * _CityWaveSettings[i].x;
        float reached = smoothstep(0, max(0.1, _CityWaveSettings[i].z), radius - distance(positionWS, _CityWaveOrigins[i].xyz));
        float target = abs(_CityWaveSettings[i].w - perspective) < 0.5 || _CityWaveSettings[i].w > 3.5 ? 1 : 0;
        amount = lerp(amount, target, reached);
    }
    return amount * _CitySequenceVisibility;
}

void CityEchoAt(float3 positionWS, out half3 heldColor, out float heldAmount, out half3 movingColor)
{
    heldColor = 0;
    heldAmount = 0;
    movingColor = 0;
    for (int i = 0; i < _CityWaveCount; i++)
    {
        float radius = max(0, _Time.y - _CityWaveOrigins[i].w) * _CityWaveSettings[i].x;
        float distanceToSource = distance(positionWS, _CityWaveOrigins[i].xyz);
        float width = max(0.1, _CityWaveSettings[i].z);
        float behind = radius - distanceToSource;
        float reached = smoothstep(0, width, behind);
        heldColor = lerp(heldColor, _CityWaveColors[i].rgb, reached);
        heldAmount = max(heldAmount, reached);
        // Gold uses the real world echo; character waves briefly affect surroundings.
        float ring = (1 - smoothstep(0, width, abs(behind)))
            * (1 - smoothstep(_CityWaveSettings[i].y, _CityWaveSettings[i].y + width, radius));
        if (_CityWaveSettings[i].w < 3.5) movingColor += _CityWaveColors[i].rgb * ring * 2;
    }
    heldAmount *= _CitySequenceVisibility;
    movingColor *= _CitySequenceVisibility;
}
#endif
