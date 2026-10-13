Shader "An Echo Has No Shape/City Muse Vegetation"
{
    Properties
    {
        _MainTex("Alpha Texture", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.35
        _Opacity("Opacity", Range(0,1)) = 0.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "MuseVegetationReveal"
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _Cutoff, _Opacity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half textureAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                clip(textureAlpha - max(_Cutoff, 0.001));
                float visibility = CityPerspectiveAt(i.positionWS, 1);
                clip(visibility - 0.001);
                half3 color, movingColor;
                float held;
                CityEchoAt(i.positionWS, color, held, movingColor);
                half shade = 0.75h + 0.25h * abs(dot(normalize(i.normalWS), normalize(float3(0.4, 1, 0.3))));
                return half4(color * shade, textureAlpha * visibility * _Opacity);
            }
            ENDHLSL
        }
    }
}
