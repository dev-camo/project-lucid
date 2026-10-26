// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Crowd, a URP 14 Unlit Shader Graph (platform 14), opaque
// with alpha clipping at the material's threshold and alpha-to-coverage. A flipbook sheet animates along the
// picked row at the game-time frame rate.
Shader "HardLight/Environment/HL_Env_Crowd"
{
    Properties
    {
        _Separator_0 ("#", Float) = 1
        _Separator_1 ("# (1)", Float) = 1
        _Max_Frames_width ("-- Max Frames width", Float) = 4
        _Max_Frames_height ("-- Max Frames height", Float) = 4
        _FPS ("-- FPS", Float) = 1
        _Pick_Row ("-- Pick Row", Range(0, 3)) = 3
        _Alpha_Clip ("-- Alpha Clip", Range(0, 1)) = 0.5
        [NoScaleOffset]_Diff_Tex ("-- Diff Texture", 2D) = "white" {}
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
        // The graph clips at the material's threshold; alpha test is not a material keyword.
        #define _ALPHATEST_ON 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float _Separator_1;
            float4 _Diff_Tex_TexelSize;
            float _Separator_0;
            float _Max_Frames_height;
            float _Alpha_Clip;
            float _Max_Frames_width;
            float _FPS;
            float _Pick_Row;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;

        TEXTURE2D(_Diff_Tex);
        SAMPLER(sampler_Diff_Tex);

        // The graph's Modulo by one: the fraction carrying the dividend's sign.
        float SignedFrac(float x)
        {
            return x >= -x ? frac(abs(x)) : -frac(abs(x));
        }

        // The flipbook frame: the game-time frame (FPS, wrapping at the sheet width) wrapped to the frame count,
        // split into column and row, offset by the picked row and scaled to one tile.
        float4 CrowdSample(float2 uv0)
        {
            float frames = _Max_Frames_width * _Max_Frames_height;
            float frame = floor(SignedFrac(_HLShaderTimeGame * _FPS / _Max_Frames_width) * _Max_Frames_width);
            float tile = floor(SignedFrac((frame + 9.99999975e-06) / frames) * frames);
            float2 tileSize = 1.0 / float2(_Max_Frames_width, _Max_Frames_height);
            float row = floor((tile + 0.5) * tileSize.x);
            float2 offset = float2(tile - _Max_Frames_width * row, row);
            float2 uv = (uv0 + float2(0.0, floor(_Pick_Row)) + offset) * tileSize;
            return SAMPLE_TEXTURE2D(_Diff_Tex, sampler_Diff_Tex, uv);
        }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Blend One Zero
            ZTest LEqual
            ZWrite On
            Cull Back
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
            #pragma instancing_options renderinglayer
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
                output.positionWS = TransformObjectToWorld(input.positionOS);
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
                float4 diffuse = CrowdSample(input.uv0.xy);
                // As the generated pass: clip (sharpened for alpha-to-coverage when available); an opaque
                // surface then writes 1 unless alpha-to-coverage is available.
                float alpha = AlphaDiscard(diffuse.a, _Alpha_Clip);
                outColor = float4(diffuse.rgb, OutputAlpha(alpha, false));
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
                clip(CrowdSample(input.uv0.xy).a - _Alpha_Clip);
                return input.positionCS.zzzz;
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
