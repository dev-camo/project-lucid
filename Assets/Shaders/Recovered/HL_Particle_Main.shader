// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Particle/HL_Particle_Main, a URP 14 Unlit Shader Graph (platform 14).
// The forward output follows the decoded programs exactly, including the twice-applied
// _MainTex tiling and the absence of fog in the output colour.
Shader "HardLight/Particle/HL_Particle_Main"
{
    Properties
    {
        [ToggleUI]_Use_Texture_Colour ("Use Texture Colour?", Float) = 0
        _Primary_Color ("Primary Color [!_Use_Texture_Colour]", Color) = (1,1,1,0)
        _Secondary_Color ("Secondary Color [!_Use_Texture_Colour]", Color) = (1,1,1,0)
        _Color_Offset ("Color Offset [!_Use_Texture_Colour]", Range(-1, 1)) = 0
        _MainTex ("MainTex", 2D) = "white" {}
        _UV_Speed_XY ("UV Speed XY", Vector) = (0,0,0,0)
        [HDR]_HDR ("HDR", Color) = (1,1,1,1)
        _Softness ("Softness", Float) = 1
        [ToggleUI]_Use_Alpha_Instead_of_Red ("Use Alpha Instead of Red", Float) = 0
        [Toggle(_GRADIENTMAPENABLE)]_GRADIENTMAPENABLE ("GradientMapEnable [!_Use_Texture_Colour]", Float) = 0
        [NoScaleOffset]_gradienmap ("gradienmap [!_Use_Texture_Colour]", 2D) = "white" {}
        _GradientMapSpeed ("GradientMapSpeed [!_Use_Texture_Colour]", Float) = 1
        [Toggle(_DEPTHFADE)]_DEPTHFADE ("DepthFade", Float) = 0
        _DepthFadeDistance ("DepthFadeDistance", Float) = 1
        _DepthFadeStrength ("DepthFadeStrength", Float) = 1
        [ToggleUI]_Camera_Distance_Fade ("Camera Distance Fade", Float) = 0
        _Camera_Distance_Fade_Near ("Camera Distance Fade Near", Float) = 0
        _Camera_Distance_Fade_Far ("Camera Distance Fade Far", Float) = 0
        [HideInInspector]_CastShadows ("_CastShadows", Float) = 0
        [HideInInspector]_Surface ("_Surface", Float) = 1
        [HideInInspector]_Blend ("_Blend", Float) = 0
        [HideInInspector]_AlphaClip ("_AlphaClip", Float) = 0
        [HideInInspector]_SrcBlend ("_SrcBlend", Float) = 1
        [HideInInspector]_DstBlend ("_DstBlend", Float) = 0
        [HideInInspector][ToggleUI]_ZWrite ("_ZWrite", Float) = 0
        [HideInInspector]_ZWriteControl ("_ZWriteControl", Float) = 0
        [HideInInspector]_ZTest ("_ZTest", Float) = 4
        [HideInInspector]_Cull ("_Cull", Float) = 2
        [HideInInspector]_AlphaToMask ("_AlphaToMask", Float) = 0
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
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_TexelSize;
            float4 _MainTex_ST;
            float4 _Primary_Color;
            float4 _Secondary_Color;
            float _Softness;
            float _DepthFadeDistance;
            float _DepthFadeStrength;
            float4 _gradienmap_TexelSize;
            float _Color_Offset;
            float _GradientMapSpeed;
            float _Camera_Distance_Fade;
            float _Camera_Distance_Fade_Near;
            float _Camera_Distance_Fade_Far;
            float _Use_Alpha_Instead_of_Red;
            float _Use_Texture_Colour;
            float2 _UV_Speed_XY;
            float4 _HDR;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_gradienmap);
        SAMPLER(sampler_gradienmap);
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Blend [_SrcBlend] [_DstBlend], [_SrcBlend] [_DstBlend]
            ZTest [_ZTest]
            ZWrite [_ZWrite]
            Cull [_Cull]
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma shader_feature _ _SAMPLE_GI
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #pragma shader_feature_fragment _ _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _ _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma shader_feature_local _ _DEPTHFADE
            #pragma shader_feature_local _ _GRADIENTMAPENABLE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 uv0 : TEXCOORD0;     // xy: UV, z: custom particle stream (dissolve)
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
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv0 = input.uv0;
                output.color = input.color;
                return output;
            }

            // Linear eye depth of the opaque scene behind this fragment, as the decoded program
            // derives it: _ZBufferParams for perspective, inverse view-projection for orthographic.
            float SceneEyeDepth(float4 positionCS)
            {
                float y = _ProjectionParams.x < 0.0 ? _ScaledScreenParams.y - positionCS.y : positionCS.y;
                float2 screenUV = float2(positionCS.x, y) / _ScaledScreenParams.xy;
                #if UNITY_UV_STARTS_AT_TOP
                screenUV.y = 1.0 - screenUV.y;
                #endif
                float rawDepth = SampleSceneDepth(screenUV);
                if (unity_OrthoParams.w == 1.0)
                {
                    // Metal stores reversed 0..1 depth; GL stores 0..1 but its inverse VP expects -1..1.
                    #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                    #endif
                    float3 positionWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                    return abs(mul(UNITY_MATRIX_V, float4(positionWS, 1.0)).z);
                }
                return 1.0 / (_ZBufferParams.z * rawDepth + _ZBufferParams.w);
            }

            float CameraFadeDistance(float4 clipPosition)
            {
                // Keep the shipped Metal w-z curve, including its orthographic normalization.
                #if UNITY_REVERSED_Z
                return clipPosition.w - clipPosition.z;
                #else
                return (clipPosition.z - UNITY_NEAR_CLIP_VALUE * clipPosition.w) / (1.0 - UNITY_NEAR_CLIP_VALUE);
                #endif
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // The graph offsets by time and tiling, then the sample applies _MainTex_ST again.
                float2 uv = input.uv0.xy * _MainTex_ST.xy + (_HLShaderTimeGame * _UV_Speed_XY + _MainTex_ST.zw);
                uv = uv * _MainTex_ST.xy + _MainTex_ST.zw;
                // Core.hlsl supplies the single _GlobalMipBias.x through SAMPLE_TEXTURE2D.
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                float3 color;
                if (_Use_Texture_Colour != 0.0)
                {
                    color = tex.rgb * input.color.rgb;
                }
                else
                {
                    #if defined(_GRADIENTMAPENABLE)
                    float gradientU = frac(_TimeParameters.x * _GradientMapSpeed + tex.r * 0.5);
                    color = SAMPLE_TEXTURE2D(_gradienmap, sampler_gradienmap, gradientU.xx).rgb * input.color.rgb;
                    #else
                    color = lerp(_Secondary_Color.rgb, _Primary_Color.rgb, tex.r + _Color_Offset) * input.color.rgb;
                    #endif
                }

                // Red (or alpha) times vertex alpha, limited by the green-channel dissolve driven by
                // the custom stream, then softened.
                float alpha = lerp(tex.r, tex.a, _Use_Alpha_Instead_of_Red) * input.color.a;
                alpha = min(alpha, tex.g - (input.uv0.z * 2.0 - 1.0));
                alpha = saturate(alpha / _Softness);

                float4 clipPosition = TransformWorldToHClip(input.positionWS);
                #if defined(_DEPTHFADE)
                alpha *= saturate((SceneEyeDepth(input.positionCS) - clipPosition.w) / _DepthFadeDistance * _DepthFadeStrength);
                #endif
                float distanceFade = saturate((CameraFadeDistance(clipPosition) - _Camera_Distance_Fade_Near) /
                                              (_Camera_Distance_Fade_Far - _Camera_Distance_Fade_Near));
                alpha = lerp(alpha, alpha * distanceFade, _Camera_Distance_Fade);

                // The colour is premultiplied in every variant; only transparent programs write the alpha.
                #if defined(_SURFACE_TYPE_TRANSPARENT)
                outColor = float4(alpha * color * _HDR.rgb, alpha);
                #else
                outColor = float4(alpha * color * _HDR.rgb, 1.0);
                #endif
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma shader_feature_local _ _DEPTHFADE
            #pragma shader_feature_local _ _GRADIENTMAPENABLE

            struct Attributes
            {
                float3 positionOS : POSITION;
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
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma shader_feature_local _ _DEPTHFADE
            #pragma shader_feature_local _ _GRADIENTMAPENABLE
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
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv0 = input.uv0;
                output.color = input.color;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - output.positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
