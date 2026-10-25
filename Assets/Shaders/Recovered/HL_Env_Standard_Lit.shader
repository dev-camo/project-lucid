// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Standard_Lit, a URP 14 Lit Shader Graph (platform 14).
// The graph's surface inputs are rebuilt from the Metal programs; lighting, GI, shadows, fog
// and depth/meta output use URP 14.0.11's own library functions, as the graph's generated passes do.
Shader "HardLight/Environment/HL_Env_Standard_Lit"
{
    Properties
    {
        _Documentation_Link_Below ("## Documentation Link Below : HL_Env_Standard", Float) = 0
        _Documentation_Link ("[https://hardlight.atlassian.net/wiki/spaces/HL/pages/8335266/SDT+-+HL+Env+Standard](https://hardlight.atlassian.net/wiki/spaces/HL/pages/8335266/SDT+-+HL+Env+Standard)", Float) = 0
        _Separator_0 ("#", Float) = 1
        _Title_Scrolling_UV ("## Scrolling UV", Float) = 1
        _REF_USE_SCROLLING_UVS ("!REF _USE_SCROLLING_UVS", Float) = 0
        [Toggle(_USE_SCROLLING_UVS)]_USE_SCROLLING_UVS ("Use Scrolling UVs?", Float) = 0
        _UV_Scrolling_Horizontal ("-- UV Scrolling Horizontal [_USE_SCROLLING_UVS]", Float) = 0
        _UV_Scrolling_Vertical ("-- UV Scrolling Vertical  [_USE_SCROLLING_UVS]", Float) = 0
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
        _Title_Fresnel ("## Fresnel", Float) = 1
        _REF_USE_FRESNEL ("!REF _USE_FRESNEL", Float) = 0
        [Toggle(_USE_FRESNEL)]_USE_FRESNEL ("Use Fresnel?", Float) = 0
        [HDR]_Fresnel_Colour ("-- Fresnel Colour [_USE_FRESNEL]", Color) = (1,1,1,0)
        _Fresnel_Amount ("-- Fresnel Amount [_USE_FRESNEL]", Range(0, 10)) = 5
        _Fresnel_Intensity ("-- Fresnel Intensity [_USE_FRESNEL]", Range(1, 10)) = 1
        _Fresnel_Opacity ("-- Fresnel Opacity [_USE_FRESNEL]", Range(0, 1)) = 0
        _Separator_5 ("# (5)", Float) = 1
        _Title_DetailMaps ("## Detail Maps", Float) = 1
        _REF_USE_DETAIL_MAPS ("!REF _USE_DETAIL_MAPS", Float) = 0
        [Toggle(_USE_DETAIL_MAPS)]_USE_DETAIL_MAPS ("Use Detail Maps?", Float) = 0
        _Detail_Diffuse_Tex ("-- Detail Diffuse Texture [_USE_DETAIL_MAPS]", 2D) = "white" {}
        [Normal]_Detail_Normal_Tex ("-- Detail Normal Texture [_USE_DETAIL_MAPS]", 2D) = "bump" {}
        _Separator_6 ("# (6)", Float) = 1
        _Title_Alpha ("## Alpha", Float) = 1
        _NOTE_Alpha_stored_in_Diff_Alpha_channel ("!NOTE Alpha stored in Diff Alpha channel", Float) = 0
        _NOTE_Remember_to_turn_on_Transparency_or_Alpha_Clipping ("!NOTE Remember to turn on Transparency or Alpha Clipping", Float) = 0
        _NOTE_in_Surface_Type_below_to_enable_these_properties ("!NOTE in Surface Type below to enable these properties.", Float) = 0
        _Alpha_Contrast ("-- Alpha Contrast [_Surface == 1]", Float) = 1
        _Alpha_Clip ("-- Alpha Clip  [_ALPHATEST_ON]", Range(0, 1)) = 0.5
        _Separator_7 ("# (7)", Float) = 1
        [HideInInspector]_WorkflowMode ("_WorkflowMode", Float) = 1
        [HideInInspector]_CastShadows ("_CastShadows", Float) = 1
        [HideInInspector]_ReceiveShadows ("_ReceiveShadows", Float) = 1
        [HideInInspector]_Surface ("_Surface", Float) = 0
        [HideInInspector]_Blend ("_Blend", Float) = 0
        [HideInInspector]_AlphaClip ("_AlphaClip", Float) = 0
        [HideInInspector]_BlendModePreserveSpecular ("_BlendModePreserveSpecular", Float) = 0
        [HideInInspector]_SrcBlend ("_SrcBlend", Float) = 1
        [HideInInspector]_DstBlend ("_DstBlend", Float) = 0
        [HideInInspector][ToggleUI]_ZWrite ("_ZWrite", Float) = 1
        [HideInInspector]_ZWriteControl ("_ZWriteControl", Float) = 0
        [HideInInspector]_ZTest ("_ZTest", Float) = 4
        [HideInInspector]_Cull ("_Cull", Float) = 2
        [HideInInspector]_AlphaToMask ("_AlphaToMask", Float) = 0
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
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float _REF_USE_DETAIL_MAPS;
            float _Separator_7;
            float _Title_Scrolling_UV;
            float _REF_USE_SCROLLING_UVS;
            float _Title_Diffuse;
            float _Separator_1;
            float _UV_Scrolling_Vertical;
            float _Separator_8;
            float _NOTE_Remember_to_turn_on_Transparency_or_Alpha_Clipping;
            float _REF_USE_FRESNEL;
            float _Title_MSEA;
            float4 _MSEA_Tex_TexelSize;
            float _Separator_2;
            float _NOTE_R_Metal_G_Smoothness_B_Emission_Optional_A_Alpha_Optional;
            float _Fresnel_Opacity;
            float _Fresnel_Intensity;
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
            float _Title_Fresnel;
            float4 _Fresnel_Colour;
            float _Fresnel_Amount;
            float4 _Emissive_Tex_TexelSize;
            float4 _Detail_Normal_Tex_TexelSize;
            float4 _Detail_Normal_Tex_ST;
            float _Title_DetailMaps;
            float _Separator_5;
            float4 _Detail_Diffuse_Tex_TexelSize;
            float4 _Detail_Diffuse_Tex_ST;
            float _Alpha_Contrast;
            float _Documentation_Link_Below;
            float _Documentation_Link;
            float _Separator_6;
            float _Title_Alpha;
            float _Separator_0;
            float _Alpha_Clip;
            float _UV_Scrolling_Horizontal;
            float _NOTE_Alpha_stored_in_Diff_Alpha_channel;
            float _NOTE_in_Surface_Type_below_to_enable_these_properties;
            float _USE_VERTEX_COLOUR_TINT;
        CBUFFER_END

        // Game-scaled shader time published by the time manager (_HLShaderTimeGame).
        float _HLShaderTimeGame;

        TEXTURE2D(_Diff_Tex);
        TEXTURE2D(_MSEA_Tex);
        TEXTURE2D(_Normal_Tex);
        TEXTURE2D(_Emissive_Tex);
        TEXTURE2D(_Detail_Diffuse_Tex);
        TEXTURE2D(_Detail_Normal_Tex);
        // The graph's single inline sampler: linear, repeat, 2x anisotropic.
        SAMPLER(SamplerState_Linear_Repeat_Aniso2);
        // Core.hlsl's SAMPLE_TEXTURE2D adds _GlobalMipBias.x once, as the original programs do.
        #define HL_SAMPLE(tex, uv) SAMPLE_TEXTURE2D(tex, SamplerState_Linear_Repeat_Aniso2, uv)

        // Main UV: Diff tiling and offset, plus game-time scrolling when enabled.
        float2 HLEnvMainUV(float2 uv0)
        {
            float2 offset = _Diff_Tex_ST.zw;
            #if defined(_USE_SCROLLING_UVS)
            offset += _HLShaderTimeGame * float2(_UV_Scrolling_Horizontal, _UV_Scrolling_Vertical);
            #endif
            return uv0 * _Diff_Tex_ST.xy + offset;
        }

        // The graph's alpha: the Diff alpha scaled by _Alpha_Contrast.
        float HLEnvAlpha(float2 uv)
        {
            return HL_SAMPLE(_Diff_Tex, uv).a * _Alpha_Contrast;
        }

        // Depth, shadow and meta passes clip against _Alpha_Clip under alpha test.
        void HLEnvClip(float2 uv)
        {
            #if defined(_ALPHATEST_ON)
            clip(HLEnvAlpha(uv) - _Alpha_Clip);
            #endif
        }

        // Tangent-space normal: the normal map, blended with the detail normal by the
        // graph's default Normal Blend (summed xy, multiplied z, normalized).
        float3 HLEnvNormalTS(float2 uv, float2 uv0)
        {
            float3 normalTS = UnpackNormal(HL_SAMPLE(_Normal_Tex, uv));
            #if defined(_USE_DETAIL_MAPS)
            float3 detail = UnpackNormal(HL_SAMPLE(_Detail_Normal_Tex, TRANSFORM_TEX(uv0, _Detail_Normal_Tex)));
            normalTS = SafeNormalize(float3(normalTS.xy + detail.xy, normalTS.z * detail.z));
            #endif
            return normalTS;
        }

        // The graph selects handedness from raw tangent.w and applies the object-scale sign once.
        float3x3 HLEnvTangentToWorld(float4 tangentWS, float3 normalWS)
        {
            return CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w > 0.0 ? 1.0 : -1.0);
        }

        float3 HLEnvGraphNormalWS(float3 normalTS, float4 tangentWS, float3 normalWS)
        {
            float3x3 tangentToWorld = HLEnvTangentToWorld(tangentWS, normalWS);
            // Shader Graph applies the reciprocal normal length to all three frame vectors before blending.
            float renormFactor = 1.0 / length(normalWS);
            tangentToWorld[0] *= renormFactor;
            tangentToWorld[1] *= renormFactor;
            tangentToWorld[2] *= renormFactor;
            return normalize(SafeNormalize(TransformTangentToWorld(normalTS, tangentToWorld)));
        }

        // Albedo: Diff (times the detail diffuse) times the tint, optionally vertex-colour tinted,
        // then pulled toward the fresnel colour at grazing angles when fresnel is enabled.
        float3 HLEnvAlbedo(float2 uv, float2 uv0, float4 vertexColor, float3 normalWS, float3 viewDirWS)
        {
            float3 albedo = HL_SAMPLE(_Diff_Tex, uv).rgb;
            #if defined(_USE_DETAIL_MAPS)
            albedo *= HL_SAMPLE(_Detail_Diffuse_Tex, TRANSFORM_TEX(uv0, _Detail_Diffuse_Tex)).rgb;
            #endif
            albedo *= _Diff_Tint.rgb;
            albedo = lerp(albedo, albedo * vertexColor.rgb, _USE_VERTEX_COLOUR_TINT);
            #if defined(_USE_FRESNEL)
            float fresnel = pow(1.0 - saturate(dot(normalWS, normalize(viewDirWS))), _Fresnel_Amount) * _Fresnel_Intensity;
            albedo = lerp(_Fresnel_Colour.rgb, albedo, saturate(1.0 - fresnel * _Fresnel_Opacity));
            #endif
            return albedo;
        }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend], [_SrcBlend] [_DstBlend]
            ZTest [_ZTest]
            ZWrite [_ZWrite]
            Cull [_Cull]
            AlphaToMask [_AlphaToMask]

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
            #pragma shader_feature_fragment _ _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _ _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _ _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma shader_feature_local _ _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_vertex _ _USE_SCROLLING_UVS
            #pragma shader_feature_local_fragment _ _USE_FRESNEL
            #pragma shader_feature_local_fragment _ _USE_DETAIL_MAPS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 uv0 : TEXCOORD0;
                float4 staticLightmapUV : TEXCOORD1;
                float4 dynamicLightmapUV : TEXCOORD2;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 uv0 : TEXCOORD9;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float4 color : TEXCOORD4;
                float4 fogFactorAndVertexLight : TEXCOORD5;
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
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv = HLEnvMainUV(input.uv0.xy);
                output.uv0 = input.uv0;
                output.color = input.color;
                half fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #endif
                output.fogFactorAndVertexLight = float4(fogFactor, VertexLighting(vertexInput.positionWS, normalInput.normalWS));
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV.xy, unity_LightmapST, output.staticLightmapUV);
                #if defined(DYNAMICLIGHTMAP_ON)
                output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
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
                float2 uv = input.uv;
                float2 uv0 = input.uv0.xy;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.tangentToWorld = HLEnvTangentToWorld(input.tangentWS, input.normalWS);
                float3 normalTS = HLEnvNormalTS(uv, uv0);
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

                // Pinned URP Shader Graph debug inputs follow the same lightmap/SH branches as baked GI.
                #if defined(DEBUG_DISPLAY)
                #if defined(DYNAMICLIGHTMAP_ON)
                inputData.dynamicLightmapUV = input.dynamicLightmapUV;
                #endif
                #if defined(LIGHTMAP_ON)
                inputData.staticLightmapUV = input.staticLightmapUV;
                #else
                inputData.vertexSH = input.vertexSH;
                #endif
                #endif

                // Retain the generated pass's alpha-to-coverage sharpening before surface saturation.
                // Transparency uses graph alpha; opaque surfaces without alpha testing use 1.
                #if defined(_ALPHATEST_ON)
                half alpha = HLEnvAlpha(uv);
                alpha = AlphaDiscard(alpha, _Alpha_Clip);
                #elif defined(_SURFACE_TYPE_TRANSPARENT)
                half alpha = HLEnvAlpha(uv);
                #else
                half alpha = 1;
                #endif
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.alpha = saturate(alpha);
                float3 graphNormalWS = inputData.normalWS;
                #if defined(_USE_FRESNEL)
                graphNormalWS = HLEnvGraphNormalWS(normalTS, input.tangentWS, input.normalWS);
                #endif
                surfaceData.albedo = HLEnvAlbedo(uv, uv0, input.color, graphNormalWS, inputData.viewDirectionWS);
                half2 metalSmooth = saturate(HL_SAMPLE(_MSEA_Tex, uv).rg * half2(_Metal_Intensity, _Smoothness_Intensity));
                surfaceData.metallic = metalSmooth.x;
                surfaceData.smoothness = metalSmooth.y;
                surfaceData.normalTS = normalTS;
                surfaceData.emission = HL_SAMPLE(_Emissive_Tex, uv).rgb * _Emissive_Colour.rgb;
                surfaceData.occlusion = 1;
                #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                #if defined(_SURFACE_TYPE_TRANSPARENT)
                color.a = OutputAlpha(color.a, true);
                #else
                color.a = OutputAlpha(color.a, false);
                #endif
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _USE_FRESNEL
            #pragma shader_feature_local_fragment _ _USE_DETAIL_MAPS
            #pragma shader_feature_local_vertex _ _USE_SCROLLING_UVS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; float4 uv0 : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
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
                output.positionCS = positionCS;
                output.normalWS = normalWS;
                output.uv = HLEnvMainUV(input.uv0.xy);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                HLEnvClip(input.uv);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _USE_FRESNEL
            #pragma shader_feature_local_fragment _ _USE_DETAIL_MAPS
            #pragma shader_feature_local_vertex _ _USE_SCROLLING_UVS

            struct Attributes { float3 positionOS : POSITION; float4 uv0 : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = HLEnvMainUV(input.uv0.xy);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                HLEnvClip(input.uv);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma shader_feature_local_vertex _ _USE_SCROLLING_UVS
            #pragma shader_feature_local_fragment _ _USE_FRESNEL
            #pragma shader_feature_local_fragment _ _USE_DETAIL_MAPS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float4 uv0 : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 uv0 : TEXCOORD3;
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
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = HLEnvMainUV(input.uv0.xy);
                output.uv0 = input.uv0;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
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
                HLEnvClip(input.uv);
                float3 normalTS = HLEnvNormalTS(input.uv, input.uv0.xy);
                float3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, HLEnvTangentToWorld(input.tangentWS, input.normalWS)));
                outNormalWS = float4(normalWS, 0.0);
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
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma shader_feature_local_vertex _ _USE_SCROLLING_UVS
            #pragma shader_feature_local_fragment _ _USE_FRESNEL
            #pragma shader_feature_local_fragment _ _USE_DETAIL_MAPS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

            struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float4 uv0 : TEXCOORD0; float4 uv1 : TEXCOORD1; float4 uv2 : TEXCOORD2; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float4 tangentWS : TEXCOORD4;
                float4 uv0 : TEXCOORD5;
                float4 uv1 : TEXCOORD6;
                float4 uv2 : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                #if defined(EDITOR_VISUALIZATION)
                float2 VizUV : TEXCOORD8;
                float4 LightCoord : TEXCOORD9;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1.xy, input.uv2.xy);
                output.uv = HLEnvMainUV(input.uv0.xy);
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                output.color = input.color;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(input.positionOS.xyz, input.uv0.xy, input.uv1.xy, input.uv2.xy, output.VizUV, output.LightCoord);
                #endif
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                HLEnvClip(input.uv);
                float3 graphNormalWS = input.normalWS;
                #if defined(_USE_FRESNEL)
                graphNormalWS = HLEnvGraphNormalWS(HLEnvNormalTS(input.uv, input.uv0.xy), input.tangentWS, input.normalWS);
                #endif
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = HLEnvAlbedo(input.uv, input.uv0.xy, input.color, graphNormalWS,
                                               GetWorldSpaceNormalizeViewDir(input.positionWS));
                metaInput.Emission = HL_SAMPLE(_Emissive_Tex, input.uv).rgb * _Emissive_Colour.rgb;
                #if defined(EDITOR_VISUALIZATION)
                metaInput.VizUV = input.VizUV;
                metaInput.LightCoord = input.LightCoord;
                #endif
                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
