Shader "An Echo Has No Shape/Ground Blend Overlay"
{
    Properties
    {
        _GroundTint("Ground Tint", Color) = (1,1,1,1)
        _GroundBlendHeight("Blend Height", Float) = 0.8
        _GroundBlendFalloff("Blend Falloff", Float) = 0.45
        _GroundBlendStrength("Blend Strength", Range(0,1)) = 1
        _GroundBlendNoiseScale("Edge Noise Scale", Float) = 1.5
        _GroundBlendNoiseStrength("Edge Noise", Range(0,1)) = 0.2
        _VertexColorInfluence("Vertex Red Influence", Range(0,1)) = 0
        _GroundBlendMaximumOpacity("Maximum Opacity", Range(0,1)) = 0.55
        _GroundSampleMip("Terrain Sample Mip", Range(0,6)) = 2
        [HideInInspector] _GroundBlendBottomY("Object Bottom Y", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" }
        Pass
        {
            Name "GroundBlendOverlay"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GlobalGroundBlendMap); SAMPLER(sampler_GlobalGroundBlendMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _GroundTint;
                float _GroundBlendHeight, _GroundBlendFalloff, _GroundBlendStrength;
                float _GroundBlendNoiseScale, _GroundBlendNoiseStrength;
                float _VertexColorInfluence, _GroundBlendBottomY;
                float _GroundBlendMaximumOpacity, _GroundSampleMip;
            CBUFFER_END
            float4 _GlobalGroundBlendBounds;
            float _GlobalGroundBlendMapAvailable;

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float2 p) { p = frac(p * float2(123.34,456.21)); p += dot(p,p+45.32); return frac(p.x*p.y); }
            float Noise(float2 p)
            {
                float2 c=floor(p), f=frac(p); f=f*f*(3.0-2.0*f);
                return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),lerp(Hash(c+float2(0,1)),Hash(c+1),f.x),f.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS=p.positionCS; output.positionWS=p.positionWS;
                output.fogFactor=ComputeFogFactor(p.positionCS.z); output.color=input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv=(input.positionWS.xz-_GlobalGroundBlendBounds.xy)/max(_GlobalGroundBlendBounds.zw,float2(0.001,0.001));
                half inside=step(0.0,uv.x)*step(uv.x,1.0)*step(0.0,uv.y)*step(uv.y,1.0);
                clip(_GlobalGroundBlendMapAvailable*inside-0.5);
                float edge=(Noise(input.positionWS.xz*_GroundBlendNoiseScale)-0.5)*_GroundBlendNoiseStrength*max(_GroundBlendFalloff,0.001)*2.0;
                float h=input.positionWS.y-_GroundBlendBottomY;
                half mask=(1.0h-smoothstep(_GroundBlendHeight+edge,_GroundBlendHeight+max(_GroundBlendFalloff,0.001)+edge,h))*_GroundBlendStrength;
                mask*=lerp(1.0h,input.color.r,_VertexColorInfluence); clip(mask-0.002h);
                half3 ground=SAMPLE_TEXTURE2D_LOD(_GlobalGroundBlendMap,sampler_GlobalGroundBlendMap,saturate(uv),_GroundSampleMip).rgb*_GroundTint.rgb;

                // The baked map contains albedo, not the terrain's final shaded color.
                // Shade it as horizontal ground so it sits in the same lighting as the terrain.
                const half3 groundNormal = half3(0.0h, 1.0h, 0.0h);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half direct = saturate(dot(groundNormal, mainLight.direction));
                half3 groundLighting = SampleSH(groundNormal)
                    + mainLight.color * direct * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                ground *= max(groundLighting, 0.02h);

                // Preserve the source material through the transition, but lock to the
                // terrain color at the actual contact point so no bright seam remains.
                half opacity = saturate(mask) * _GroundBlendMaximumOpacity;
                half contactLock = smoothstep(0.82h, 0.98h, saturate(mask));
                opacity = lerp(opacity, saturate(mask), contactLock);
                return half4(MixFog(ground,input.fogFactor),opacity);
            }
            ENDHLSL
        }
    }
}
