// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Gameplay/HL_Gameplay_Dream_Barrier, a URP 14 Unlit Shader Graph (platform 14),
// additive. A scrolling noise distorts the diffuse texture and a rotated, scrolling scanline texture that steps
// towards the scanline colour; the alpha is the diffuse alpha times vertex red, faded out with camera distance.
Shader "HardLight/Gameplay/HL_Gameplay_Dream_Barrier"
{
    Properties
    {
        _Separator ("#", Float) = 0
        _Title_Vertex_Colour ("## Vertex Colour", Float) = 1
        _Title_Red ("### Red = Alpha", Float) = 1
        _Title_Green ("### Green =", Float) = 1
        _Title_Blue ("### Blue =", Float) = 1
        _Title_Alpha ("### Alpha =", Float) = 1
        _Separator_1 ("# (1)", Float) = 0
        _Title_Surface_Inputs ("## Surface Inputs", Range(0, 2)) = 1
        _Diff_Texture ("-- Diff Texture", 2D) = "white" {}
        [HDR]_Diff_Colour_Tint ("-- Diff Colour Tint", Color) = (1,1,1,1)
        _Diff_Alpha_Channel_Amount ("-- Diff Alpha Channel Amount", Range(-1, 1)) = 0
        _Diff_Global_Alpha_Amount ("-- Global Alpha Amount", Range(0, 2)) = 1
        _Alpha_Camera_Fade ("-- Alpha Camera Fade", Float) = 1
        _Separator_2 ("# (2)", Float) = 0
        _Title_Distortion ("## Distortion", Range(0, 2)) = 1
        _Distortion_Amount ("-- Distortion Amount", Range(-0.25, 0.25)) = 0
        _Distortion_UV_Tiling_X ("-- Distortion UV Tiling X", Float) = 1
        _Distortion_UV_Tiling_Y ("-- Distortion UV Tiling Y", Float) = 1
        _Distortion_UV_Speed_Direction_X ("-- Distortion UV Speed/Direction X", Range(-1, 1)) = 0
        _Distortion_UV_Speed_Direction_Y ("-- Distortion UV Speed/Direction Y", Range(-1, 1)) = 0
        _Separator_3 ("# (3)", Float) = 0
        _Title_Scanlines ("## Scanlines", Range(0, 2)) = 1
        _Scanlines_Thickness ("-- Scanlines Thickness", Range(0, 1)) = 0.5
        _Scanlines_Colour ("-- Scanlines Colour", Color) = (1,1,1,1)
        _Scanlines_Distortion_Amount ("-- Scanlines Distortion Amount", Range(-0.25, 0.25)) = 0
        _Scanlines_UV_Tiling ("-- Scanlines UV Tiling", Float) = 1
        _Scanlines_UV_Rotation ("-- Scanlines UV Rotation", Range(-90, 90)) = 0
        _Scanlines_UV_Speed_Direction ("-- Scanlines UV Speed/Direction", Float) = 0
        _Separator_4 ("# (4)", Float) = 0
        [NoScaleOffset][NonModifiableTextureData]_Texture2DAsset_efea1f31ccab439c9e6d1d1c2705abe6_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
        [NoScaleOffset][NonModifiableTextureData]_SampleTexture2D_7ded27fa0d1b4e12b302d5bbab485959_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
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

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float4 _SampleTexture2D_7ded27fa0d1b4e12b302d5bbab485959_Texture_1_Texture2D_TexelSize;
            float4 _Texture2DAsset_efea1f31ccab439c9e6d1d1c2705abe6_Out_0_Texture2D_TexelSize;
            float _Title_Red;
            float _Title_Green;
            float _Title_Vertex_Colour;
            float _Title_Alpha;
            float _Title_Blue;
            float _Title_Surface_Inputs;
            float _Separator;
            float _Separator_1;
            float _Title_Distortion;
            float _Distortion_UV_Tiling_X;
            float _Diff_Global_Alpha_Amount;
            float _Separator_3;
            float _Distortion_Amount;
            float _Distortion_UV_Tiling_Y;
            float _Separator_2;
            float4 _Diff_Texture_TexelSize;
            float4 _Diff_Texture_ST;
            float _Distortion_UV_Speed_Direction_Y;
            float _Title_Scanlines;
            float4 _Diff_Colour_Tint;
            float _Scanlines_UV_Tiling;
            float _Scanlines_Distortion_Amount;
            float _Scanlines_Thickness;
            float _Scanlines_UV_Speed_Direction;
            float _Scanlines_UV_Rotation;
            float _Separator_4;
            float _Distortion_UV_Speed_Direction_X;
            float4 _Scanlines_Colour;
            float _Diff_Alpha_Channel_Amount;
            float _Alpha_Camera_Fade;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Blend SrcAlpha One, One One
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

            // The graph's immutable textures: the distortion noise and the scanlines.
            TEXTURE2D(_Texture2DAsset_efea1f31ccab439c9e6d1d1c2705abe6_Out_0_Texture2D);
            TEXTURE2D(_SampleTexture2D_7ded27fa0d1b4e12b302d5bbab485959_Texture_1_Texture2D);
            SAMPLER(sampler_SampleTexture2D_7ded27fa0d1b4e12b302d5bbab485959_Texture_1_Texture2D);
            TEXTURE2D(_Diff_Texture);
            SAMPLER(sampler_Diff_Texture);
            // The graph's inline sampler: linear, repeat.
            SAMPLER(SamplerState_Linear_Repeat);

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
                // The distortion noise at UV0 times the tiling plus game time times the speed, read with UV0's own
                // derivatives; its red and green offset the diffuse and the scanline UVs by their amounts.
                float2 uv = input.uv0.xy * float2(_Distortion_UV_Tiling_X, _Distortion_UV_Tiling_Y)
                    + _HLShaderTimeGame * float2(_Distortion_UV_Speed_Direction_X, _Distortion_UV_Speed_Direction_Y);
                float2 noise = SAMPLE_TEXTURE2D_GRAD(_Texture2DAsset_efea1f31ccab439c9e6d1d1c2705abe6_Out_0_Texture2D, SamplerState_Linear_Repeat,
                    uv, ddx(input.uv0.xy), ddy(input.uv0.xy)).xy;
                // The diffuse UV goes through the texture's tiling and offset twice, as the graph does.
                float2 diffuseUV = input.uv0.xy + _Distortion_Amount * noise;
                diffuseUV = diffuseUV * _Diff_Texture_ST.xy + _Diff_Texture_ST.zw;
                diffuseUV = diffuseUV * _Diff_Texture_ST.xy + _Diff_Texture_ST.zw;
                float4 diffuse = SAMPLE_TEXTURE2D(_Diff_Texture, sampler_Diff_Texture, diffuseUV);
                // The scanline UV rotated by the rotation (degrees) about (0.5, 0.5), times the tiling, scrolled by
                // game time; each channel at or above the thickness steps to the scanline colour's alpha.
                float angle = _Scanlines_UV_Rotation * 0.0174532924;
                float s = sin(angle), c = cos(angle);
                float2 p = input.uv0.xy + _Scanlines_Distortion_Amount * noise - 0.5;
                float2 scanUV = (float2(p.x * c + p.y * s, p.y * c - p.x * s) + 0.5) * _Scanlines_UV_Tiling + _Scanlines_UV_Speed_Direction * _HLShaderTimeGame;
                float3 scan = SAMPLE_TEXTURE2D(_SampleTexture2D_7ded27fa0d1b4e12b302d5bbab485959_Texture_1_Texture2D,
                    sampler_SampleTexture2D_7ded27fa0d1b4e12b302d5bbab485959_Texture_1_Texture2D, scanUV).rgb;
                float3 lines = saturate(step(_Scanlines_Thickness, scan) * _Scanlines_Colour.a);
                float3 rgb = lerp(diffuse.rgb * _Diff_Colour_Tint.rgb, _Scanlines_Colour.rgb, lines);
                // Alpha: the diffuse alpha plus the channel amount, saturated, times vertex red and the global amount,
                // saturated; faded by (clip w - camera fade) / 100.
                float alpha = saturate(saturate(diffuse.a + _Diff_Alpha_Channel_Amount) * input.color.r * _Diff_Global_Alpha_Amount);
                float fade = 1.0 - saturate((TransformWorldToHClip(input.positionWS).w - _Alpha_Camera_Fade) * 0.01);
                outColor = float4(rgb, fade * alpha);
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
