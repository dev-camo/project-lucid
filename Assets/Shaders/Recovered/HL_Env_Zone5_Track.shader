// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Zone5_Track, a URP 14 Lit Shader Graph (platform 14),
// opaque. The graph's surface inputs are rebuilt from the Metal programs;
// lighting, GI, shadows, fog and depth/meta output use URP 14.0.11's own library functions, as the
// graph's generated passes do.
Shader "HardLight/Environment/HL_Env_Zone5_Track"
{
    Properties
    {
        _Separator_0 ("#", Float) = 1
        _Title_Scrolling_UV ("## Scrolling UV", Float) = 1
        _UV_Scrolling_Horizontal ("-- UV Scrolling Horizontal", Float) = 0.01
        _UV_Scrolling_Vertical ("-- UV Scrolling Vertical", Float) = 0
        _Separator_8 ("# (8)", Float) = 1
        _Title_Diffuse ("## Diffuse", Float) = 1
        _Diff_Tex ("-- Diff Texture", 2D) = "white" {}
        _Diff_Tint ("-- Diff Colour Tint", Color) = (1,1,1,0)
        [ToggleUI]_USE_VERTEX_COLOUR_TINT ("-- Use Vertex Colour Tint?", Float) = 0
        _Separator_1 ("# (1)", Float) = 1
        _Title_MSEA ("## MS", Float) = 1
        _NOTE_R_Metal_G_Smoothness_B_Emission_Optional_A_Alpha_Optional ("!NOTE R = Metal, G = Smoothness", Float) = 0
        [NoScaleOffset]_MSEA_Tex ("-- MS Texture", 2D) = "white" {}
        _Metal_Intensity ("-- Metal Intensity", Range(0, 1)) = 0
        _Smoothness_Intensity ("-- Smoothness Intensity", Range(0, 1)) = 1
        _Separator_2 ("# (2)", Float) = 1
        _Title_Normal ("## Normal", Float) = 1
        [NoScaleOffset][Normal]_Normal_Tex ("-- Norm Texture", 2D) = "bump" {}
        _Separator_3 ("# (3)", Float) = 1
        _Title_Emission ("## Emission", Float) = 1
        [NoScaleOffset]_Emissive_Tex ("-- Emissive Texture", 2D) = "white" {}
        [HDR]_Emissive_Colour ("-- Emissive Colour", Color) = (0,0,0,0)
        _Separator_4 ("# (4)", Float) = 1
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
            float _Title_Scrolling_UV;
            float _Title_Diffuse;
            float _Separator_1;
            float _UV_Scrolling_Vertical;
            float _Separator_8;
            float _Title_MSEA;
            float4 _MSEA_Tex_TexelSize;
            float _Separator_2;
            float _NOTE_R_Metal_G_Smoothness_B_Emission_Optional_A_Alpha_Optional;
            float _Title_Normal;
            float4 _Normal_Tex_TexelSize;
            float _Separator_3;
            float _Title_Emission;
            float4 _Diff_Tex_TexelSize;
            float4 _Diff_Tex_ST;
            float4 _Emissive_Colour;
            float _Smoothness_Intensity;
            float _Metal_Intensity;
            float4 _Diff_Tint;
            float _Separator_4;
            float4 _Emissive_Tex_TexelSize;
            float _Separator_0;
            float _UV_Scrolling_Horizontal;
            float _USE_VERTEX_COLOUR_TINT;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;

        TEXTURE2D(_Diff_Tex);
        SAMPLER(sampler_Diff_Tex);
        TEXTURE2D(_MSEA_Tex);
        TEXTURE2D(_Normal_Tex);
        TEXTURE2D(_Emissive_Tex);
        // The graph's inline sampler: linear, repeat, 2x anisotropic.
        SAMPLER(SamplerState_Linear_Repeat_Aniso2);
        #define TRACK_SAMPLE(tex, uv) SAMPLE_TEXTURE2D(tex, SamplerState_Linear_Repeat_Aniso2, uv)

        // xy: the Diff-tiled UV scrolled by game time; zw: the same UV without scrolling.
        float4 TrackUVs(float2 uv0)
        {
            float2 tiled = uv0 * _Diff_Tex_ST.xy + _Diff_Tex_ST.zw;
            return float4(tiled + _HLShaderTimeGame * float2(_UV_Scrolling_Horizontal, _UV_Scrolling_Vertical), tiled);
        }

        // Diffuse and emission scroll only where the Diff alpha (read at the static UV tiled once more,
        // through the texture's own sampler) is at least 0.001.
        float2 TrackColourUV(float4 uvs)
        {
            float mask = SAMPLE_TEXTURE2D(_Diff_Tex, sampler_Diff_Tex, uvs.zw * _Diff_Tex_ST.xy + _Diff_Tex_ST.zw).a;
            return mask >= 0.001 ? uvs.xy : uvs.zw;
        }

        float3 TrackAlbedo(float4 uvs, float4 vertexColor)
        {
            float3 albedo = TRACK_SAMPLE(_Diff_Tex, TrackColourUV(uvs)).rgb * _Diff_Tint.rgb;
            return lerp(albedo, vertexColor.rgb * albedo, _USE_VERTEX_COLOUR_TINT);
        }

        float3 TrackEmission(float4 uvs)
        {
            return TRACK_SAMPLE(_Emissive_Tex, TrackColourUV(uvs)).rgb * _Emissive_Colour.rgb;
        }

        // Metal and smoothness, and the normal map, always read the static UV.
        float2 TrackMetalSmooth(float4 uvs)
        {
            return saturate(TRACK_SAMPLE(_MSEA_Tex, uvs.zw).rg * float2(_Metal_Intensity, _Smoothness_Intensity));
        }

        float3 TrackNormalTS(float4 uvs)
        {
            return UnpackNormal(TRACK_SAMPLE(_Normal_Tex, uvs.zw));
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
                float2 uv0 : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float4 color : TEXCOORD3;
                float4 fogFactorAndVertexLight : TEXCOORD4;
                float4 uv : TEXCOORD5;            // xy: scrolled UV, zw: static UV
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD6;
                #endif
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
                #if defined(DYNAMICLIGHTMAP_ON)
                float2 dynamicLightmapUV : TEXCOORD8;
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
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.color = input.color;
                output.uv = TrackUVs(input.uv0);
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

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                inputData.tangentToWorld = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 normalTS = TrackNormalTS(input.uv);
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

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = TrackAlbedo(input.uv, input.color);
                float2 metalSmooth = TrackMetalSmooth(input.uv);
                surfaceData.metallic = metalSmooth.x;
                surfaceData.smoothness = metalSmooth.y;
                surfaceData.normalTS = normalTS;
                surfaceData.emission = TrackEmission(input.uv);
                surfaceData.occlusion = 1;
                surfaceData.alpha = 1;
                #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = OutputAlpha(color.a, false);
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

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }
            float4 Frag() : SV_Target { return 0; }
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

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }
            float4 Frag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.zzzz; }
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv0 : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TrackUVs(input.uv0);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
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
                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 normalTS = TrackNormalTS(input.uv);
                outNormalWS = float4(NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld)), 0.0);
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
                float3 normalOS : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : TEXCOORD0;
                float4 uv : TEXCOORD1;
                #if defined(EDITOR_VISUALIZATION)
                float2 vizUV : TEXCOORD3;
                float4 lightCoord : TEXCOORD4;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
                output.uv = TrackUVs(input.uv0);
                output.color = input.color;
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(input.positionOS.xyz, input.uv0, input.uv1, input.uv2, output.vizUV, output.lightCoord);
                #endif
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = TrackAlbedo(input.uv, input.color);
                metaInput.Emission = TrackEmission(input.uv);
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
