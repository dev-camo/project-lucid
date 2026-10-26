// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Zone4_Cloud_Plane, a URP 14 Unlit Shader Graph (platform 14),
// transparent. Two-octave world-XZ noise displaces the vertices along the world normal and shades the colour;
// the alpha is a depth fade against the scene times vertex red.
Shader "HardLight/Environment/HL_Env_Zone4_Cloud_Plane"
{
    Properties
    {
        _Title_Vertex_Colour ("## Vertex Colour", Float) = 1
        _Title_Red_Alpha ("### Red = Alpha", Float) = 1
        _Title_Green ("### Green =", Float) = 1
        _Title_Alpha ("### Alpha =", Float) = 1
        _Title_Blue ("### Blue =", Float) = 1
        _Separator_0 ("#", Float) = 0
        _Title_Surface_Input ("## Surface Input", Float) = 1
        _Colour ("-- Colour", Color) = (1,1,1,0)
        _Separator_1 ("# (1)", Float) = 0
        _Title_Colour_Noise ("## Colour Noise", Float) = 1
        _Colour_Noise_UV_Scale ("-- Colour Noise UV Scale", Float) = 1
        _Colour_Noise_UV_Speed_Direction ("-- Colour Noise UV Speed/Direction", Vector) = (0,0,0,0)
        _Colour_Noise_Lightness ("-- Colour Noise Lightness", Float) = 0
        _Colour_Noise_Contrast ("-- Colour Noise Contrast", Float) = 1
        _Separator_2 ("# (2)", Float) = 0
        _Title_Vertex_Displacement ("## Vertex Displacement", Float) = 1
        _VD_Noise_UV_Scale ("-- VD Noise UV Scale", Float) = 1
        _VD_Noise_UV_Speed_Direction ("-- VD Noise UV Speed/Direction", Vector) = (0,0,0,0)
        _VD_Noise_Amount ("-- VD Noise Amount", Float) = 0
        _Separator_3 ("# (3)", Float) = 0
        _Title_Depth_Fade ("## Depth Fade", Float) = 1
        _Depth_Distance ("-- Depth Distance", Float) = 3
        _Depth_Strength ("-- Depth Strength", Float) = 1
        _Separator_4 ("# (4)", Float) = 0
        _Separator_5 ("# (5)", Float) = 0
        [NoScaleOffset][NonModifiableTextureData]_Texture2DAsset_994fbcb52e0341d68bebd7d5c495b39f_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
        [NoScaleOffset][NonModifiableTextureData]_Texture2DAsset_98ffcd9dfa7244b3a64ded4333b20935_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
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
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float4 _Texture2DAsset_98ffcd9dfa7244b3a64ded4333b20935_Out_0_Texture2D_TexelSize;
            float4 _Texture2DAsset_994fbcb52e0341d68bebd7d5c495b39f_Out_0_Texture2D_TexelSize;
            float _Title_Red_Alpha;
            float _Title_Green;
            float _Title_Vertex_Colour;
            float _Title_Alpha;
            float _Title_Blue;
            float _Separator_0;
            float _Title_Vertex_Displacement;
            float _Title_Depth_Fade;
            float _Colour_Noise_Contrast;
            float _VD_Noise_UV_Scale;
            float _Colour_Noise_UV_Scale;
            float4 _Colour_Noise_UV_Speed_Direction;
            float _Separator_1;
            float _Title_Colour_Noise;
            float4 _VD_Noise_UV_Speed_Direction;
            float _Separator_2;
            float _Colour_Noise_Lightness;
            float _Title_Surface_Input;
            float _VD_Noise_Amount;
            float _Separator_3;
            float _Depth_Distance;
            float _Depth_Strength;
            float _Separator_4;
            float _Separator_5;
            float4 _Colour;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off
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
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // The graph's immutable noise textures (the same asset in the original).
            TEXTURE2D(_Texture2DAsset_994fbcb52e0341d68bebd7d5c495b39f_Out_0_Texture2D);
            TEXTURE2D(_Texture2DAsset_98ffcd9dfa7244b3a64ded4333b20935_Out_0_Texture2D);
            // The graph's inline samplers.
            SAMPLER(SamplerState_Linear_Repeat);
            SAMPLER(SamplerState_Trilinear_Repeat);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 uv0 : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float4 color : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Two noise octaves at world XZ: one at the scale, one offset by (0.3, 0.2) then scaled by 0.5, each
            // scrolled by game time (speed xy and zw); the red channels summed.
            float CloudNoise(float2 xz, float scale, float4 speed)
            {
                float4 uv = float4(xz * scale, xz * scale + float2(0.3, 0.2)) + _HLShaderTimeGame * speed;
                return SAMPLE_TEXTURE2D(_Texture2DAsset_98ffcd9dfa7244b3a64ded4333b20935_Out_0_Texture2D, SamplerState_Trilinear_Repeat, uv.xy).x
                    + SAMPLE_TEXTURE2D(_Texture2DAsset_98ffcd9dfa7244b3a64ded4333b20935_Out_0_Texture2D, SamplerState_Trilinear_Repeat, uv.zw * 0.5).x;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // The displacement noise: the same octaves at the vertex's undisplaced world XZ, wrapped to the unit
                // square and read at level 0, times the amount, along the world normal (added in object space).
                float2 xz = TransformObjectToWorld(input.positionOS).xz;
                float4 uv = float4(xz * _VD_Noise_UV_Scale, xz * _VD_Noise_UV_Scale + float2(0.3, 0.2)) + _HLShaderTimeGame * _VD_Noise_UV_Speed_Direction;
                float noise = SAMPLE_TEXTURE2D_LOD(_Texture2DAsset_994fbcb52e0341d68bebd7d5c495b39f_Out_0_Texture2D, SamplerState_Linear_Repeat, frac(uv.xy), 0).x
                    + SAMPLE_TEXTURE2D_LOD(_Texture2DAsset_994fbcb52e0341d68bebd7d5c495b39f_Out_0_Texture2D, SamplerState_Linear_Repeat, frac(uv.zw * 0.5), 0).x;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS + output.normalWS * (noise * _VD_Noise_Amount));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv0 = input.uv0;
                output.color = input.color;
                return output;
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // The colour noise at a tenth of its scale, contrasted and lifted, saturated, times the colour.
                float shade = saturate(_Colour_Noise_Contrast * CloudNoise(input.positionWS.xz, _Colour_Noise_UV_Scale * 0.1, _Colour_Noise_UV_Speed_Direction) + _Colour_Noise_Lightness);
                // The depth fade: (scene eye depth - fragment clip w) / distance * strength, saturated.
                float y = _ProjectionParams.x < 0.0 ? _ScaledScreenParams.y - input.positionCS.y : input.positionCS.y;
                float2 screenUV = float2(input.positionCS.x, y) / _ScaledScreenParams.xy;
                #if UNITY_UV_STARTS_AT_TOP
                screenUV.y = 1.0 - screenUV.y;
                #endif
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth;
                if (unity_OrthoParams.w == 1.0)
                {
                    // Metal stores reversed 0..1 depth; GL stores 0..1 but its inverse VP expects -1..1.
                    #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                    #endif
                    float3 scenePositionWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                    sceneDepth = abs(mul(UNITY_MATRIX_V, float4(scenePositionWS, 1.0)).z);
                }
                else
                {
                    sceneDepth = 1.0 / (_ZBufferParams.z * rawDepth + _ZBufferParams.w);
                }
                float fade = saturate((sceneDepth - TransformWorldToHClip(input.positionWS).w) / _Depth_Distance * _Depth_Strength);
                outColor = float4(shade * _Colour.rgb, fade * input.color.r);
                #if defined(_WRITE_RENDERING_LAYERS)
                outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
                #endif
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
