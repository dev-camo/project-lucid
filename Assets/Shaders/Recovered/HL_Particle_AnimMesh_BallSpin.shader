// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Particle/HL_Particle_AnimMesh_BallSpin, a URP 14 Unlit Shader Graph (platform 14).
// The forward output follows the decoded programs: the fresnel term and the side mask apply in every shipped
// variant (_FRESNELENABLE does not change the compiled output), the fresnel normal is the mesh normal through the
// inverse world transform, and no shipped program enables _GRADIENTMAPENABLE, mixes fog or
// reads the depth fade properties.
Shader "HardLight/Particle/HL_Particle_AnimMesh_BallSpin"
{
    Properties
    {
        [Toggle(_USE_UV_MASK)]_USE_UV_MASK ("Use UV Mask", Float) = 1
        _SideMask_Min ("SideMask_Min", Float) = 0.1
        _SideMask_Max ("SideMask_Max", Float) = 0.2
        _Primary_Color ("Primary Color", Color) = (1,1,1,0)
        _Secondary_Color ("Secondary Color", Color) = (1,1,1,0)
        _Color_Offset ("Color Offset", Range(-1, 1)) = 0
        _Channel_Picker ("Channel Picker", Vector) = (1,0,0,0)
        [NoScaleOffset]_Main_Texture ("Main Texture", 2D) = "white" {}
        _Tiling_and_PannerSpeed ("Tiling and PannerSpeed", Vector) = (1,1,0,1)
        _Texture_Offset ("Texture Offset", Vector) = (0,0,0,0)
        _Softness ("Softness", Float) = 0.3
        [Toggle(_FRESNELENABLE)]_FRESNELENABLE ("FresnelEnable", Float) = 1
        _Fresnel_Min ("Fresnel Min", Float) = 0
        _Fresnel_Max ("Fresnel Max", Float) = 1
        _DepthFade_Distance ("DepthFade Distance", Float) = 1
        _DepthFade_Strenght ("DepthFade Strenght", Float) = 1
        [Toggle(_GRADIENTMAPENABLE)]_GRADIENTMAPENABLE ("GradientMapEnable", Float) = 0
        [NoScaleOffset]_gradientMap ("gradientMap", 2D) = "white" {}
        [HideInInspector]_CastShadows ("_CastShadows", Float) = 0
        [HideInInspector]_Surface ("_Surface", Float) = 1
        [HideInInspector]_Blend ("_Blend", Float) = 0
        [HideInInspector]_AlphaClip ("_AlphaClip", Float) = 0
        [HideInInspector]_SrcBlend ("_SrcBlend", Float) = 1
        [HideInInspector]_DstBlend ("_DstBlend", Float) = 0
        [HideInInspector][ToggleUI]_ZWrite ("_ZWrite", Float) = 0
        [HideInInspector]_ZWriteControl ("_ZWriteControl", Float) = 2
        [HideInInspector]_ZTest ("_ZTest", Float) = 4
        [HideInInspector]_Cull ("_Cull", Float) = 0
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
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float4 _Tiling_and_PannerSpeed;
            float _Fresnel_Min;
            float _Fresnel_Max;
            float _Softness;
            float4 _Main_Texture_TexelSize;
            float _DepthFade_Distance;
            float _DepthFade_Strenght;
            float4 _Primary_Color;
            float4 _Secondary_Color;
            float4 _gradientMap_TexelSize;
            float4 _Channel_Picker;
            float _Color_Offset;
            float2 _Texture_Offset;
            float _SideMask_Min;
            float _SideMask_Max;
        CBUFFER_END

        TEXTURE2D(_Main_Texture);
        SAMPLER(sampler_Main_Texture);

        struct Attributes
        {
            float3 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 uv0 : TEXCOORD0;     // xy: UV, z: custom particle stream subtracted from alpha
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

        // Forward and DepthOnly share this vertex program. As in all three original vertex programs, the normal is
        // the mesh normal times the world-to-object matrix (the inverse transpose), its squared length clamped to
        // FLT_MIN before normalizing: SafeNormalize in TransformObjectToWorldNormal.
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
            #pragma instancing_options renderinglayer
            // Declared as shipped; no shipped program mixes fog into the output.
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma shader_feature _ _SAMPLE_GI
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #pragma shader_feature_fragment _ _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _ _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma shader_feature_local _ _GRADIENTMAPENABLE
            #pragma shader_feature_local _ _FRESNELENABLE
            #pragma shader_feature_local _ _USE_UV_MASK
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            void Frag(Varyings input, bool isFrontFace : SV_IsFrontFace, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Facing-corrected fresnel against the view direction (or the camera's backward axis for
                // orthographic views).
                float3 normalWS = normalize(input.normalWS);
                normalWS = isFrontFace ? normalWS : -normalWS;
                float3 viewDirWS = unity_OrthoParams.w == 0.0 ? normalize(_WorldSpaceCameraPos - input.positionWS)
                                                              : UNITY_MATRIX_V[2].xyz;
                float fresnel = saturate((dot(normalWS, viewDirWS) - _Fresnel_Min) / _Fresnel_Max);

                // The panner scales the wrapped time, not the scaled time.
                float2 uv = input.uv0.xy * _Tiling_and_PannerSpeed.xy + frac(_TimeParameters.x) * _Tiling_and_PannerSpeed.zw + _Texture_Offset;
                // Core.hlsl supplies the single _GlobalMipBias.x through SAMPLE_TEXTURE2D.
                float4 tex = SAMPLE_TEXTURE2D(_Main_Texture, sampler_Main_Texture, uv);

                // The picked channel, optionally faded by a vertical UV ramp.
                float mask = saturate(dot(_Channel_Picker, tex));
                #if defined(_USE_UV_MASK)
                mask = saturate(mask - ((1.0 - input.uv0.y) * 2.0 - 1.0));
                #endif
                float3 color = lerp(_Secondary_Color.rgb, _Primary_Color.rgb, saturate(mask + _Color_Offset)) * input.color.rgb;

                // Softened alpha, faded toward the top and bottom edges by the v * (1 - v) side mask.
                float alpha = saturate((mask * input.color.a * fresnel - input.uv0.z) / _Softness);
                alpha *= saturate((input.uv0.y * (1.0 - input.uv0.y) - _SideMask_Min) / _SideMask_Max);

                // Opaque programs write alpha 1.
                #if defined(_SURFACE_TYPE_TRANSPARENT)
                outColor = float4(alpha * color, alpha);
                #else
                outColor = float4(alpha * color, 1.0);
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
            #pragma shader_feature_local _ _GRADIENTMAPENABLE
            #pragma shader_feature_local _ _FRESNELENABLE
            #pragma shader_feature_local _ _USE_UV_MASK

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
            #pragma vertex ShadowVert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma shader_feature_local _ _GRADIENTMAPENABLE
            #pragma shader_feature_local _ _FRESNELENABLE
            #pragma shader_feature_local _ _USE_UV_MASK
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            // The shadow pass keeps the mesh normal, for the bias and the interpolant alike.
            Varyings ShadowVert(Attributes input)
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
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
