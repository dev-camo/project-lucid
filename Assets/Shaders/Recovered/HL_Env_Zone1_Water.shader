// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Zone1_Water, a URP 14 Lit Shader Graph (platform 14), transparent
// with depth writes. A depth colour ramp with colour variation, colour-dodged by the normal-refracted scene colour,
// under depth-edge and vertex-colour foam; vertex green fades the alpha. The shipped forward pass applies the graph's
// own fog blend to the albedo and no fog to the lit output.
Shader "HardLight/Environment/HL_Env_Zone1_Water"
{
    Properties
    {
        _Separator_0 ("#", Float) = 0
        _FOLDOUT_Vertex_Colour_Channel_Info ("# Vertex Colour Channel Info", Float) = 0
        _NOTE_VC_Red_Foam ("### VC Red = Foam", Float) = 0
        _NOTE_VC_Green_Alpha ("### VC Green = Alpha", Float) = 0
        _NOTE_VC_Blue ("### VC Blue =", Float) = 0
        _NOTE_VC_Alpha ("### VC Alpha =", Float) = 0
        _FOLDOUT_Packed_Noise_Channel_Info ("# Packed Noise Channel Info", Float) = 0
        _NOTE_R_Foam ("### R = Foam", Float) = 0
        _NOTE_G_Colour_Variation ("### G = Colour Variation", Float) = 0
        _NOTE_B ("### B =", Float) = 0
        _NOTE_A ("### A =", Float) = 0
        _Separator_1 ("# (1)", Float) = 0
        _Title_Surface_Input ("## Surface Input", Float) = 0
        [NoScaleOffset]_Packed_Noise_Texture ("-- Packed Noise Texture", 2D) = "black" {}
        _Separator_2 ("# (2)", Float) = 0
        _Smoothness ("-- Smoothness", Range(0, 1)) = 1
        _Emission_Intensity ("-- Emission Intensity", Range(0, 2)) = 0
        _Title_Alpha ("### Alpha", Float) = 0
        _Overall_Alpha ("-- Overall Alpha", Range(0, 1)) = 1
        _Title_Fog ("### Fog", Float) = 0
        _Scene_Fog_Intensity ("-- Scene Fog Intensity", Range(0, 1)) = 0
        _Separator_3 ("# (3)", Float) = 0
        _Title_Colour ("## Colour", Float) = 0
        _Title_Depth ("### Depth", Float) = 0
        [NoScaleOffset]_Water_Colour_Gradient_Ramp_Texture ("-- Water Colour Gradient Ramp Texture", 2D) = "white" {}
        _Water_Depth_Distance ("-- Water Depth Distance", Float) = 10
        _Title_Colour_Varation ("### Colour Varation", Float) = 0
        _Color_Varation ("-- Color Varation", Color) = (0.9009557,0,1,0)
        _Color_Varation_Amount ("-- Color Varation Amount", Float) = 1
        _Color_Varation_UV_Tiling_X ("-- Color Varation UV Tiling X", Float) = 1
        _Color_Varation_UV_Tiling_Y ("-- Color Varation UV Tiling Y", Float) = 1
        _Colour_Variation_Speed_Direction_X ("-- Colour Variation Speed/Direction X", Float) = 0
        _Colour_Variation_Speed_Direction_Y ("-- Colour Variation Speed/Direction Y", Float) = 0.3
        _Separator_4 ("# (4)", Float) = 0
        _Title_Normal ("## Normal", Float) = 0
        [Normal]_Normal_Texture ("-- Normal Texture", 2D) = "bump" {}
        _Normal_Speed_Direction_X ("-- Normal Speed/Direction X", Float) = 0
        _Normal_Speed_Direction_Y ("-- Normal Speed/Direction Y", Float) = 0.09
        _Normal_Intensity ("-- Normal Intensity", Range(0, 2)) = 1
        _Separator_5 ("# (5)", Float) = 0
        _Title_Refraction ("## Refraction", Float) = 0
        _Refraction_Intensity ("-- Refraction Intensity", Range(0, 0.1)) = 0.05
        _Refraction_Colour_Blend_Amount ("-- Refraction Colour Blend Amount", Range(0, 1)) = 1
        _Separator_6 ("# (6)", Float) = 0
        _Title_Foam_Surface_Input ("## Foam Surface Input", Float) = 0
        _NOTE_Opacity_stored_in_colour_alpha_channel ("!NOTE Opacity stored in colour alpha channel.", Float) = 0
        _Foam_Color ("-- Foam Color", Color) = (0.9716981,0.9716981,0.9716981,1)
        _Title_Foam ("### Foam", Float) = 0
        _Foam_Depth_Fade ("-- Foam Depth Fade", Float) = 40
        _Foam_Step_Amount ("-- Foam Step Amount", Float) = -0.3
        _Foam_UV_Tiling_X ("-- Foam UV Tiling X", Float) = 1
        _Foam_UV_Tiling_Y ("-- Foam UV Tiling Y", Float) = 4
        _Foam_Speed_Direction_X ("-- Foam Speed/Direction X", Float) = 0
        _Foam_Speed_Direction_Y ("-- Foam Speed/Direction Y", Float) = -0.1
        _Title_Vertex_Colour_Foam ("### Vertex Colour Foam", Float) = 0
        _VC_Foam_Amount ("-- VC Foam Amount", Range(0, 3)) = 1
        _VC_Foam_UV_Tiling_X ("-- VC Foam UV Tiling X", Float) = 3
        _VC_Foam_UV_Tiling_Y ("-- VC Foam UV Tiling Y", Float) = 3
        _VC_Foam_Speed_Direction_X ("-- VC Foam Speed/Direction X", Float) = 0
        _VC_Foam_Speed_Direction_Y ("-- VC Foam Speed/Direction Y", Float) = 0.3
        _Separator_7 ("# (7)", Float) = 0
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
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float _Scene_Fog_Intensity;
            float _FOLDOUT_Vertex_Colour_Channel_Info;
            float _Separator_0;
            float _Separator_3;
            float _Title_Alpha;
            float _NOTE_VC_Blue;
            float _NOTE_VC_Alpha;
            float _NOTE_VC_Green_Alpha;
            float _FOLDOUT_Packed_Noise_Channel_Info;
            float _Separator_4;
            float _NOTE_R_Foam;
            float _NOTE_G_Colour_Variation;
            float _NOTE_B;
            float _NOTE_A;
            float _Title_Normal;
            float _Separator_2;
            float _Separator_1;
            float _Title_Foam;
            float _Title_Fog;
            float _Title_Surface_Input;
            float _Title_Colour_Varation;
            float _Colour_Variation_Speed_Direction_X;
            float _Colour_Variation_Speed_Direction_Y;
            float _Title_Colour;
            float _Normal_Intensity;
            float _Smoothness;
            float _Color_Varation_UV_Tiling_X;
            float _Color_Varation_UV_Tiling_Y;
            float _Color_Varation_Amount;
            float _Overall_Alpha;
            float4 _Normal_Texture_TexelSize;
            float4 _Normal_Texture_ST;
            float4 _Color_Varation;
            float _Emission_Intensity;
            float _Normal_Speed_Direction_X;
            float _Normal_Speed_Direction_Y;
            float4 _Foam_Color;
            float4 _Packed_Noise_Texture_TexelSize;
            float _NOTE_VC_Red_Foam;
            float _Title_Refraction;
            float _Foam_Speed_Direction_X;
            float _Foam_Speed_Direction_Y;
            float _Title_Foam_Surface_Input;
            float _Title_Depth;
            float _NOTE_Opacity_stored_in_colour_alpha_channel;
            float _Separator_7;
            float _VC_Foam_Amount;
            float _Water_Depth_Distance;
            float _Title_Vertex_Colour_Foam;
            float _Foam_UV_Tiling_X;
            float _Foam_UV_Tiling_Y;
            float _VC_Foam_UV_Tiling_X;
            float _VC_Foam_UV_Tiling_Y;
            float4 _Water_Colour_Gradient_Ramp_Texture_TexelSize;
            float _VC_Foam_Speed_Direction_Y;
            float _Separator_5;
            float _Separator_6;
            float _Foam_Depth_Fade;
            float _Refraction_Intensity;
            float _VC_Foam_Speed_Direction_X;
            float _Refraction_Colour_Blend_Amount;
            float _Foam_Step_Amount;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;

        TEXTURE2D(_Packed_Noise_Texture);
        TEXTURE2D(_Normal_Texture);
        TEXTURE2D(_Water_Colour_Gradient_Ramp_Texture);
        SAMPLER(sampler_Water_Colour_Gradient_Ramp_Texture);
        // The graph's inline sampler: linear, repeat.
        SAMPLER(SamplerState_Linear_Repeat);

        // The scrolling normal map, unpacked (the refraction offset reads its xy before the normal strength).
        float3 WaterNormalSample(float2 uv)
        {
            float2 normalUV = uv * _Normal_Texture_ST.xy + (_HLShaderTimeGame * float2(_Normal_Speed_Direction_X, _Normal_Speed_Direction_Y) + _Normal_Texture_ST.zw);
            return UnpackNormal(SAMPLE_TEXTURE2D(_Normal_Texture, SamplerState_Linear_Repeat, normalUV));
        }

        // The graph's Normal Strength: xy scaled, z lerped from 1 by the saturated strength.
        float3 WaterNormalTS(float2 uv)
        {
            float3 normal = WaterNormalSample(uv);
            return float3(normal.xy * _Normal_Intensity, lerp(1.0, normal.z, saturate(_Normal_Intensity)));
        }

        // The scene eye depth at a screen UV, from the camera depth texture: _ZBufferParams for perspective,
        // inverse view-projection for orthographic.
        float WaterSceneEyeDepth(float2 screenUV)
        {
            float rawDepth = SampleSceneDepth(screenUV);
            if (unity_OrthoParams.w == 1.0)
            {
                // Metal stores reversed 0..1 depth; GL stores 0..1 but its inverse VP expects -1..1.
                #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float3 scenePositionWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                return abs(mul(UNITY_MATRIX_V, float4(scenePositionWS, 1.0)).z);
            }
            return 1.0 / (_ZBufferParams.z * rawDepth + _ZBufferParams.w);
        }

        // The graph's Fog node density: 0 without a fog keyword, else one minus the fog intensity at the
        // near-plane-relative view depth.
        float WaterFogDensity(float3 positionWS)
        {
            #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
            float viewZ = -TransformWorldToView(positionWS).z;
            return 1.0 - ComputeFogIntensity(ComputeFogFactorZ0ToFar(max(viewZ - _ProjectionParams.y, 0.0)));
            #else
            return 0.0;
            #endif
        }

        struct WaterSurface
        {
            float3 albedo;
            float3 emission;
            float3 normalTS;
            float smoothness;
            float alpha;
        };

        // The water colour: the depth ramp (refracted scene depth below the fragment's clip w over the depth
        // distance) plus the colour variation noise, colour-dodged by the refracted scene colour, under the foam
        // (a depth-faded edge stepped against scrolling noise, plus the vertex red over noise); the albedo then
        // takes the graph's fog blend. The refraction offset applies only where the offset scene lies behind
        // the water (1 - view z against the scene eye depth there).
        WaterSurface WaterSurfaceInputs(float2 uv, float4 vertexColor, float3 positionWS, float4 positionCS)
        {
            WaterSurface surface;
            float time = _HLShaderTimeGame;
            float clipW = TransformWorldToHClip(positionWS).w;
            float y = _ProjectionParams.x < 0.0 ? _ScaledScreenParams.y - positionCS.y : positionCS.y;
            float2 screenUV = float2(positionCS.x, y) / _ScaledScreenParams.xy;
            #if UNITY_UV_STARTS_AT_TOP
            screenUV.y = 1.0 - screenUV.y;
            #endif

            float2 variationUV = uv * float2(_Color_Varation_UV_Tiling_X, _Color_Varation_UV_Tiling_Y) + time * float2(_Colour_Variation_Speed_Direction_X, _Colour_Variation_Speed_Direction_Y);
            float variation = SAMPLE_TEXTURE2D(_Packed_Noise_Texture, SamplerState_Linear_Repeat, variationUV).y * _Color_Varation_Amount;
            float3 normal = WaterNormalSample(uv);
            float2 refraction = normal.xy * _Refraction_Intensity;
            float waterDepth = 1.0 - TransformWorldToView(positionWS).z;
            float behind = waterDepth < WaterSceneEyeDepth(screenUV - refraction) ? 1.0 : 0.0;
            float2 refractedUV = screenUV - behind * refraction;
            float depth = saturate((WaterSceneEyeDepth(refractedUV) - clipW) / _Water_Depth_Distance);
            float3 color = SAMPLE_TEXTURE2D(_Water_Colour_Gradient_Ramp_Texture, sampler_Water_Colour_Gradient_Ramp_Texture, depth.xx).rgb + _Color_Varation.rgb * variation;
            float3 scene = clamp(SampleSceneColor(refractedUV) * _Refraction_Colour_Blend_Amount, 9.99999997e-07, 0.999998987);
            color = saturate(lerp(color, color / (1.0 - scene), _Refraction_Colour_Blend_Amount));

            float2 vertexFoamUV = uv * float2(_VC_Foam_UV_Tiling_X, _VC_Foam_UV_Tiling_Y) + time * float2(_VC_Foam_Speed_Direction_X, _VC_Foam_Speed_Direction_Y);
            float vertexFoam = SAMPLE_TEXTURE2D(_Packed_Noise_Texture, SamplerState_Linear_Repeat, vertexFoamUV).x + vertexColor.r >= _VC_Foam_Amount ? 1.0 : 0.0;
            float foamDepth = saturate((WaterSceneEyeDepth(screenUV) - clipW) / _Foam_Depth_Fade);
            // The graph's tiling names are crossed: Y tiles u, X tiles the depth.
            float2 foamUV = float2(uv.x * _Foam_UV_Tiling_Y, foamDepth * _Foam_UV_Tiling_X) + time * float2(_Foam_Speed_Direction_X, _Foam_Speed_Direction_Y);
            float edge = _Foam_Step_Amount >= foamDepth - SAMPLE_TEXTURE2D(_Packed_Noise_Texture, SamplerState_Linear_Repeat, foamUV).x ? 1.0 : 0.0;
            float foam = min(edge * (1.0 - foamDepth) + vertexFoam, 1.0);
            color = lerp(color, foam * _Foam_Color.rgb, foam * _Foam_Color.a);

            surface.albedo = lerp(unity_FogColor.rgb, color, saturate(_Scene_Fog_Intensity - WaterFogDensity(positionWS) + 1.0));
            surface.emission = color * _Emission_Intensity;
            surface.normalTS = float3(normal.xy * _Normal_Intensity, lerp(1.0, normal.z, saturate(_Normal_Intensity)));
            surface.smoothness = saturate(_Smoothness);
            // The graph's dot product of two splatted values: twice the overall alpha times one minus vertex green.
            surface.alpha = saturate(dot(_Overall_Alpha.xx, (1.0 - vertexColor.g).xx));
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
                float4 uv : TEXCOORD3;
                float4 fogFactorAndVertexLight : TEXCOORD4;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD5;
                #endif
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 6);
                #if defined(DYNAMICLIGHTMAP_ON)
                float2 dynamicLightmapUV : TEXCOORD7;
                #endif
                float4 color : TEXCOORD8;
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
                output.color = input.color;
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
                WaterSurface surface = WaterSurfaceInputs(uv, input.color, input.positionWS, input.positionCS);
                float3 normalTS = surface.normalTS;
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
                surfaceData.albedo = surface.albedo;
                surfaceData.metallic = 0;
                surfaceData.smoothness = surface.smoothness;
                surfaceData.emission = surface.emission;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1;
                surfaceData.alpha = surface.alpha;
                #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                // As shipped: no MixFog on the lit output (the albedo carries the graph's fog blend).
                outColor = UniversalFragmentPBR(inputData, surfaceData);
                #if defined(_WRITE_RENDERING_LAYERS)
                outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
                #endif
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

            struct Attributes { float4 positionOS : POSITION; float4 uv0 : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float4 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv0;
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float4 uv0 : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 tangentWS : TEXCOORD0;
                float4 uv : TEXCOORD1;
                float4 color : TEXCOORD2;
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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv = input.uv0;
                output.color = input.color;
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
                outNormalWS = float4(NormalizeNormalPerPixel(TransformTangentToWorld(WaterNormalTS(input.uv.xy), tangentToWorld)), 0.0);
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
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 color : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                #if defined(EDITOR_VISUALIZATION)
                float2 vizUV : TEXCOORD5;
                float4 lightCoord : TEXCOORD6;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1.xy, input.uv2.xy);
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                output.color = input.color;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(input.positionOS.xyz, input.uv0.xy, input.uv1.xy, input.uv2.xy, output.vizUV, output.lightCoord);
                #endif
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                WaterSurface surface = WaterSurfaceInputs(input.uv0.xy, input.color, input.positionWS, input.positionCS);
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
