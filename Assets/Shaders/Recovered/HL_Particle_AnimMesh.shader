// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Particle/HL_Particle_AnimMesh, a URP 14 Unlit Shader Graph (platform 14).
// The forward output follows the decoded programs: the fresnel term and the depth fade apply in
// every shipped variant (_FRESNELENABLE does not change the compiled output), and there is no fog.
Shader "HardLight/Particle/HL_Particle_AnimMesh"
{
    Properties
    {
        [ToggleUI]_Use_PlayerAnim_Time_Category ("Use PlayerAnim Time Category?", Float) = 0
        [ToggleUI]_Add_VertexColor_Alpha_at_end ("Add VertexColor Alpha at end?", Float) = 0
        [Toggle(_USE_UV_MASK)]_USE_UV_MASK ("Use UV Mask", Float) = 1
        [HDR]_Primary_Color ("Primary Color", Color) = (1,1,1,0)
        [HDR]_Secondary_Color ("Secondary Color", Color) = (1,1,1,0)
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
        _GradientMapSpeed ("GradientMapSpeed", Float) = 1
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

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float4 _Tiling_and_PannerSpeed;
            float _Camera_Distance_Fade_Far;
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
            float _GradientMapSpeed;
            float _Camera_Distance_Fade_Near;
            float _Camera_Distance_Fade;
            float _Add_VertexColor_Alpha_at_end;
            float _Use_PlayerAnim_Time_Category;
        CBUFFER_END

        // Scaled shader times published by the time manager.
        float _HLShaderTimePlayerAnim;
        float _HLShaderTimeGame;

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
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            // Declared as shipped; no shipped program mixes fog into the output.
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #pragma shader_feature_local_fragment _ _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _ _FRESNELENABLE
            #pragma shader_feature_local _ _GRADIENTMAPENABLE
            #pragma shader_feature_local _ _USE_UV_MASK
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

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
                screenUV.y = 1.0 - screenUV.y;
                float rawDepth = SampleSceneDepth(screenUV);
                if (unity_OrthoParams.w == 1.0)
                {
                    float2 ndc = screenUV * 2.0 - 1.0;
                    float4 positionWS = mul(UNITY_MATRIX_I_VP, float4(ndc.x, -ndc.y, rawDepth, 1.0));
                    positionWS.xyz /= positionWS.w;
                    return abs(mul(UNITY_MATRIX_V, float4(positionWS.xyz, 1.0)).z);
                }
                return 1.0 / (_ZBufferParams.z * rawDepth + _ZBufferParams.w);
            }

            void Frag(Varyings input, bool isFrontFace : SV_IsFrontFace, out half4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float time = _Use_PlayerAnim_Time_Category != 0.0 ? _HLShaderTimePlayerAnim : _HLShaderTimeGame;
                float2 uv = input.uv0.xy * _Tiling_and_PannerSpeed.xy + frac(time * _Tiling_and_PannerSpeed.zw) + _Texture_Offset;
                // Core.hlsl supplies the single _GlobalMipBias.x through SAMPLE_TEXTURE2D.
                float4 tex = SAMPLE_TEXTURE2D(_Main_Texture, sampler_Main_Texture, uv);

                // The picked channel, optionally faded by a vertical UV ramp. The gradient lookup
                // reads the ramped value before it is saturated.
                float maskRaw = saturate(dot(_Channel_Picker, tex));
                #if defined(_USE_UV_MASK)
                maskRaw -= (1.0 - input.uv0.y) * 2.0 - 1.0;
                #endif
                float mask = saturate(maskRaw);

                #if defined(_GRADIENTMAPENABLE)
                float gradientU = frac(maskRaw * 0.5 + _TimeParameters.x * _GradientMapSpeed);
                float3 color = SAMPLE_TEXTURE2D(_gradientMap, sampler_gradientMap, gradientU.xx).rgb + mask * 0.5;
                #else
                float3 color = lerp(_Secondary_Color.rgb, _Primary_Color.rgb, saturate(mask + _Color_Offset));
                #endif
                color *= input.color.rgb;

                // Facing-corrected fresnel against the view direction (or the camera forward for
                // orthographic views), times the soft-particle depth fade.
                float3 normalWS = normalize(input.normalWS);
                normalWS = isFrontFace ? normalWS : -normalWS;
                // Orthographic views use the view matrix's third row, the camera's backward axis.
                float3 viewDirWS = unity_OrthoParams.w == 0.0 ? normalize(_WorldSpaceCameraPos - input.positionWS)
                                                              : UNITY_MATRIX_V[2].xyz;
                float fade = saturate((dot(normalWS, viewDirWS) - _Fresnel_Min) / _Fresnel_Max);
                float4 clipPosition = TransformWorldToHClip(input.positionWS);
                fade *= saturate((SceneEyeDepth(input.positionCS) - clipPosition.w) / _DepthFade_Distance * _DepthFade_Strenght);

                float alpha = (mask * input.color.a * fade - input.uv0.z) / _Softness;
                float distanceFade = saturate((clipPosition.w - clipPosition.z - _Camera_Distance_Fade_Near) /
                                              (_Camera_Distance_Fade_Far - _Camera_Distance_Fade_Near));
                alpha = saturate(lerp(alpha, alpha * distanceFade, _Camera_Distance_Fade));

                // Opaque programs write alpha 1 and skip the end-of-graph vertex alpha.
                #if defined(_SURFACE_TYPE_TRANSPARENT)
                outColor = half4(alpha * color, _Add_VertexColor_Alpha_at_end != 0.0 ? alpha * input.color.a : alpha);
                #else
                outColor = half4(alpha * color, 1.0);
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
            #pragma shader_feature_local _ _FRESNELENABLE
            #pragma shader_feature_local _ _GRADIENTMAPENABLE
            #pragma shader_feature_local _ _USE_UV_MASK

            float4 Vert(float3 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS); }
            half Frag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
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
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _ _FRESNELENABLE
            #pragma shader_feature_local _ _GRADIENTMAPENABLE
            #pragma shader_feature_local _ _USE_UV_MASK
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 Vert(float3 positionOS : POSITION, float3 normalOS : NORMAL) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
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
                return positionCS;
            }
            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
