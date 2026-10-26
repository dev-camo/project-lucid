// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Gameplay/HL_Gameplay_LightningBolt, a URP 14 Unlit Shader Graph (platform 14),
// transparent. A rectangle (Shader Graph's Rectangle node) on rotated UV0 pulled towards a scrolling noise, in the
// colour; its coverage is also the alpha.
Shader "HardLight/Gameplay/HL_Gameplay_LightningBolt"
{
    Properties
    {
        _Separator ("#", Float) = 0
        _Title_Surface_Inputs ("## Surface Inputs", Float) = 0
        [HDR]_Color ("-- Color", Color) = (0,0.5989611,5.656854,0)
        _Width ("-- Width", Float) = 0.05
        _Height ("-- Height", Float) = 1
        _Rotation ("-- Rotation", Range(0, 360)) = 0
        _Separator_1 ("# (1)", Float) = 0
        _Title_Noise ("## Noise", Float) = 0
        _Noise_Intensity ("-- Noise Intensity", Range(0, 1)) = 0.2
        _Noise_Scale ("-- Noise Scale", Float) = 1
        _Noise_Speed_X ("-- Noise Speed X", Range(0, 1)) = 0.3
        _Noise_Speed_Y ("-- Noise Speed Y", Range(0, 1)) = 0
        _Separator_2 ("# (2)", Float) = 0
        [NoScaleOffset][NonModifiableTextureData]_SampleTexture2D_c8f92df2dd054d678562be1997342e27_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
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
            float4 _SampleTexture2D_c8f92df2dd054d678562be1997342e27_Texture_1_Texture2D_TexelSize;
            float _Separator_1;
            float _Width;
            float _Height;
            float _Noise_Scale;
            float _Noise_Intensity;
            float _Separator_2;
            float _Title_Surface_Inputs;
            float4 _Color;
            float _Rotation;
            float _Noise_Speed_X;
            float _Noise_Speed_Y;
            float _Separator;
            float _Title_Noise;
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

            // The graph's immutable noise texture.
            TEXTURE2D(_SampleTexture2D_c8f92df2dd054d678562be1997342e27_Texture_1_Texture2D);
            SAMPLER(sampler_SampleTexture2D_c8f92df2dd054d678562be1997342e27_Texture_1_Texture2D);

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
                // UV0 rotated by the rotation (degrees) about (0.5, 0.5), through Shader Graph's Rotate node matrix.
                float angle = _Rotation * 0.0174532924;
                float s = sin(angle), c = cos(angle);
                float2x2 rotation = float2x2(c, -s, s, c) * 0.5 + 0.5;
                rotation = rotation * 2.0 - 1.0;
                float2 uv = mul(input.uv0.xy - 0.5, rotation) + 0.5;
                // The noise's red and green at that UV times the scale, scrolled by game time times the speed, pull it
                // by the intensity.
                float2 noiseUV = uv * _Noise_Scale + _HLShaderTimeGame * float2(_Noise_Speed_X, _Noise_Speed_Y);
                float2 noise = SAMPLE_TEXTURE2D(_SampleTexture2D_c8f92df2dd054d678562be1997342e27_Texture_1_Texture2D, sampler_SampleTexture2D_c8f92df2dd054d678562be1997342e27_Texture_1_Texture2D, noiseUV).xy;
                uv = lerp(uv, noise, _Noise_Intensity);
                // Shader Graph's Rectangle node: the width and height, antialiased over one pixel's derivative.
                float2 d = abs(uv * 2.0 - 1.0) - float2(_Width, _Height);
                d = saturate(1.0 - d / (abs(ddx(d)) + abs(ddy(d))));
                float bolt = min(d.x, d.y);
                outColor = float4(bolt * _Color.rgb, bolt);
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
