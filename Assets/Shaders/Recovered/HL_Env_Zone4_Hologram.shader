// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Zone4_Hologram, a URP 14 Unlit Shader Graph (platform 14),
// transparent with alpha clipping at the material's threshold. The tinted pattern texture fades through scrolling
// scan lines along UV0's v.
Shader "HardLight/Environment/HL_Env_Zone4_Hologram"
{
    Properties
    {
        _Seperator ("#", Float) = 0
        _Title_Surface_Inputs ("## Surface Inputs", Float) = 0
        _RGB_Pattern_Texture ("-- RGB Pattern Texture", 2D) = "white" {}
        [HDR]_ColorTint ("-- ColorTint", Color) = (1,1,1,0)
        _Alpha_Clip ("-- Alpha Clip", Range(0, 1)) = 0.5
        _Alpha ("-- Alpha", Range(0, 1)) = 1
        _Seperator_1 ("# (1)", Float) = 0
        _Title_Lines ("## Lines", Float) = 0
        _Line_Speed ("-- Line Speed", Float) = 0
        _Line_Amount ("-- Line Amount", Float) = 100
        _Line_Alpha ("-- Line Alpha", Range(0, 1)) = 1
        _Seperator_2 ("# (2)", Float) = 0
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
        // The graph clips at the material's threshold; alpha test is not a material keyword.
        #define _ALPHATEST_ON 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float _Seperator_1;
            float _Title_Lines;
            float _Title_Surface_Inputs;
            float _Line_Alpha;
            float _Seperator;
            float4 _RGB_Pattern_Texture_TexelSize;
            float4 _RGB_Pattern_Texture_ST;
            float _Line_Amount;
            float _Line_Speed;
            float _Seperator_2;
            float _Alpha_Clip;
            float _Alpha;
            float4 _ColorTint;
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

            TEXTURE2D(_RGB_Pattern_Texture);
            SAMPLER(sampler_RGB_Pattern_Texture);

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
                output.uv0 = input.uv0;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = input.uv0.xy;
                // The pattern texture, its tiling and offset applied twice (once by the graph, once by the node).
                float4 pattern = SAMPLE_TEXTURE2D(_RGB_Pattern_Texture, sampler_RGB_Pattern_Texture,
                    (uv * _RGB_Pattern_Texture_ST.xy + _RGB_Pattern_Texture_ST.zw) * _RGB_Pattern_Texture_ST.xy + _RGB_Pattern_Texture_ST.zw);
                // Scan lines: the positive part of a sine along v (scrolling with game time), lifted by the line alpha.
                float lines = saturate(max(sin((_HLShaderTimeGame * _Line_Speed + uv.y) * _Line_Amount), 0.0) + _Line_Alpha);
                // As the generated pass: clip (sharpened for alpha-to-coverage when available).
                float alpha = AlphaDiscard(pattern.a * _Alpha * lines, _Alpha_Clip);
                outColor = float4(pattern.rgb * _ColorTint.rgb, OutputAlpha(alpha, true));
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
