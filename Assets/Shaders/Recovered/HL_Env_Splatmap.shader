// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Splatmap, a URP 14 Lit Shader Graph (platform 14), opaque. A splatmap
// mask's green, blue and alpha blend four layers (red is the base) of three Texture2DArrays: diffuse (tinted), normal
// and metal/smoothness, each layer with its own array slice and UV scale/offset.
Shader "HardLight/Environment/HL_Env_Splatmap"
{
    Properties
    {
        _NOTE_All_textures_in_each_Array_type_must_have_the_same_texture_import_settings ("!NOTE All textures in each Array type must have the same texture import settings and texture size", Float) = 0
        _Separator ("#", Float) = 0
        _Title_Splatmap_Mask_Texture ("## Splatmap Mask Texture", Float) = 0
        [NoScaleOffset]_Splatmap_Mask_Texture ("-- Splatmap Texture", 2D) = "white" {}
        _Title_Texture2DArray ("## Texture2DArray", Float) = 0
        [NoScaleOffset]_Diff_Texture2DArray ("-- Diff Texture2DArray", 2DArray) = "" {}
        [NoScaleOffset]_Normal_Texture2DArray ("-- Normal Texture2DArray", 2DArray) = "" {}
        [NoScaleOffset]_MS_Texture2DArray ("-- MS Texture2DArray", 2DArray) = "" {}
        _ArrayIndex_Red ("-- ArrayIndex : Red", Float) = 0
        _ArrayIndex_Green ("-- ArrayIndex : Green", Float) = 1
        _ArrayIndex_Blue ("-- ArrayIndex : Blue", Float) = 2
        _ArrayIndex_Alpha ("-- ArrayIndex : Alpha", Float) = 3
        _Separator_1 ("# (1)", Float) = 0
        _Title_Surface_Inputs_RED ("## Surface Inputs : RED", Float) = 0
        _Diff_Colour_Tint_Red ("-- Diff Colour Tint Red", Color) = (1,1,1,0)
        _Red_UV_Scale_Offset ("-- UV/Offset Red", Vector) = (1,1,0,0)
        _Smoothness_Intensity_Red ("-- Smoothness Intensity Red", Range(0, 1)) = 0.5
        _Metal_Intensity_Red ("-- Metal Intensity Red", Range(0, 1)) = 0
        _Separator_2 ("# (2)", Float) = 0
        _Title_Surface_Inputs_GREEN ("## Surface Inputs : GREEN", Float) = 0
        _Diff_Colour_Tint_Green ("-- Diff Colour Tint Green", Color) = (1,1,1,0)
        _Green_UV_Scale_Offset ("-- UV/Offset Green", Vector) = (1,1,0,0)
        _Smoothness_Intensity_Green ("-- Smoothness Intensity Green", Range(0, 1)) = 1
        _Metal_Intensity_Green ("-- Metal Intensity Green", Range(0, 1)) = 1
        _Separator_3 ("# (3)", Float) = 0
        _Title_Surface_Inputs_BLUE ("## Surface Inputs : BLUE", Float) = 0
        _Diff_Colour_Tint_Blue ("-- Diff Colour Tint Blue", Color) = (1,1,1,0)
        _Blue_UV_Scale_Offset ("-- UV/Offset Blue", Vector) = (1,1,0,0)
        _Smoothness_Intensity_Blue ("-- Smoothness Intensity Blue", Range(0, 1)) = 1
        _Metal_Intensity_Blue ("-- Metal Intensity Blue", Range(0, 1)) = 1
        _Separator_4 ("# (4)", Float) = 0
        _Title_Surface_Inputs_ALPHA ("## Surface Inputs : ALPHA", Float) = 0
        _Diff_Colour_Tint_Alpha ("-- Diff Colour Tint Alpha", Color) = (1,1,1,0)
        _Alpha_UV_Scale_Offset ("-- UV/Offset Alpha", Vector) = (1,1,0,0)
        _Smoothness_Intensity_Alpha ("-- Smoothness Intensity Alpha", Range(0, 1)) = 1
        _Metal_Intensity_Alpha ("-- Metal Intensity Alpha", Range(0, 1)) = 1
        _Separator_5 ("# (5)", Float) = 0
        [HideInInspector]_QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl ("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" "Queue"="Geometry" "DisableBatching"="False" "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalLitSubTarget" }

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float _Title_Splatmap_Mask_Texture;
            float _Title_Surface_Inputs_RED;
            float _Title_Texture2DArray;
            float _Separator_1;
            float _Title_Surface_Inputs_GREEN;
            float _Smoothness_Intensity_Green;
            float _Smoothness_Intensity_Blue;
            float _ArrayIndex_Alpha;
            float _Smoothness_Intensity_Red;
            float _Metal_Intensity_Green;
            float _Metal_Intensity_Blue;
            float _Metal_Intensity_Red;
            float _Separator_2;
            float4 _Green_UV_Scale_Offset;
            float4 _Diff_Colour_Tint_Blue;
            float _Separator_3;
            float _Title_Surface_Inputs_BLUE;
            float4 _Blue_UV_Scale_Offset;
            float4 _Diff_Colour_Tint_Red;
            float4 _Diff_Colour_Tint_Green;
            float _Separator_4;
            float _Separator;
            float4 _Red_UV_Scale_Offset;
            float _ArrayIndex_Green;
            float _ArrayIndex_Blue;
            float _Smoothness_Intensity_Alpha;
            float _Separator_5;
            float _Metal_Intensity_Alpha;
            float4 _Diff_Colour_Tint_Alpha;
            float _Title_Surface_Inputs_ALPHA;
            float4 _Alpha_UV_Scale_Offset;
            float _ArrayIndex_Red;
            float4 _Splatmap_Mask_Texture_TexelSize;
            float _NOTE_All_textures_in_each_Array_type_must_have_the_same_texture_import_settings;
        CBUFFER_END

        TEXTURE2D(_Splatmap_Mask_Texture);
        TEXTURE2D_ARRAY(_Diff_Texture2DArray);
        TEXTURE2D_ARRAY(_Normal_Texture2DArray);
        TEXTURE2D_ARRAY(_MS_Texture2DArray);
        // The graph samples every texture through one inline bilinear repeat sampler.
        SAMPLER(SamplerState_Linear_Repeat);

        // Each layer's UV (scaled and offset) and rounded array slice.
        float3 RedLayer(float2 uv) { return float3(uv * _Red_UV_Scale_Offset.xy + _Red_UV_Scale_Offset.zw, round(_ArrayIndex_Red)); }
        float3 GreenLayer(float2 uv) { return float3(uv * _Green_UV_Scale_Offset.xy + _Green_UV_Scale_Offset.zw, round(_ArrayIndex_Green)); }
        float3 BlueLayer(float2 uv) { return float3(uv * _Blue_UV_Scale_Offset.xy + _Blue_UV_Scale_Offset.zw, round(_ArrayIndex_Blue)); }
        float3 AlphaLayer(float2 uv) { return float3(uv * _Alpha_UV_Scale_Offset.xy + _Alpha_UV_Scale_Offset.zw, round(_ArrayIndex_Alpha)); }

        // The mask's green, blue and alpha lerp the green, blue and alpha layers over the red one, in that order.
        float3 SplatWeights(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_Splatmap_Mask_Texture, SamplerState_Linear_Repeat, uv).yzw;
        }

        // The arrays are sampled without the global mip bias, as in the original.
        #define SAMPLE_LAYER(textureArray, layer) PLATFORM_SAMPLE_TEXTURE2D_ARRAY(textureArray, SamplerState_Linear_Repeat, (layer).xy, (layer).z)

        float3 SplatAlbedo(float2 uv)
        {
            float3 weights = SplatWeights(uv);
            float3 albedo = SAMPLE_LAYER(_Diff_Texture2DArray, RedLayer(uv)).rgb * _Diff_Colour_Tint_Red.rgb;
            albedo = lerp(albedo, SAMPLE_LAYER(_Diff_Texture2DArray, GreenLayer(uv)).rgb * _Diff_Colour_Tint_Green.rgb, weights.x);
            albedo = lerp(albedo, SAMPLE_LAYER(_Diff_Texture2DArray, BlueLayer(uv)).rgb * _Diff_Colour_Tint_Blue.rgb, weights.y);
            return lerp(albedo, SAMPLE_LAYER(_Diff_Texture2DArray, AlphaLayer(uv)).rgb * _Diff_Colour_Tint_Alpha.rgb, weights.z);
        }

        // Each layer's AG or RG packed normal at full strength, blended without renormalising.
        float3 SplatNormalTS(float2 uv)
        {
            float3 weights = SplatWeights(uv);
            float3 normal = UnpackNormalmapRGorAG(SAMPLE_LAYER(_Normal_Texture2DArray, RedLayer(uv)));
            normal = lerp(normal, UnpackNormalmapRGorAG(SAMPLE_LAYER(_Normal_Texture2DArray, GreenLayer(uv))), weights.x);
            normal = lerp(normal, UnpackNormalmapRGorAG(SAMPLE_LAYER(_Normal_Texture2DArray, BlueLayer(uv))), weights.y);
            return lerp(normal, UnpackNormalmapRGorAG(SAMPLE_LAYER(_Normal_Texture2DArray, AlphaLayer(uv))), weights.z);
        }

        // Each layer's metal (red) and smoothness (green) times its intensities, blended and saturated.
        float2 SplatMetalSmoothness(float2 uv)
        {
            float3 weights = SplatWeights(uv);
            float2 ms = SAMPLE_LAYER(_MS_Texture2DArray, RedLayer(uv)).xy * float2(_Metal_Intensity_Red, _Smoothness_Intensity_Red);
            ms = lerp(ms, SAMPLE_LAYER(_MS_Texture2DArray, GreenLayer(uv)).xy * float2(_Metal_Intensity_Green, _Smoothness_Intensity_Green), weights.x);
            ms = lerp(ms, SAMPLE_LAYER(_MS_Texture2DArray, BlueLayer(uv)).xy * float2(_Metal_Intensity_Blue, _Smoothness_Intensity_Blue), weights.y);
            return saturate(lerp(ms, SAMPLE_LAYER(_MS_Texture2DArray, AlphaLayer(uv)).xy * float2(_Metal_Intensity_Alpha, _Smoothness_Intensity_Alpha), weights.z));
        }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend One Zero
            ZTest LEqual
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // The ShaderGraph Lit target computes fog per fragment from view depth.
            #define _FOG_FRAGMENT 1
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma multi_compile_fog
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 uv0 : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float4 uv : TEXCOORD3;
                float4 fogFactorAndVertexLight : TEXCOORD4;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD5;
                #endif
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 6);
                #if defined(DYNAMICLIGHTMAP_ON)
                float2 dynamicLightmapUV : TEXCOORD7;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv = input.uv0;
                float fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #endif
                output.fogFactorAndVertexLight = float4(fogFactor, VertexLighting(vertexInput.positionWS, normalInput.normalWS));
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                #if defined(DYNAMICLIGHTMAP_ON)
                output.dynamicLightmapUV = input.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                OUTPUT_SH(output.normalWS, output.vertexSH);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vertexInput);
                #endif
                return output;
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv.xy;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // The raw interpolated basis, as the original transforms the lighting normal before normalising.
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                inputData.tangentToWorld = float3x3(input.tangentWS.xyz, tangentSign * cross(input.normalWS, input.tangentWS.xyz), input.normalWS);
                float3 normalTS = SplatNormalTS(uv);
                inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, inputData.tangentToWorld));
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
                #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                #if defined(DYNAMICLIGHTMAP_ON)
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
                #else
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                #endif
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);

                float2 metalSmoothness = SplatMetalSmoothness(uv);
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = SplatAlbedo(uv);
                surfaceData.metallic = metalSmoothness.x;
                surfaceData.smoothness = metalSmoothness.y;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1;
                surfaceData.alpha = 1;
                #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0;
                outColor = color;
                #if defined(_WRITE_RENDERING_LAYERS)
                outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZTest LEqual
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, output.normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZTest LEqual
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.zzzz;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZTest LEqual
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float4 uv0 : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 tangentWS : TEXCOORD0;
                float4 uv : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv = input.uv0;
                output.normalWS = normalInput.normalWS;
                return output;
            }

            void Frag(Varyings input, out float4 outNormalWS : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, tangentSign * cross(input.normalWS, input.tangentWS.xyz), input.normalWS);
                outNormalWS = float4(NormalizeNormalPerPixel(TransformTangentToWorld(SplatNormalTS(input.uv.xy), tangentToWorld)), 0.0);
                #if defined(_WRITE_RENDERING_LAYERS)
                outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                #if defined(EDITOR_VISUALIZATION)
                float2 vizUV : TEXCOORD3;
                float4 lightCoord : TEXCOORD4;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionOS = input.positionOS.xyz;
                output.positionCS = UnityMetaVertexPosition(positionOS, input.uv1.xy, input.uv2.xy);
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(positionOS, input.uv0.xy, input.uv1.xy, input.uv2.xy, output.vizUV, output.lightCoord);
                #endif
                return output;
            }

            // The blended diffuse is the albedo; there is no emission.
            float4 Frag(Varyings input) : SV_Target
            {
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = SplatAlbedo(input.uv0.xy);
                metaInput.Emission = 0;
                #if defined(EDITOR_VISUALIZATION)
                metaInput.VizUV = input.vizUV;
                metaInput.LightCoord = input.lightCoord;
                #endif
                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
