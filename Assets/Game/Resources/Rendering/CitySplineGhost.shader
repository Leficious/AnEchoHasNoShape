Shader "An Echo Has No Shape/City Spline Ghost"
{
    Properties
    {
        _Tint("Tint", Color) = (0.7, 0.85, 0.9, 0.8)
        _FigureHeight("Figure Feet Y and Height", Vector) = (0, 1.8, 0, 0)
        [HideInInspector] _CityPerspective("City Perspective", Float) = 0
        [HideInInspector] _UseHeightFade("Use Height Fade", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest+20" "RenderType"="TransparentCutout" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Assets/Game/Systems/Echolocation/Shaders/CityEcho.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _Tint;
        float4 _FigureHeight;
        float _CityPerspective;
        float _UseHeightFade;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
        struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float fog : TEXCOORD1; float localHeight : TEXCOORD2; float3 positionWS : TEXCOORD3; };
        Varyings Vert(Attributes v)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.fog = ComputeFogFactor(o.positionCS.z);
            o.localHeight = (TransformObjectToWorld(v.positionOS.xyz).y - _FigureHeight.x)
                / max(_FigureHeight.y, 0.001);
            return o;
        }
        float FigureOpacity(float localHeight, float3 positionWS)
        {
            // One height gradient across all modular/skinned parts: feet=0, head=1.
            // Continuous fade from invisible feet to normal opacity at the head.
            float heightOpacity = lerp(1.0, saturate(localHeight), saturate(_UseHeightFade));
            return saturate(_Tint.a) * heightOpacity * CityPerspectiveAt(positionWS, _CityPerspective);
        }
        void ClipFigure(Varyings i)
        {
            float threshold = frac(52.9829189 * frac(dot(floor(i.positionCS.xy), float2(0.06711056, 0.00583715))));
            clip(FigureOpacity(i.localHeight, i.positionWS) - max(threshold, 0.0001));
        }
        ENDHLSL
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            half4 Frag(Varyings i) : SV_Target
            {
                ClipFigure(i);
                half shade = 0.7h + 0.3h * saturate(dot(normalize(i.normalWS), normalize(float3(0.4, 1, 0.3))));
                half3 tint = _Tint.rgb;
                if (_CityPerspective > 0.5)
                {
                    half3 heldColor, movingColor;
                    float heldAmount;
                    CityEchoAt(i.positionWS, heldColor, heldAmount, movingColor);
                    tint = heldColor;
                    // Soften character red without changing the city's palette or final gold.
                    if (_CityPerspective > 1.5 && _CityPerspective < 2.5)
                    {
                        half luminance = dot(heldColor, half3(0.2126, 0.7152, 0.0722));
                        half3 mutedRed = lerp(heldColor, luminance.xxx, 0.45h);
                        tint = CityCharacterColorAt(i.positionWS, mutedRed);
                    }
                }
                return half4(MixFog(tint * shade, i.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "FigureDepth"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half4 DepthFrag(Varyings i) : SV_Target
            {
                ClipFigure(i);
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            half4 DepthNormalsFrag(Varyings i) : SV_Target
            {
                ClipFigure(i);
                float3 normalWS = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5)), 0);
                #else
                    return half4(normalWS, 0);
                #endif
            }
            ENDHLSL
        }
    }
}
