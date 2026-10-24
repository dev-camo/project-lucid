// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Particle/HL_Particle_Trail, a URP 14 Unlit Shader Graph (platform 14).
// The depth fade has no keyword: every forward variant reads the camera depth texture.
Shader "HardLight/Particle/HL_Particle_Trail"
{
    Properties
    {
        [ToggleUI]_Use_PlayerAnim_Time_Category ("Use PlayerAnim Time Category?", Float) = 0
        [HDR]_Primary_Color ("Primary Color", Color) = (1,1,1,0)
        [HDR]_Secondary_Color ("Secondary Color", Color) = (1,1,1,0)
        _Color_Offset ("Color Offset", Range(-1, 1)) = 0
        _Channel_Picker ("Channel Picker", Vector) = (1,0,0,0)
        [NoScaleOffset]_Main_Texture ("Main Texture", 2D) = "white" {}
        _Softness ("Softness", Float) = 0.3
        _Tiling_and_PannerSpeed ("Tiling and PannerSpeed", Vector) = (1,1,-1,0)
        _DepthFade_Distance ("DepthFade Distance", Float) = 1
        _DepthFade_Strenght ("DepthFade Strenght", Float) = 1
        [Toggle(_GRADIENTMAPENABLE)]_GRADIENTMAPENABLE ("GradientMapEnable", Float) = 0
        [NoScaleOffset]_gradientMap ("gradientMap", 2D) = "white" {}
        _GradientMapSpeed ("GradientMapSpeed", Float) = 1
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
            float _Softness;
            float4 _Main_Texture_TexelSize;
            float _DepthFade_Distance;
            float _DepthFade_Strenght;
            float4 _Primary_Color;
            float4 _Secondary_Color;
            float4 _gradientMap_TexelSize;
            float4 _Channel_Picker;
            float _Color_Offset;
            float _GradientMapSpeed;
            float _Use_PlayerAnim_Time_Category;
        CBUFFER_END

        // Game-scaled and player-animation shader times published by the time manager.
        float _HLShaderTimeGame;
        float _HLShaderTimePlayerAnim;

        TEXTURE2D(_Main_Texture);
        SAMPLER(sampler_Main_Texture);
        TEXTURE2D(_gradientMap);
        SAMPLER(sampler_gradientMap);
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
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma shader_feature _ _SAMPLE_GI
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _ _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #pragma shader_feature_fragment _ _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local _ _GRADIENTMAPENABLE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

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
                    #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                    #endif
                    float3 positionWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                    return abs(mul(UNITY_MATRIX_V, float4(positionWS, 1.0)).z);
                }
                return 1.0 / (_ZBufferParams.z * rawDepth + _ZBufferParams.w);
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // Core.hlsl supplies the single _GlobalMipBias.x through SAMPLE_TEXTURE2D.
                float time = _Use_PlayerAnim_Time_Category != 0.0 ? _HLShaderTimePlayerAnim : _HLShaderTimeGame;
                float2 uv = input.uv0.xy * _Tiling_and_PannerSpeed.xy + frac(time * _Tiling_and_PannerSpeed.zw);
                float4 tex = SAMPLE_TEXTURE2D(_Main_Texture, sampler_Main_Texture, uv);

                // The picked channel, eroded by vertex alpha, then faded towards both trail edges (uv.y 0 and 1).
                float value = saturate(dot(_Channel_Picker, tex)) - ((1.0 - input.color.a) * 2.0 - 1.0);
                float edge = saturate((1.0 - input.uv0.y) * input.uv0.y * 3.33333325);
                float alpha = saturate(value * edge);

                #if defined(_GRADIENTMAPENABLE)
                float gradientU = frac(value * 0.5 + _TimeParameters.x * _GradientMapSpeed);
                float3 color = SAMPLE_TEXTURE2D(_gradientMap, sampler_gradientMap, gradientU.xx).rgb + alpha * 0.5;
                #else
                float3 color = lerp(_Secondary_Color.rgb, _Primary_Color.rgb, saturate(alpha + _Color_Offset));
                #endif
                color *= input.color.rgb;

                float clipW = TransformWorldToHClip(input.positionWS).w;
                float depthFade = saturate((SceneEyeDepth(input.positionCS) - clipW) / _DepthFade_Distance * _DepthFade_Strenght);
                alpha = saturate(depthFade * alpha / _Softness);

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
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
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
            float4 Frag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.zzzz; }
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
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
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
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.uv0 = input.uv0;
                output.color = input.color;
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
