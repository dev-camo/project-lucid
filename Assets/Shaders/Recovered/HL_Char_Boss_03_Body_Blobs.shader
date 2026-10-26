// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Character/HL_Char_Boss_03_Body_Blobs, a URP 14 Unlit Shader Graph
// (platform 14), opaque with alpha clipping at a constant 0.5 and alpha-to-coverage. The mesh is pushed
// along its normal by a scrolling displacement texture; the erosion edge clips it away.
Shader "HardLight/Character/HL_Char_Boss_03_Body_Blobs"
{
    Properties
    {
        _Displacement_Texture ("-- Displacement Texture", 2D) = "white" {}
        _Displacement_Speed_X ("-- Displacement Speed X", Float) = 0.1
        _Displacement_Speed_Y ("-- Displacement Speed Y", Float) = 0.1
        _Displacement_Amount ("-- Displacement Amount", Float) = 0.3
        _Colour ("-- Colour", Color) = (0.5019608,0.5019608,0.5019608,0)
        _Title_Vertex_Displacement_1 ("## Erosion", Float) = 0
        _Erosion_Tiling ("-- Erosion Tiling", Float) = 1
        _Erosion_Progress ("-- Erosion Progress", Range(0, 1)) = 0
        [NoScaleOffset][NonModifiableTextureData]_SampleTexture2D_576d80b255874f098fc57ed07098b701_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
        [HideInInspector]_QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl ("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Unlit" "Queue"="AlphaTest" "DisableBatching"="False" "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalUnlitSubTarget" }

        HLSLINCLUDE
        #pragma target 3.5
        // The graph clips at a constant 0.5; alpha test is not a material keyword.
        #define _ALPHATEST_ON 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float4 _SampleTexture2D_576d80b255874f098fc57ed07098b701_Texture_1_Texture2D_TexelSize;
            float4 _Displacement_Texture_TexelSize;
            float4 _Displacement_Texture_ST;
            float _Displacement_Speed_X;
            float _Displacement_Speed_Y;
            float _Title_Vertex_Displacement_1;
            float _Erosion_Progress;
            float _Erosion_Tiling;
            float _Displacement_Amount;
            float4 _Colour;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;

        TEXTURE2D(_Displacement_Texture);
        TEXTURE2D(_SampleTexture2D_576d80b255874f098fc57ed07098b701_Texture_1_Texture2D);
        SAMPLER(sampler_SampleTexture2D_576d80b255874f098fc57ed07098b701_Texture_1_Texture2D);
        // The graph's inline sampler for the displacement: linear, repeat.
        SAMPLER(SamplerState_Linear_Repeat);

        // Object position pushed along the normal by the scrolling displacement texture's red.
        float3 BlobDisplace(float3 positionOS, float3 normalOS, float2 uv0)
        {
            float2 uv = uv0 * _Displacement_Texture_ST.xy + (_HLShaderTimeGame * float2(_Displacement_Speed_X, _Displacement_Speed_Y) + _Displacement_Texture_ST.zw);
            return positionOS + SAMPLE_TEXTURE2D_LOD(_Displacement_Texture, SamplerState_Linear_Repeat, uv, 0).r * _Displacement_Amount * normalOS;
        }

        // The eroded edge: smoothstep((0.5 - erosion) * 4), where erosion is the texture's blue shifted by
        // the progress.
        float BlobAlpha(float2 uv0)
        {
            float erosion = SAMPLE_TEXTURE2D(_SampleTexture2D_576d80b255874f098fc57ed07098b701_Texture_1_Texture2D,
                sampler_SampleTexture2D_576d80b255874f098fc57ed07098b701_Texture_1_Texture2D, uv0 * _Erosion_Tiling).b;
            erosion = saturate(erosion + (_Erosion_Progress - 0.5) * 2.0);
            return min(smoothstep(0.0, 1.0, saturate((0.5 - erosion) * 4.0)), 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Blend One Zero
            ZTest LEqual
            ZWrite On
            Cull Off
            AlphaToMask On

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

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
                output.positionWS = TransformObjectToWorld(BlobDisplace(input.positionOS, input.normalOS, input.uv0.xy));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv0 = input.uv0;
                return output;
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // As the generated pass: clip (sharpened for alpha-to-coverage when available); an opaque
                // surface then writes 1 unless alpha-to-coverage is available.
                float alpha = AlphaDiscard(BlobAlpha(input.uv0.xy), 0.5);
                outColor = float4(_Colour.rgb, OutputAlpha(alpha, false));
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(BlobDisplace(input.positionOS, input.normalOS, input.uv0.xy));
                output.uv0 = input.uv0;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(BlobAlpha(input.uv0.xy) - 0.5);
                return input.positionCS.zzzz;
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
