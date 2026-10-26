// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Sky/HL_Skybox_Portal, a URP 14 Unlit Shader Graph (platform 14), transparent
// with alpha clipping always on. A distorted mask drives a scrolling gradient, an inner glow over a
// rotated, distorted cubemap seen along the view direction, and an edge highlight; the graph adds no fog.
Shader "HardLight/Sky/HL_Skybox_Portal"
{
    Properties
    {
        _Separator_0 ("#", Float) = 0
        _Title_Surface_Inputs ("## Surface Inputs", Float) = 0
        _Alpha_Clip ("-- Alpha Clip", Range(0, 1)) = 0.5
        _Alpha ("-- Alpha", Range(0, 1)) = 1
        _Separator_1 ("# (1)", Float) = 0
        _Title_Cubemap ("## Cubemap", Float) = 0
        _Title_Cubemap_Surface ("### Cubemap Surface", Float) = 0
        [NoScaleOffset]_Cubemap_Texture ("-- Cubemap Texture", Cube) = "" {}
        _Cubemap_Colour_Tint ("-- Cubemap Colour Tint", Color) = (0,0,0,0)
        _NOTE_Tint_is_addative ("!NOTE Tint is addative.", Float) = 0
        _Cubemap_Blur_Amount ("-- Cubemap Blur Amount", Range(0, 10)) = 0
        _Title_Cubemap_Transform ("### Cubemap Transform", Float) = 0
        _Cubemap_Transform ("-- Cubemap Transform", Vector) = (0,0,0,0)
        _Cubemap_Flip_Y ("-- Cubemap Flip Y", Float) = -1
        _Title_Cubemap_Rotation ("### Cubemap Rotation", Float) = 0
        _Cubemap_Rotation_Amount ("-- Cubemap Rotation Amount", Range(0, 360)) = 0
        _Cubemap_Rotation_Axis ("-- Cubemap Rotation Axis", Vector) = (0,1,0,0)
        _Title_Cubemap_Distortion ("### Cubemap Distortion", Float) = 0
        _Cubemap_Distortion_Amount ("-- Cubemap Distortion Amount", Range(0, 0.1)) = 0
        _Separator_2 ("# (2)", Float) = 0
        _Title_Inner_Edge ("## Inner Glow", Float) = 0
        _Inner_Glow_Colour ("-- Inner Glow Colour", Color) = (0.2117647,0,0.4705882,1)
        _Inner_Glow_Mask_Min ("-- Inner Glow Mask Min", Range(0, 1)) = 0
        _Inner_Glow_Mask_Max ("-- Inner Glow Mask Max", Range(0, 1)) = 1
        _Separator_3 ("# (3)", Float) = 0
        _Title_Edge_Highlight ("## Edge Highlight", Float) = 0
        [HDR]_Edge_Highlight_Colour ("-- Edge Highlight Colour", Color) = (2,2,2,0)
        _Edge_Highlight_Mask_Min ("-- Edge Highlight Mask Min", Range(0, 1)) = 0.5
        _Edge_Highlight_Mask_Max ("-- Edge Highlight Mask Max", Range(0, 1)) = 0.6
        _Separator_4 ("# (4)", Float) = 0
        _Title_Gradient ("## Gradient", Float) = 0
        [NoScaleOffset]_Gradient_Texture ("-- Gradient Texture", 2D) = "white" {}
        _Gradient_Speed ("-- Gradient Speed", Range(0, 1)) = 0.25
        _Gradient_Mask_Min ("-- Gradient Mask Texture Min", Range(0, 1)) = 0
        _Gradient_Mask_Max ("-- Gradient Mask Texture Max", Range(0, 1)) = 1
        _Gradient_Mask_Fresnel_Amount ("-- Gradient Mask Fresnel Amount", Float) = 5
        _Gradient_Mask_Fresnel_Strength ("-- Gradient Mask Fresnel Strength", Float) = 2
        _Gradient_Mask_Depth_Distance ("-- Gradient Mask Depth Distance", Float) = 3
        _Gradient_Mask_Depth_Strength ("-- Gradient Mask Depth Strength", Float) = 1
        _Separator_5 ("# (5)", Float) = 0
        _Title_Mask ("## Mask", Float) = 0
        [NoScaleOffset]_Mask_Texture ("-- Mask Texture", 2D) = "white" {}
        _Mask_Texture_Blur ("-- Mask Texture Blur", Range(0, 5)) = 0
        _Mask_Channel_Selection ("-- Mask Channel Selection (Red, Green, Blue, Alpha) &", Vector) = (0,1,0,0)
        _Separator_6 ("# (6)", Float) = 0
        _Title_Mask_Distortion ("### Mask Distortion", Float) = 0
        _Distortion_Noise ("-- Distortion Noise", 2D) = "white" {}
        _NOTE_Offset_XY_controls_scrolling ("!NOTE Offset XY controls scrolling.", Float) = 0
        _Distortion_Amount ("-- Distortion Amount", Range(0, 1)) = 0.05
        _Separator_7 ("# (7)", Float) = 0
        [HideInInspector]_QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl ("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "UniversalMaterialType"="Unlit" "Queue"="Transparent" "DisableBatching"="False" "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalUnlitSubTarget" }

        HLSLINCLUDE
        #pragma target 3.5
        // The graph clips against _Alpha_Clip; alpha test is not a material keyword.
        #define _ALPHATEST_ON 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float _Cubemap_Blur_Amount;
            float _Title_Cubemap;
            float _Alpha;
            float _Title_Surface_Inputs;
            float _Separator_0;
            float _Title_Cubemap_Surface;
            float _Title_Cubemap_Transform;
            float _Title_Cubemap_Rotation;
            float _Cubemap_Rotation_Amount;
            float _Alpha_Clip;
            float _Inner_Glow_Mask_Max;
            float _Gradient_Mask_Max;
            float _Title_Cubemap_Distortion;
            float _Cubemap_Distortion_Amount;
            float _Gradient_Mask_Min;
            float _NOTE_Tint_is_addative;
            float _Separator_1;
            float _Title_Inner_Edge;
            float _Inner_Glow_Mask_Min;
            float _Separator_2;
            float _Title_Gradient;
            float _Distortion_Amount;
            float _Title_Edge_Highlight;
            float _Mask_Texture_Blur;
            float4 _Mask_Channel_Selection;
            float _Gradient_Speed;
            float _Separator_3;
            float4 _Gradient_Texture_TexelSize;
            float4 _Inner_Glow_Colour;
            float _Title_Mask_Distortion;
            float _Title_Mask;
            float3 _Cubemap_Rotation_Axis;
            float _Separator_4;
            float _Separator_5;
            float4 _Distortion_Noise_TexelSize;
            float4 _Distortion_Noise_ST;
            float4 _Mask_Texture_TexelSize;
            float _NOTE_Offset_XY_controls_scrolling;
            float4 _Cubemap_Colour_Tint;
            float _Separator_6;
            float3 _Cubemap_Transform;
            float _Cubemap_Flip_Y;
            float _Gradient_Mask_Fresnel_Amount;
            float _Edge_Highlight_Mask_Max;
            float _Separator_7;
            float _Edge_Highlight_Mask_Min;
            float _Gradient_Mask_Fresnel_Strength;
            float4 _Edge_Highlight_Colour;
            float _Gradient_Mask_Depth_Distance;
            float _Gradient_Mask_Depth_Strength;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;

        TEXTURE2D(_Distortion_Noise);
        SAMPLER(sampler_Distortion_Noise);
        TEXTURE2D(_Mask_Texture);
        // The graph's inline sampler for the mask: linear, clamp.
        SAMPLER(SamplerState_Linear_Clamp);

        // The distortion noise: UV0 scrolled by game time times the noise offset, then tiled twice.
        float3 PortalNoise(float2 uv0)
        {
            float2 uv = (uv0 * _Distortion_Noise_ST.xy + _HLShaderTimeGame * _Distortion_Noise_ST.zw) * _Distortion_Noise_ST.xy + _Distortion_Noise_ST.zw;
            return SAMPLE_TEXTURE2D(_Distortion_Noise, sampler_Distortion_Noise, uv).rgb;
        }

        // The mask: the chosen channels of the mask texture at the distorted UV, blurred by its mip level.
        float PortalMask(float2 uv0, float3 noise)
        {
            float4 mask = SAMPLE_TEXTURE2D_LOD(_Mask_Texture, SamplerState_Linear_Clamp, uv0 + noise.xy * _Distortion_Amount, _Mask_Texture_Blur);
            return dot(mask, _Mask_Channel_Selection);
        }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest LEqual
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma shader_feature _ _SAMPLE_GI
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            TEXTURECUBE(_Cubemap_Texture);
            TEXTURE2D(_Gradient_Texture);
            // The graph's inline sampler for the cubemap and gradient: linear, repeat.
            SAMPLER(SamplerState_Linear_Repeat);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 uv0 : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
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
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv0 = input.uv0;
                return output;
            }

            // Linear eye depth of the opaque scene behind this fragment, as the decoded program derives it.
            float SceneEyeDepth(float4 positionCS)
            {
                float y = _ProjectionParams.x < 0.0 ? _ScaledScreenParams.y - positionCS.y : positionCS.y;
                float2 screenUV = float2(positionCS.x, y) / _ScaledScreenParams.xy;
                #if UNITY_UV_STARTS_AT_TOP
                screenUV.y = 1.0 - screenUV.y;
                #endif
                float rawDepth = SampleSceneDepth(screenUV);
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

            // Shader Graph's Rotate About Axis node (the amount is in degrees).
            float3 RotateAboutAxis(float3 v, float3 axis, float degrees)
            {
                float s, c;
                sincos(degrees * 0.0174532924, s, c);
                float t = 1.0 - c;
                axis = normalize(axis);
                float3x3 rotation =
                {
                    t * axis.x * axis.x + c, t * axis.x * axis.y - axis.z * s, t * axis.z * axis.x + axis.y * s,
                    t * axis.x * axis.y + axis.z * s, t * axis.y * axis.y + c, t * axis.y * axis.z - axis.x * s,
                    t * axis.z * axis.x - axis.y * s, t * axis.y * axis.z + axis.x * s, t * axis.z * axis.z + c
                };
                return mul(rotation, v);
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 noise = PortalNoise(input.uv0.xy);
                float mask = PortalMask(input.uv0.xy, noise);

                float3 gradient = SAMPLE_TEXTURE2D(_Gradient_Texture, SamplerState_Linear_Repeat,
                    (_HLShaderTimeGame * _Gradient_Speed + mask).xx).rgb;

                // The cubemap along the (offset, distorted, optionally flipped, rotated) view direction, tinted
                // additively, under the inner glow colour where the mask is low.
                float3 direction = viewDirWS + _Cubemap_Transform + noise * _Cubemap_Distortion_Amount;
                direction.y *= _Cubemap_Flip_Y;
                direction = RotateAboutAxis(direction, _Cubemap_Rotation_Axis, _Cubemap_Rotation_Amount);
                float3 cube = SAMPLE_TEXTURECUBE_LOD(_Cubemap_Texture, SamplerState_Linear_Repeat, direction, _Cubemap_Blur_Amount).rgb + _Cubemap_Colour_Tint.rgb;
                float glow = saturate(smoothstep(_Inner_Glow_Mask_Min, _Inner_Glow_Mask_Max, mask) + (1.0 - _Inner_Glow_Colour.a));
                cube = lerp(_Inner_Glow_Colour.rgb, cube, glow);

                // The cubemap shows through the gradient by the mask, a fresnel falloff and the depth fade.
                float fresnel = saturate(1.0 - pow(1.0 - saturate(dot(normalize(input.normalWS), viewDirWS)), _Gradient_Mask_Fresnel_Amount) * _Gradient_Mask_Fresnel_Strength);
                float clipW = TransformWorldToHClip(input.positionWS).w;
                float depthFade = saturate((SceneEyeDepth(input.positionCS) - clipW) / _Gradient_Mask_Depth_Distance * _Gradient_Mask_Depth_Strength);
                float reveal = min(smoothstep(_Gradient_Mask_Min, _Gradient_Mask_Max, mask) * fresnel, 1.0) * depthFade;
                float3 color = lerp(gradient, cube, reveal);
                color = lerp(_Edge_Highlight_Colour.rgb, color, smoothstep(_Edge_Highlight_Mask_Min, _Edge_Highlight_Mask_Max, mask));

                // As the generated pass: alpha clipping, with alpha-to-coverage sharpening when available.
                float alpha = AlphaDiscard(saturate(mask) * _Alpha, _Alpha_Clip);
                outColor = float4(color, alpha);
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

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 uv0 : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv0 = input.uv0;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(saturate(PortalMask(input.uv0.xy, PortalNoise(input.uv0.xy))) * _Alpha - _Alpha_Clip);
                return input.positionCS.zzzz;
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
