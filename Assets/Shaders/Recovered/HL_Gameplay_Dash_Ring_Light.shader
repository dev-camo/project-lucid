// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Gameplay/HL_Gameplay_Dash_Ring_Light, a URP 14 Unlit Shader Graph (platform 14),
// opaque. A scrolling light texture (times 1.5) tinted by the idle or active colour, or a scrolling dream texture
// in dream mode.
Shader "HardLight/Gameplay/HL_Gameplay_Dash_Ring_Light"
{
    Properties
    {
        _Separator ("#", Float) = 0
        _Title_Modes ("## Modes", Float) = 0
        [ToggleUI]_ISACTIVE ("-- Activate?", Float) = 0
        [ToggleUI]_ISDREAM ("-- Use Dream Mode?", Float) = 0
        _Separator_1 ("# (1)", Float) = 0
        _Title_Surface_Inputs ("## Surface Inputs", Float) = 0
        _UV_Speed ("-- UV Speed", Range(-1, 1)) = 0
        _Separator_2 ("# (2)", Float) = 0
        _Title_Default ("## Default", Float) = 0
        _Default_Idle_Colour ("-- Default Idle Colour", Color) = (1,0.686,0,1)
        _Default_Active_Colour ("-- Default Active Colour", Color) = (0.06382966,1,0,0)
        _Separator_3 ("# (3)", Float) = 0
        [NoScaleOffset][NonModifiableTextureData]_Texture2DAsset_df0b622c693b4ce09eb0cef48816c421_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
        [NoScaleOffset][NonModifiableTextureData]_Texture2DAsset_3b305af8f02543c1b40ec4cd91ca4592_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
        [HideInInspector]_QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl ("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Unlit" "Queue"="Geometry" "DisableBatching"="False" "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalUnlitSubTarget" }

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float4 _Texture2DAsset_3b305af8f02543c1b40ec4cd91ca4592_Out_0_Texture2D_TexelSize;
            float4 _Texture2DAsset_df0b622c693b4ce09eb0cef48816c421_Out_0_Texture2D_TexelSize;
            float _Separator_1;
            float _UV_Speed;
            float _Title_Default;
            float _Title_Surface_Inputs;
            float4 _Default_Idle_Colour;
            float4 _Default_Active_Colour;
            float _Separator_2;
            float _Separator_3;
            float _ISACTIVE;
            float _ISDREAM;
            float _Separator;
            float _Title_Modes;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Blend One Zero
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
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // The graph's immutable textures: the light and the dream texture.
            TEXTURE2D(_Texture2DAsset_df0b622c693b4ce09eb0cef48816c421_Out_0_Texture2D);
            TEXTURE2D(_Texture2DAsset_3b305af8f02543c1b40ec4cd91ca4592_Out_0_Texture2D);
            // The graph's inline sampler: linear, repeat.
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
                // Both textures at UV0 scrolled by game time times the speed (on both axes). The light texture times
                // 1.5 is tinted by the idle colour, or the active colour when active; dream mode shows the dream texture.
                float2 uv = input.uv0.xy + _HLShaderTimeGame * _UV_Speed;
                float3 light = SAMPLE_TEXTURE2D(_Texture2DAsset_df0b622c693b4ce09eb0cef48816c421_Out_0_Texture2D, SamplerState_Linear_Repeat, uv).rgb * 1.5;
                float3 dream = SAMPLE_TEXTURE2D(_Texture2DAsset_3b305af8f02543c1b40ec4cd91ca4592_Out_0_Texture2D, SamplerState_Linear_Repeat, uv).rgb;
                float3 ring = lerp(light * _Default_Idle_Colour.rgb, light * _Default_Active_Colour.rgb, _ISACTIVE);
                outColor = float4(lerp(ring, dream, _ISDREAM), 1.0);
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
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
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return input.positionCS.zzzz;
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
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
