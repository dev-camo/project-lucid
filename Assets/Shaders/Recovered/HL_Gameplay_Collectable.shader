// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Gameplay/HL_Gameplay_Collectable, a URP 14 Unlit Shader Graph (platform 14).
// Every pass spins the mesh about its object Y axis by game time; the colour is an unlit matcap.
Shader "HardLight/Gameplay/HL_Gameplay_Collectable"
{
    Properties
    {
        _Seperator_0 ("#", Float) = 0
        _Title_Matcap ("## Matcap", Float) = 1
        [NoScaleOffset]_Matcap_Tex ("-- Matcap Texture", 2D) = "white" {}
        _Matcap_Border_Push_Amount ("-- Matcap Border Push Amount", Float) = 0.45
        _Seperator_1 ("# (1)", Float) = 0
        _Title_Vertex_Colour ("## Surface Properties", Float) = 1
        [ToggleUI]_USE_VERTEX_COLOUR_TINT ("-- Use Vertex Colour Tint?", Float) = 0
        [HDR]_Diff_Tint ("-- Diff Colour Tint", Color) = (1,1,1,0)
        _Seperator_2 ("# (2)", Float) = 0
        _Title_Vertex_Animation ("## Vertex Animation", Float) = 0
        _Rotation_Speed ("-- Rotation Speed", Float) = 0
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
            float _Title_Matcap;
            float4 _Matcap_Tex_TexelSize;
            float _Matcap_Border_Push_Amount;
            float _Seperator_1;
            float _Title_Vertex_Colour;
            float _USE_VERTEX_COLOUR_TINT;
            float _Seperator_2;
            float _Title_Vertex_Animation;
            float _Seperator_0;
            float _Rotation_Speed;
            float4 _Diff_Tint;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;

        TEXTURE2D(_Matcap_Tex);
        SAMPLER(sampler_Matcap_Tex);

        // The graph's vertex rotation: object space about Y, applied to position and normal alike.
        float3 SpinAboutY(float3 v)
        {
            float angle = _HLShaderTimeGame * _Rotation_Speed;
            float s, c;
            sincos(angle, s, c);
            return float3(c * v.x + s * v.z, v.y, -s * v.x + c * v.z);
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
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : TEXCOORD0;
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
                output.positionWS = TransformObjectToWorld(SpinAboutY(input.positionOS));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(SpinAboutY(input.normalOS));
                output.color = input.color;
                return output;
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // Matcap from the cross product of the view-space view direction and normal,
                // pushed out from the centre by the border amount (v flipped). Core.hlsl supplies the single
                // _GlobalMipBias.x through SAMPLE_TEXTURE2D.
                float3 viewVS = normalize(mul((float3x3)UNITY_MATRIX_V, GetWorldSpaceNormalizeViewDir(input.positionWS)));
                float3 normalVS = mul(input.normalWS / length(input.normalWS), (float3x3)UNITY_MATRIX_I_V);
                float3 rim = cross(viewVS, normalVS);
                float2 uv = float2(rim.y, -rim.x) * _Matcap_Border_Push_Amount + 0.5;
                float3 matcap = SAMPLE_TEXTURE2D(_Matcap_Tex, sampler_Matcap_Tex, uv).rgb;
                matcap = lerp(matcap, input.color.rgb * matcap, _USE_VERTEX_COLOUR_TINT);
                outColor = float4(matcap * _Diff_Tint.rgb, 1.0);
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

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : TEXCOORD0;
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
                output.positionWS = TransformObjectToWorld(SpinAboutY(input.positionOS));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(SpinAboutY(input.normalOS));
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : TEXCOORD0;
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
                float3 positionWS = TransformObjectToWorld(SpinAboutY(input.positionOS));
                float3 normalWS = TransformObjectToWorldNormal(SpinAboutY(input.normalOS));
                output.positionWS = positionWS;
                output.normalWS = normalWS;
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
