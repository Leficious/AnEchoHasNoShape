Shader "An Echo Has No Shape/Ground Blend Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,2)) = 1
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.35
        _OcclusionStrength("Occlusion", Range(0,1)) = 1

        [Header(Ground Blend)]
        _GroundMap("Fallback Ground Texture", 2D) = "gray" {}
        _GroundTint("Ground Tint", Color) = (1,1,1,1)
        _GroundTiling("Fallback World Tiling", Float) = 0.12
        _GroundBlendHeight("Blend Height", Float) = 0.8
        _GroundBlendFalloff("Blend Falloff", Float) = 0.45
        _GroundBlendStrength("Blend Strength", Range(0,1)) = 1
        _GroundBlendNoiseScale("Edge Noise Scale", Float) = 1.5
        _GroundBlendNoiseStrength("Edge Noise", Range(0,1)) = 0.2
        _VertexColorInfluence("Vertex Red Influence", Range(0,1)) = 0
        [HideInInspector] _GroundBlendBottomY("Object Bottom Y", Float) = 0

        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "UniversalMaterialType"="Lit"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GroundBlendVertex
            #pragma fragment GroundBlendFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            TEXTURE2D(_GroundMap);     SAMPLER(sampler_GroundMap);
            TEXTURE2D(_GlobalGroundBlendMap); SAMPLER(sampler_GlobalGroundBlendMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _GroundTint;
                half _BumpScale;
                half _Metallic;
                half _Smoothness;
                half _OcclusionStrength;
                float _GroundTiling;
                float _GroundBlendHeight;
                float _GroundBlendFalloff;
                float _GroundBlendStrength;
                float _GroundBlendNoiseScale;
                float _GroundBlendNoiseStrength;
                float _VertexColorInfluence;
                float _GroundBlendBottomY;
                half _Cutoff;
                float _Cull;
            CBUFFER_END

            float4 _GlobalGroundBlendBounds; // min X, min Z, size X, size Z
            float _GlobalGroundBlendMapAvailable;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half4 fogFactorAndVertexLight : TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
                #ifdef DYNAMICLIGHTMAP_ON
                    float2 dynamicLightmapUV : TEXCOORD6;
                #endif
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float GroundBlendHash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float GroundBlendNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);
                return lerp(
                    lerp(GroundBlendHash(cell), GroundBlendHash(cell + float2(1, 0)), local.x),
                    lerp(GroundBlendHash(cell + float2(0, 1)), GroundBlendHash(cell + 1), local.x),
                    local.y);
            }

            Varyings GroundBlendVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;

                half fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                half3 vertexLight = VertexLighting(positionInputs.positionWS, normalInputs.normalWS);
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
                #ifdef DYNAMICLIGHTMAP_ON
                    output.dynamicLightmapUV = input.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                return output;
            }

            void InitializeGroundBlendInputData(Varyings input, half3 normalTS, out InputData inputData)
            {
                inputData = (InputData)0;
                inputData.positionWS = input.positionWS;

                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent, input.normalWS.xyz);
                inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactorAndVertexLight.x;
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);

                #ifdef DYNAMICLIGHTMAP_ON
                    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
                #else
                    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                #endif
            }

            half4 GroundBlendFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                #ifdef _ALPHATEST_ON
                    clip(baseSample.a - _Cutoff);
                #endif

                float2 fallbackUV = input.positionWS.xz * _GroundTiling;
                half3 fallbackGround = SAMPLE_TEXTURE2D(_GroundMap, sampler_GroundMap, fallbackUV).rgb;

                float2 worldUV = (input.positionWS.xz - _GlobalGroundBlendBounds.xy)
                    / max(_GlobalGroundBlendBounds.zw, float2(0.001, 0.001));
                half3 bakedGround = SAMPLE_TEXTURE2D(_GlobalGroundBlendMap, sampler_GlobalGroundBlendMap, saturate(worldUV)).rgb;
                half insideMap = step(0.0, worldUV.x) * step(worldUV.x, 1.0)
                    * step(0.0, worldUV.y) * step(worldUV.y, 1.0);
                half useBakedMap = saturate(_GlobalGroundBlendMapAvailable * insideMap);
                half3 groundColor = lerp(fallbackGround, bakedGround, useBakedMap) * _GroundTint.rgb;

                float heightAboveBase = input.positionWS.y - _GroundBlendBottomY;
                float edgeNoise = (GroundBlendNoise(input.positionWS.xz * _GroundBlendNoiseScale) - 0.5)
                    * _GroundBlendNoiseStrength * max(_GroundBlendFalloff, 0.001) * 2.0;
                float blendMask = 1.0 - smoothstep(
                    _GroundBlendHeight + edgeNoise,
                    _GroundBlendHeight + max(_GroundBlendFalloff, 0.001) + edgeNoise,
                    heightAboveBase);
                blendMask *= _GroundBlendStrength;
                blendMask *= lerp(1.0, input.color.r, _VertexColorInfluence);
                blendMask = saturate(blendMask);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = lerp(baseSample.rgb, groundColor, blendMask);
                surfaceData.alpha = baseSample.a;
                surfaceData.metallic = lerp(_Metallic, 0.0h, blendMask);
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = lerp(_Smoothness, min(_Smoothness, 0.25h), blendMask);
                surfaceData.normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
                surfaceData.occlusion = _OcclusionStrength;
                surfaceData.emission = half3(0, 0, 0);
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;

                InputData inputData;
                InitializeGroundBlendInputData(input, surfaceData.normalTS, inputData);
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/Meta"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
