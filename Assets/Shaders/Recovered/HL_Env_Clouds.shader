// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Clouds, a URP 14 Unlit Shader Graph (platform 14), opaque. A ramp
// indexed by the wrapped Lambert main light (scaled by the sample offset) tints the cloud, and a fresnel rim fades
// to the fresnel colour.
Shader "HardLight/Environment/HL_Env_Clouds"
{
    Properties
    {
        _Separator_0 ("#", Float) = 0
        _Title_SubSurfaceScattering ("## Sub Surface Scattering", Float) = 0
        [NoScaleOffset]_SubSurfaceScattering_Ramp_Tex ("-- SSS Texture Ramp", 2D) = "white" {}
        _SubSurfaceScattering_Opacity ("-- SSS Opacity", Range(0, 1)) = 1
        _SubSurfaceScattering_Intensity ("-- SSS Intensity", Range(1, 2)) = 1
        _SubSurfaceScattering_Sample_Offset ("-- SSS Sample Offset", Range(0.01, 0.99)) = 1
        _SubSurfaceScattering_Colour_Tint ("-- SSS Colour Tint", Color) = (1,1,1,0)
        _Separator_1 ("# (1)", Float) = 0
        _Title_Fresnel ("## Fresnel", Float) = 0
        _Fresnel_Colour ("-- Fresnel Colour", Color) = (1,1,1,0)
        _Fresnel_Amount ("-- Fresnel Amount", Range(0, 10)) = 5
        _Fresnel_Intensity ("-- Fresnel Intensity", Range(1, 20)) = 1
        _Fresnel_Opacity ("-- Fresnel Opacity", Range(0, 1)) = 0
        _Separator_2 ("# (2)", Float) = 0
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
            float _Title_SubSurfaceScattering;
            float _Separator_2;
            float _Fresnel_Opacity;
            float _Fresnel_Intensity;
            float4 _SubSurfaceScattering_Colour_Tint;
            float _Separator_1;
            float4 _Fresnel_Colour;
            float _Separator_0;
            float _Fresnel_Amount;
            float _SubSurfaceScattering_Sample_Offset;
            float _SubSurfaceScattering_Intensity;
            float _SubSurfaceScattering_Opacity;
            float4 _SubSurfaceScattering_Ramp_Tex_TexelSize;
            float _Title_Fresnel;
        CBUFFER_END
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

            TEXTURE2D(_SubSurfaceScattering_Ramp_Tex);
            // The graph's inline sampler: linear, clamp.
            SAMPLER(SamplerState_Linear_Clamp);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
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
                return output;
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // The view direction: towards the camera, or the camera's back axis when orthographic.
                float3 viewWS = unity_OrthoParams.w == 0.0 ? normalize(_WorldSpaceCameraPos - input.positionWS) : UNITY_MATRIX_V[2].xyz;
                viewWS = normalize(viewWS);
                float3 normalWS = input.normalWS / length(input.normalWS);

                // Subsurface scattering: the ramp at the wrapped Lambert main light times the sample offset (v 0), times
                // the intensity and tint, faded in from the tint by that ramp coordinate plus the transparency.
                float rampU = saturate((dot(normalWS, _MainLightPosition.xyz) * 0.5 + 0.5) * _SubSurfaceScattering_Sample_Offset);
                float3 ramp = SAMPLE_TEXTURE2D(_SubSurfaceScattering_Ramp_Tex, SamplerState_Linear_Clamp, float2(rampU, saturate(0.0))).rgb;
                float3 tint = _SubSurfaceScattering_Colour_Tint.rgb;
                float3 sss = lerp(tint, tint * ramp * _SubSurfaceScattering_Intensity, saturate(rampU + 1.0 - _SubSurfaceScattering_Opacity));

                // Fresnel rim: pow(1 - N.V, amount) times the intensity, its opacity fading towards the fresnel colour.
                float fresnel = pow(1.0 - saturate(dot(normalize(normalWS), viewWS)), _Fresnel_Amount) * _Fresnel_Intensity;
                float3 color = lerp(_Fresnel_Colour.rgb, sss, saturate(1.0 - fresnel * _Fresnel_Opacity));

                outColor = float4(color, 1.0);
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
            float4 Frag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.zzzz; }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
