// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Standard_Unlit, a URP 14 Unlit Shader Graph (platform 14).
// The forward output follows the decoded programs, including the Diff tiling applied twice when
// scrolling, the graph's own fog capped by _Fog_Max_Amount, and the unsaturated transparent alpha.
Shader "HardLight/Environment/HL_Env_Standard_Unlit"
{
    Properties
    {
        _Separator_0 ("#", Float) = 1
        _Title_Scrolling_UV ("## Scrolling UV", Float) = 1
        _REF_USE_SCROLLING_UVS ("!REF _USE_SCROLLING_UVS", Float) = 0
        [Toggle(_USE_SCROLLING_UVS)]_USE_SCROLLING_UVS ("Use Scrolling UVs?", Float) = 0
        _UV_Scrolling_Horizontal ("-- UV Scrolling Horizontal [_USE_SCROLLING_UVS]", Float) = 0
        _UV_Scrolling_Vertical ("-- UV Scrolling Vertical  [_USE_SCROLLING_UVS]", Float) = 0
        _Separator_1 ("# (1)", Float) = 1
        _Title_Diffuse ("## Diffuse", Float) = 1
        _Diff_Tex ("-- Diff Texture", 2D) = "white" {}
        [HDR]_Diff_Tint ("-- Diff Colour Tint", Color) = (1,1,1,0)
        [ToggleUI]_USE_VERTEX_COLOUR_TINT ("-- Use Vertex Colour Tint?", Float) = 0
        _Fog_Max_Amount ("-- Fog Max Amount", Range(0, 1)) = 1
        _Separator_2 ("# (2)", Float) = 1
        _Title_Camera_Distance_Fade ("## Camera Distance Fade (Transparent Only)", Float) = 1
        [ToggleUI]_Use_Camera_Distance_Fade ("-- Use Camera Distance Fade? [_Surface == 1]", Float) = 0
        _Camera_Distance_Fade_Far ("-- Camera Distance Fade Far [_Surface == 1]", Float) = 0
        _Camera_Distance_Fade_Near ("-- Camera Distance Fade Near [_Surface == 1]", Float) = 0
        _Separator_3 ("# (3)", Float) = 1
        [HideInInspector]_CastShadows ("_CastShadows", Float) = 1
        [HideInInspector]_Surface ("_Surface", Float) = 0
        [HideInInspector]_Blend ("_Blend", Float) = 0
        [HideInInspector]_AlphaClip ("_AlphaClip", Float) = 0
        [HideInInspector]_SrcBlend ("_SrcBlend", Float) = 1
        [HideInInspector]_DstBlend ("_DstBlend", Float) = 0
        [HideInInspector][ToggleUI]_ZWrite ("_ZWrite", Float) = 1
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
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Unlit" "Queue"="Geometry" "DisableBatching"="False" "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalUnlitSubTarget" }

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float _Title_Scrolling_UV;
            float _REF_USE_SCROLLING_UVS;
            float _Title_Diffuse;
            float _Separator_1;
            float _UV_Scrolling_Vertical;
            float4 _Diff_Tex_TexelSize;
            float4 _Diff_Tex_ST;
            float _Separator_2;
            float4 _Diff_Tint;
            float _Separator_0;
            float _UV_Scrolling_Horizontal;
            float _USE_VERTEX_COLOUR_TINT;
            float _Camera_Distance_Fade_Far;
            float _Title_Camera_Distance_Fade;
            float _Camera_Distance_Fade_Near;
            float _Use_Camera_Distance_Fade;
            float _Separator_3;
            float _Fog_Max_Amount;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;
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
            #pragma shader_feature_fragment _ _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _ _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma shader_feature_local _ _USE_SCROLLING_UVS
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            TEXTURE2D(_Diff_Tex);
            // The graph's inline sampler: linear, repeat, 2x anisotropic.
            SAMPLER(SamplerState_Linear_Repeat_Aniso2);

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

            // Preserve the shipped Metal w-z curve across APIs with different clip-depth conventions.
            float CameraFadeDistance(float4 positionCS)
            {
                #if UNITY_REVERSED_Z
                return positionCS.w - positionCS.z;
                #else
                return (positionCS.z - UNITY_NEAR_CLIP_VALUE * positionCS.w) / (1.0 - UNITY_NEAR_CLIP_VALUE);
                #endif
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // Scrolling offsets the tiled UV by game time, and the sample tiles it again.
                float2 uv = input.uv0.xy * _Diff_Tex_ST.xy + _Diff_Tex_ST.zw;
                #if defined(_USE_SCROLLING_UVS)
                uv += _HLShaderTimeGame * float2(_UV_Scrolling_Horizontal, _UV_Scrolling_Vertical);
                uv = uv * _Diff_Tex_ST.xy + _Diff_Tex_ST.zw;
                #endif
                float4 diffuse = SAMPLE_TEXTURE2D(_Diff_Tex, SamplerState_Linear_Repeat_Aniso2, uv) * _Diff_Tint;
                float3 color = lerp(diffuse.rgb, input.color.rgb * diffuse.rgb, _USE_VERTEX_COLOUR_TINT);

                // Shader Graph's Fog node, capped by _Fog_Max_Amount.
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                float viewZ = -TransformWorldToView(input.positionWS).z;
                float density = 1.0 - ComputeFogIntensity(ComputeFogFactorZ0ToFar(max(viewZ - _ProjectionParams.y, 0.0)));
                color = lerp(color, unity_FogColor.rgb, saturate(density * _Fog_Max_Amount));
                #endif

                #if defined(_SURFACE_TYPE_TRANSPARENT)
                float4 clipPosition = TransformWorldToHClip(input.positionWS);
                float fade = saturate((CameraFadeDistance(clipPosition) - _Camera_Distance_Fade_Near) /
                                      (_Camera_Distance_Fade_Far - _Camera_Distance_Fade_Near));
                float alpha = lerp(diffuse.a, diffuse.a * fade, _Use_Camera_Distance_Fade);
                #if defined(_ALPHAMODULATE_ON)
                color = lerp(1.0, color, alpha);
                #endif
                #else
                float alpha = 1.0;
                #endif
                outColor = float4(color, alpha);
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
            #pragma shader_feature_local _ _USE_SCROLLING_UVS
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
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
                float3 positionWS : TEXCOORD1;
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
            #pragma shader_feature_local _ _USE_SCROLLING_UVS
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
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
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv0 = input.uv0;
                output.positionWS = positionWS;
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
