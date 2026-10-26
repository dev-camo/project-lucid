// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Character/HL_Char_Boss_03_Body, a URP 14 Lit Shader Graph (platform 14), transparent
// (alpha 1) with depth writes. The vertices are displaced along their normals by a scrolling texture (masked by the
// ID mask's red); the diffuse and emissive are flow-mapped, the diffuse fades to a colour near the scene depth and to
// the fresnel colour at grazing angles, and a scrolling noise normal map (masked by the ID mask's green) is blended
// over the normal map.
Shader "HardLight/Character/HL_Char_Boss_03_Body"
{
    Properties
    {
        _Separator_0 ("#", Float) = 0
        _Documentation_Link_Below ("## Documentation Link Below : HL_Boss_03_Body", Float) = 0
        _Documentation_Link ("[https://hardlight.atlassian.net/wiki/spaces/HL/pages/8340855/SDT+-+HL+Boss+03+Body](https://hardlight.atlassian.net/wiki/spaces/HL/pages/8340855/SDT+-+HL+Boss+03+Body)", Float) = 0
        _Separator_1 ("# (1)", Float) = 0
        _Title_Surface_Inputs ("## Surface Inputs", Float) = 0
        [NoScaleOffset]_Diff_Texture ("-- Diff Texture", 2D) = "white" {}
        _Separator_2 ("# (2)", Float) = 0
        _Title_IDMask ("## IDMask", Float) = 0
        _NOTE_R_Displacement_Mask ("!NOTE R= Displacement Mask", Float) = 0
        [NoScaleOffset]_IDMask_Texture ("-- IDMask Texture", 2D) = "white" {}
        _Separator_3 ("# (3)", Float) = 0
        _Title_MS ("## MS", Float) = 0
        _NOTE_R_Metal_G_Smoothness ("!NOTE R=Metal, G=Smoothness", Float) = 0
        [NoScaleOffset]_MS_Texture ("-- MS Texture", 2D) = "white" {}
        _Metal_Intensity ("-- Metal Intensity", Range(0, 2)) = 0.5
        _Smoothness_Intensity ("-- Smoothness Intensity", Range(0, 2)) = 0.5
        _Separator_4 ("# (4)", Float) = 0
        _Title_Emissive ("## Emissive", Float) = 0
        [NoScaleOffset]_Emissive_Texture ("-- Emissive Texture", 2D) = "white" {}
        _Emissive_Intensity ("-- Emissive Intensity", Range(0, 4)) = 1
        _Separator_5 ("# (5)", Float) = 0
        _Title_Normal ("## Normal", Float) = 0
        [NoScaleOffset][Normal]_Norm_Texture ("-- Norm Texture", 2D) = "bump" {}
        _Normal_Intensity ("-- Normal Intensity", Range(0, 2)) = 1
        _Title_Normal_Noise ("## Normal Noise", Float) = 0
        [Normal]_Norm_Noise_Texture ("-- Norm Noise Texture", 2D) = "bump" {}
        _Normal_Noise_Intensity ("-- Norm Noise Intensity", Range(0, 2)) = 1
        _Norm_Noise_Speed_Direction_X ("-- Norm Noise Speed/Direction X", Float) = 0
        _Norm_Noise_Speed_Direction_Y ("-- Norm Noise Speed/Direction Y", Float) = 0
        _Separator_6 ("# (6)", Float) = 0
        _Title_Fresnel ("## Fresnel", Float) = 0
        [HDR]_Fresnel_Colour ("-- Fresnel Colour", Color) = (0,0,0,0)
        _Fresnel_Amount ("-- Fresnel Amount", Float) = 1
        _Fresnel_Intensity ("-- Fresnel Intensity", Float) = 1
        _Fresnel_Opacity ("-- Fresnel Opacity", Range(0, 1)) = 1
        _Separator_7 ("# (7)", Float) = 0
        _Title_Displacement ("## Displacement", Float) = 0
        [NoScaleOffset]_Displacement_Texture ("-- Displacement  Texture", 2D) = "white" {}
        _Displacement_Scale ("-- Displacement Scale", Float) = 0.1
        _Displacement_Speed_X ("-- Displacement Speed X", Float) = 0.1
        _Displacement_Speed_Y ("-- Displacement Speed Y", Float) = 0.1
        _Displacement_Amount ("-- Displacement Amount", Float) = 0.3
        _Separator_8 ("# (8)", Float) = 0
        _Title_Depth_Fade ("## Depth Fade", Float) = 0
        _Depth_Fade_Distance ("-- Depth Fade Distance", Float) = 0
        _Depth_Fade_Colour ("-- Depth Fade Colour", Color) = (0,0,0,0)
        _Separator_9 ("# (9)", Float) = 0
        _Title_FlowMap ("## FlowMap", Float) = 0
        _FlowMap_Strength ("-- FlowMap Strength", Range(0, 1)) = 0
        _FlowMap_Speed ("-- FlowMap Speed", Range(0, 1)) = 0
        [NoScaleOffset]_FlowMap_Texture ("-- FlowMap Texture", 2D) = "linearGrey" {}
        _Title_FlowMap_Documentation ("## FlowMap Documentation", Float) = 0
        _FlowMap_Documentation_Link ("[https://confluence.eu.sega.internal/display/HL/Create+Flow+Maps+in+Blender](https://confluence.eu.sega.internal/display/HL/Create+Flow+Maps+in+Blender)", Float) = 0
        _Separator_10 ("# (10)", Float) = 0
        [HideInInspector]_QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl ("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "UniversalMaterialType"="Lit" "Queue"="Transparent" "DisableBatching"="False" "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalLitSubTarget" }

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float _Separator_7;
            float _Documentation_Link;
            float _Documentation_Link_Below;
            float _Separator_6;
            float _Title_Surface_Inputs;
            float _Title_IDMask;
            float _NOTE_R_Displacement_Mask;
            float4 _Displacement_Texture_TexelSize;
            float4 _MS_Texture_TexelSize;
            float _Title_MS;
            float _NOTE_R_Metal_G_Smoothness;
            float4 _Diff_Texture_TexelSize;
            float _Smoothness_Intensity;
            float _Norm_Noise_Speed_Direction_Y;
            float _Title_Emissive;
            float4 _Emissive_Texture_TexelSize;
            float4 _IDMask_Texture_TexelSize;
            float _Separator_0;
            float _Fresnel_Opacity;
            float _Normal_Noise_Intensity;
            float _Title_Normal_Noise;
            float _Fresnel_Intensity;
            float _Fresnel_Amount;
            float4 _Fresnel_Colour;
            float4 _Norm_Texture_TexelSize;
            float _Title_Fresnel;
            float _Separator_1;
            float _Emissive_Intensity;
            float _Metal_Intensity;
            float _Displacement_Scale;
            float _Displacement_Amount;
            float _Separator_2;
            float _Title_Displacement;
            float _FlowMap_Strength;
            float _Displacement_Speed_X;
            float _Separator_8;
            float _Displacement_Speed_Y;
            float _FlowMap_Speed;
            float4 _FlowMap_Texture_TexelSize;
            float _Separator_3;
            float _Title_Depth_Fade;
            float4 _Depth_Fade_Colour;
            float _Separator_9;
            float _Depth_Fade_Distance;
            float _Separator_4;
            float _Title_FlowMap;
            float _Separator_10;
            float _FlowMap_Documentation_Link;
            float _Normal_Intensity;
            float4 _Norm_Noise_Texture_TexelSize;
            float4 _Norm_Noise_Texture_ST;
            float _Norm_Noise_Speed_Direction_X;
            float _Title_FlowMap_Documentation;
            float _Separator_5;
            float _Title_Normal;
        CBUFFER_END

        TEXTURE2D(_Diff_Texture);
        TEXTURE2D(_MS_Texture);
        TEXTURE2D(_Norm_Texture);
        TEXTURE2D(_Norm_Noise_Texture);
        TEXTURE2D(_Emissive_Texture);
        TEXTURE2D(_FlowMap_Texture);
        TEXTURE2D(_Displacement_Texture);
        TEXTURE2D(_IDMask_Texture);
        SAMPLER(sampler_IDMask_Texture);
        TEXTURE2D_X_FLOAT(_CameraDepthTexture);
        SAMPLER(sampler_CameraDepthTexture);
        // The graph's inline linear repeat sampler.
        SAMPLER(SamplerState_Linear_Repeat);
        float _HLShaderTimeGame;

        // The object-space position displaced along the normal by the displacement texture's red (at UV1 times the
        // scale, scrolled by the speeds) times the amount, back towards the original where the ID mask's red is set.
        float3 Boss3PositionOS(float3 positionOS, float3 normalOS, float2 uv0, float2 uv1)
        {
            float2 displacementUV = uv1 * _Displacement_Scale + _HLShaderTimeGame * float2(_Displacement_Speed_X, _Displacement_Speed_Y);
            float displacement = SAMPLE_TEXTURE2D_LOD(_Displacement_Texture, SamplerState_Linear_Repeat, displacementUV, 0).x * _Displacement_Amount;
            float3 displaced = positionOS + displacement * normalOS;
            return lerp(displaced, positionOS, SAMPLE_TEXTURE2D_LOD(_IDMask_Texture, sampler_IDMask_Texture, uv0, 0).x);
        }

        // Shader Graph's Normal Strength node.
        float3 NormalStrength(float3 normal, float strength)
        {
            return float3(normal.xy * strength, lerp(1.0, normal.z, saturate(strength)));
        }

        // The normal map at its intensity blended (Shader Graph's Normal Blend) with the noise normal map at its
        // intensity (UV1 through its tiling and offset, scrolled), faded out by the ID mask's green.
        float3 Boss3NormalTS(float2 uv0, float2 uv1)
        {
            float3 base = NormalStrength(UnpackNormalmapRGorAG(SAMPLE_TEXTURE2D(_Norm_Texture, SamplerState_Linear_Repeat, uv0)), _Normal_Intensity);
            float2 noiseUV = uv1 * _Norm_Noise_Texture_ST.xy + _Norm_Noise_Texture_ST.zw + _HLShaderTimeGame * float2(_Norm_Noise_Speed_Direction_X, _Norm_Noise_Speed_Direction_Y);
            float3 noise = NormalStrength(UnpackNormalmapRGorAG(SAMPLE_TEXTURE2D(_Norm_Noise_Texture, SamplerState_Linear_Repeat, noiseUV)), _Normal_Noise_Intensity);
            noise = lerp(noise, float3(0.0, 0.0, 1.0), SAMPLE_TEXTURE2D(_IDMask_Texture, SamplerState_Linear_Repeat, uv0).g);
            return SafeNormalize(float3(base.xy + noise.xy, base.z * noise.z));
        }

        // A texture flow-mapped along the flow map ((0.5 - xy) * 2 * strength): two reads offset by phases 0.5
        // of a cycle apart, crossfaded.
        #define BOSS3_FLOW(tex, uv) lerp(SAMPLE_TEXTURE2D(tex, SamplerState_Linear_Repeat, (uv) + flow * phaseA).rgb, \
            SAMPLE_TEXTURE2D(tex, SamplerState_Linear_Repeat, (uv) + flow * phaseB).rgb, abs(phaseA * 2.0 - 1.0))

        // The scene's eye depth at a fragment, as the decoded program derives it from the pixel position: linear from
        // the depth texture in perspective, through the inverse view-projection in orthographic.
        float Boss3SceneEyeDepth(float4 positionCS)
        {
            float y = _ProjectionParams.x < 0.0 ? _ScaledScreenParams.y - positionCS.y : positionCS.y;
            float2 screenUV = float2(positionCS.x, y) / _ScaledScreenParams.xy;
            #if UNITY_UV_STARTS_AT_TOP
            screenUV.y = 1.0 - screenUV.y;
            #endif
            float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).x;
            if (unity_OrthoParams.w == 1.0)
            {
                // Metal stores reversed 0..1 depth; GL stores 0..1 but its inverse VP expects -1..1.
                #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float3 positionWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                return abs(mul(UNITY_MATRIX_V, float4(positionWS, 1.0)).z);
            }
            return 1.0 / (_ZBufferParams.z * rawDepth + _ZBufferParams.w);
        }

        struct Boss3Surface
        {
            float3 albedo;
            float3 emission;
            float metallic;
            float smoothness;
        };

        // The surface from UV0, the world position, the mapped normal and the fragment position.
        Boss3Surface Boss3SurfaceInputs(float2 uv, float3 positionWS, float3 normalWS, float4 positionCS)
        {
            Boss3Surface surface;
            float2 flow = (SAMPLE_TEXTURE2D(_FlowMap_Texture, SamplerState_Linear_Repeat, uv).xy - 0.5) * -2.0 * _FlowMap_Strength;
            float phaseA = frac(_HLShaderTimeGame * _FlowMap_Speed);
            float phaseB = frac(_HLShaderTimeGame * _FlowMap_Speed + 0.5);
            float3 diffuse = BOSS3_FLOW(_Diff_Texture, uv);

            // Depth fade: towards the fade colour where the scene behind is within the fade distance (of the
            // fragment's clip w).
            float clipW = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0)).w;
            float fade = saturate((Boss3SceneEyeDepth(positionCS) - clipW) / _Depth_Fade_Distance);
            float3 colour = lerp(_Depth_Fade_Colour.rgb, diffuse, fade);

            // Fresnel: towards the fresnel colour at grazing angles.
            float3 viewWS = unity_OrthoParams.w == 0.0 ? normalize(_WorldSpaceCameraPos - positionWS) : UNITY_MATRIX_V[2].xyz;
            float facing = saturate(dot(normalize(normalWS), normalize(viewWS)));
            float fresnel = saturate(1.0 - pow(1.0 - facing, _Fresnel_Amount) * _Fresnel_Intensity * _Fresnel_Opacity);
            surface.albedo = lerp(_Fresnel_Colour.rgb, colour, fresnel);
            surface.emission = BOSS3_FLOW(_Emissive_Texture, uv) * _Emissive_Intensity;

            // Metal (red) and smoothness (green), each times its intensity.
            float2 metalSmoothness = SAMPLE_TEXTURE2D(_MS_Texture, SamplerState_Linear_Repeat, uv).xy;
            surface.metallic = saturate(metalSmoothness.x * _Metal_Intensity);
            surface.smoothness = saturate(metalSmoothness.y * _Smoothness_Intensity);
            return surface;
        }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest LEqual
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // The ShaderGraph Lit target computes fog per fragment from view depth.
            #define _FOG_FRAGMENT 1
            #define _SURFACE_TYPE_TRANSPARENT 1
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
                float4 uv1 : TEXCOORD1;
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
                float4 uv1 : TEXCOORD4;
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
                VertexPositionInputs vertexInput = GetVertexPositionInputs(Boss3PositionOS(input.positionOS.xyz, input.normalOS, input.uv0.xy, input.uv1.xy));
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv = input.uv0;
                output.uv1 = input.uv1;
                float fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #endif
                output.fogFactorAndVertexLight = float4(fogFactor, VertexLighting(vertexInput.positionWS, normalInput.normalWS));
                OUTPUT_LIGHTMAP_UV(input.uv1.xy, unity_LightmapST, output.staticLightmapUV);
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
                float3 normalTS = Boss3NormalTS(uv, input.uv1.xy);
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

                Boss3Surface surface = Boss3SurfaceInputs(uv, input.positionWS, inputData.normalWS, input.positionCS);
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = surface.albedo;
                surfaceData.metallic = surface.metallic;
                surfaceData.smoothness = surface.smoothness;
                surfaceData.emission = surface.emission;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1;
                surfaceData.alpha = 1;
                #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 uv0 : TEXCOORD0; float4 uv1 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
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
                float3 positionWS = TransformObjectToWorld(Boss3PositionOS(input.positionOS.xyz, input.normalOS, input.uv0.xy, input.uv1.xy));
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 uv0 : TEXCOORD0; float4 uv1 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(Boss3PositionOS(input.positionOS.xyz, input.normalOS, input.uv0.xy, input.uv1.xy));
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float4 uv0 : TEXCOORD0; float4 uv1 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 tangentWS : TEXCOORD0;
                float4 uv : TEXCOORD1;
                float4 uv1 : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
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
                output.positionCS = TransformObjectToHClip(Boss3PositionOS(input.positionOS.xyz, input.normalOS, input.uv0.xy, input.uv1.xy));
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv = input.uv0;
                output.uv1 = input.uv1;
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
                outNormalWS = float4(NormalizeNormalPerPixel(TransformTangentToWorld(Boss3NormalTS(input.uv.xy, input.uv1.xy), tangentToWorld)), 0.0);
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
                float4 tangentOS : TANGENT;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 tangentWS : TEXCOORD0;
                float4 uv0 : TEXCOORD1;
                float4 uv1 : TEXCOORD2;
                float4 uv2 : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float3 normalWS : TEXCOORD5;
                #if defined(EDITOR_VISUALIZATION)
                float2 vizUV : TEXCOORD6;
                float4 lightCoord : TEXCOORD7;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionOS = Boss3PositionOS(input.positionOS.xyz, input.normalOS, input.uv0.xy, input.uv1.xy);
                output.positionCS = UnityMetaVertexPosition(positionOS, input.uv1.xy, input.uv2.xy);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                output.positionWS = TransformObjectToWorld(positionOS);
                output.normalWS = normalInput.normalWS;
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(positionOS, input.uv0.xy, input.uv1.xy, input.uv2.xy, output.vizUV, output.lightCoord);
                #endif
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, tangentSign * cross(input.normalWS, input.tangentWS.xyz), input.normalWS);
                float3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(Boss3NormalTS(input.uv0.xy, input.uv1.xy), tangentToWorld));
                Boss3Surface surface = Boss3SurfaceInputs(input.uv0.xy, input.positionWS, normalWS, input.positionCS);
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = surface.albedo;
                metaInput.Emission = surface.emission;
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
