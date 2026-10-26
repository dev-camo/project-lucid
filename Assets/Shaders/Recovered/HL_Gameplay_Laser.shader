// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Gameplay/HL_Gameplay_Laser, a URP 14 Unlit Shader Graph (platform 14), transparent
// without depth writes. Shader Graph's Simple Noise, scrolled by game time, distorts the scrolling main texture's UV
// and dissolves it; a mask, the colour and the vertex colour multiply the result.
Shader "HardLight/Gameplay/HL_Gameplay_Laser"
{
    Properties
    {
        [HDR]_Color ("Color", Color) = (1,1,1,1)
        [NoScaleOffset]_Main_Texture ("Main Texture", 2D) = "white" {}
        _Main_Speed ("Main Speed", Vector) = (-0.5,0,0,0)
        [NoScaleOffset]_Mask ("Mask", 2D) = "white" {}
        _Noise_Scale ("Noise Scale", Float) = 25
        _Noise_Speed ("Noise Speed", Vector) = (-1,0,0,0)
        _Noise_Levels ("Noise Levels", Float) = 0.1
        _Noise_Amount ("Noise Amount", Range(0, 1)) = 0.1
        _Dissolve_Amount ("Dissolve Amount", Range(0, 1)) = 0
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
            float4 _Main_Texture_TexelSize;
            float4 _Color;
            float2 _Main_Speed;
            float4 _Mask_TexelSize;
            float _Noise_Scale;
            float2 _Noise_Speed;
            float _Noise_Levels;
            float _Noise_Amount;
            float _Dissolve_Amount;
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

            TEXTURE2D(_Main_Texture);
            SAMPLER(sampler_Main_Texture);
            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);

            // Shader Graph's Simple Noise: three octaves of sine-hash value noise.
            float SimpleNoiseRandom(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }

            float SimpleNoiseValue(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);
                float bottom = lerp(SimpleNoiseRandom(i), SimpleNoiseRandom(i + float2(1.0, 0.0)), f.x);
                float top = lerp(SimpleNoiseRandom(i + float2(0.0, 1.0)), SimpleNoiseRandom(i + float2(1.0, 1.0)), f.x);
                return lerp(bottom, top, f.y);
            }

            float SimpleNoise(float2 uv, float scale)
            {
                return SimpleNoiseValue(uv * scale) * 0.125
                     + SimpleNoiseValue(uv * (scale * 0.5)) * 0.25
                     + SimpleNoiseValue(uv * (scale * 0.25)) * 0.5;
            }

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
                output.uv0 = input.uv0;
                output.color = input.color;
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

                // The noise, scrolled by game time, saturated and raised to the levels power.
                float noise = pow(saturate(SimpleNoise(uv + _HLShaderTimeGame * _Noise_Speed, _Noise_Scale)), _Noise_Levels);

                // The main texture over the UV pulled towards the noise by the amount, scrolled by game time; the
                // dissolve lerps all four channels towards its alpha times the noise.
                float2 mainUV = lerp(uv, noise.xx, _Noise_Amount) + _Main_Speed * _HLShaderTimeGame;
                float4 main = SAMPLE_TEXTURE2D(_Main_Texture, sampler_Main_Texture, mainUV);
                float4 color = lerp(main, main.a * noise, _Dissolve_Amount);

                // The mask's red also masks the alpha; the colour tints only the RGB.
                color *= SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).rgbr;
                color.rgb *= _Color.rgb;
                outColor = color * input.color;
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
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv0 = input.uv0;
                output.color = input.color;
                output.normalWS = normalWS;
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
                return output;
            }
            float4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
