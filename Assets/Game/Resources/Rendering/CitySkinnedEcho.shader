Shader "An Echo Has No Shape/City Skinned Echo"
{
    Properties
    {
        _CityPerspective("City Perspective", Float) = 1
        [HideInInspector] _CharacterMode("Character Mode", Float) = 0
        [HideInInspector] _CharacterReveal("Character Timed Reveal", Float) = 0
        [HideInInspector] _CharacterHover("Character Hover", Range(0, 1)) = 0
        [HideInInspector] _CharacterColor("Character Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Assets/Game/Systems/Echolocation/Shaders/CityEcho.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float _CityPerspective;
        float _CharacterMode;
        float _CharacterReveal;
        float _CharacterHover;
        half4 _CharacterColor;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
        struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
        Varyings Vert(Attributes v)
        {
            Varyings o;
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            return o;
        }
        float Visibility(float3 positionWS)
        {
            if (_CharacterMode < 0.5) return CityPerspectiveAt(positionWS, _CityPerspective);
            half3 heldColor, movingColor;
            float heldAmount;
            CityEchoAt(positionWS, heldColor, heldAmount, movingColor);
            return saturate(max(_CharacterReveal, heldAmount));
        }
        half4 Frag(Varyings i) : SV_Target
        {
            float alpha = Visibility(i.positionWS);
            clip(alpha - 0.001);
            half3 heldColor, movingColor;
            float heldAmount;
            CityEchoAt(i.positionWS, heldColor, heldAmount, movingColor);
            if (_CharacterMode > 0.5)
            {
                // Muted identity, with completion gold applied afterwards (never desaturated).
                half luminance = dot(_CharacterColor.rgb, half3(0.2126, 0.7152, 0.0722));
                half3 identity = lerp(_CharacterColor.rgb, luminance.xxx, 0.35h);
                half3 color = CityCharacterColorAt(i.positionWS, identity);
                float3 normal = normalize(i.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half rim = pow(1.0h - saturate(abs(dot(normal, view))), 3.0h);
                half shape = 0.45h + 0.55h * saturate(dot(normal, normalize(float3(0.4, 1, 0.3))) * 0.5h + 0.5h);
                half3 edgeColor = lerp(color, half3(1, 1, 1), 0.3h);
                half hover = saturate(_CharacterHover);
                half3 fill = color * lerp(shape, 1.0h, hover * 0.15h);
                half3 hoverEdge = lerp(edgeColor, half3(1, 1, 1), hover * 0.2h);
                return half4(lerp(fill, hoverEdge, rim), alpha * lerp(0.45h + hover * 0.05h, 0.75h + hover * 0.1h, rim));
            }
            half shade = 0.8h + 0.2h * abs(dot(normalize(i.normalWS), normalize(float3(0.4, 1, 0.3))));
            return half4(heldColor * shade, alpha);
        }
        half4 DepthFrag(Varyings i) : SV_Target
        {
            clip(Visibility(i.positionWS) - 0.001);
            return 0;
        }
        ENDHLSL
        Pass
        {
            Name "CitySkinnedDepth"
            Tags { "LightMode"="EchoArchitectureDepth" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            ENDHLSL
        }
        Pass
        {
            Name "CitySkinnedReveal"
            Tags { "LightMode"="EchoRevealPostFog" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
    FallBack Off
}
