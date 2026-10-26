// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Gameplay/HL_Gameplay_Zone2_Tube_Door, a URP 14 Unlit Shader Graph (platform 14),
// alpha-clipped and two-sided. The door opens as a hole growing from the UV centre with the open amount, while the
// tinted diffuse turns about the centre by twice the open amount (radians).
Shader "HardLight/Gameplay/HL_Gameplay_Zone2_Tube_Door"
{
    Properties
    {
        _Seperator_0 ("#", Float) = 0
        _Title_Surface_Inputs ("## Surface Inputs", Float) = 1
        [NoScaleOffset]_Diff_Texture ("-- Diff Texture", 2D) = "white" {}
        [HDR]_Diff_Tint ("-- Diff Tint", Color) = (1,1,1,0)
        _Open_Door_Amount ("-- Open Door Amount", Range(0, 1)) = 0
        _Seperator_1 ("# (1)", Float) = 0
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
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float4 _Diff_Texture_TexelSize;
            float _Title_Surface_Inputs;
            float _Seperator_0;
            float _Seperator_1;
            float _Open_Door_Amount;
            float4 _Diff_Tint;
        CBUFFER_END

        // The door's coverage: 1 where twice the distance from the UV centre (at most 1) reaches the open amount.
        float TubeDoorAlpha(float2 uv)
        {
            return min(length(uv - 0.5) * 2.0, 1.0) >= _Open_Door_Amount ? 1.0 : 0.0;
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
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            TEXTURE2D(_Diff_Texture);
            SAMPLER(sampler_Diff_Texture);

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
                // The coverage clips at 0.5, sharpened for alpha to coverage when it is available, which then also
                // becomes the output alpha (1 otherwise).
                float alpha = TubeDoorAlpha(input.uv0.xy);
                float clipValue = alpha - 0.5;
                float width = abs(ddx(alpha)) + abs(ddy(alpha));
                float sharpened = saturate((clipValue - width * 0.5) / max(width, 9.99999975e-05) + 1.0);
                if (_AlphaToMaskAvailable != 0.0)
                    clipValue = sharpened - 9.99999975e-05;
                clip(clipValue);

                // The diffuse at UV0 turned about the centre by twice the open amount (Shader Graph's Rotate node,
                // radians), tinted.
                float angle = _Open_Door_Amount * 2.0;
                float s = sin(angle), c = cos(angle);
                float2x2 rotation = float2x2(c, -s, s, c) * 0.5 + 0.5;
                rotation = rotation * 2.0 - 1.0;
                float2 uv = mul(input.uv0.xy - 0.5, rotation) + 0.5;
                float3 diffuse = SAMPLE_TEXTURE2D(_Diff_Texture, sampler_Diff_Texture, uv).rgb;
                outColor = float4(diffuse * _Diff_Tint.rgb, _AlphaToMaskAvailable != 0.0 ? sharpened : 1.0);
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
                // The coverage clips at 0.5, unsharpened.
                clip(TubeDoorAlpha(input.uv0.xy) - 0.5);
                return input.positionCS.zzzz;
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
