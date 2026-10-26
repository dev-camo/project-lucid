// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Hub/HL_Hub_Standard_Unlit, a URP 14 Unlit Shader Graph (platform 14),
// transparent. A distortion-offset, scrolling diffuse with a hue-cycling glitter facing away from
// the view; the alpha takes the vertex colour's red channel.
Shader "HardLight/Hub/HL_Hub_Standard_Unlit"
{
    Properties
    {
        _Seperator ("#", Float) = 0
        _Title_Surface_Inputs ("## Surface Inputs", Float) = 0
        _Alpha ("-- Alpha", Range(0, 1)) = 1
        _Diff_Texture ("-- Diff Texture", 2D) = "white" {}
        _Diff_UV_Speed_X ("-- Diff UV Speed X", Range(0, 1)) = 0
        _Diff_UV_Speed_Y ("-- Diff UV Speed Y", Range(0, 1)) = 0
        _Seperator_1 ("# (1)", Float) = 0
        _Title_Distortion ("## Distortion", Float) = 0
        _Distortion_Intensity ("-- Distortion Intensity", Range(0, 0.2)) = 0
        _Distortion_Texture ("-- Distortion Texture", 2D) = "white" {}
        _Distortion_Speed_X ("-- Distortion Speed X", Range(0, 1)) = 0
        _Distortion_Speed_Y ("-- Distortion Speed Y", Range(0, 1)) = 0
        _Seperator_3 ("# (3)", Float) = 0
        _Title_Glitter ("## Glitter", Float) = 0
        [HDR]_Glitter_Colour ("-- Glitter Colour", Color) = (1,1,1,0)
        _Glitter_UV_Tiling ("-- Glitter UV Tiling", Float) = 5
        _Gliter_Amount ("-- Gliter Amount", Float) = 0.5
        _Glitter_Speed ("-- Glitter Speed", Float) = 0
        _Glitter_Opacity ("-- Glitter Opacity", Range(0, 1)) = 0
        [NoScaleOffset]_Glitter_Texture ("Glitter Texture", 2D) = "white" {}
        _Seperator_4 ("# (4)", Float) = 0
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
            float4 _Diff_Texture_TexelSize;
            float4 _Diff_Texture_ST;
            float _Distortion_Intensity;
            float _Seperator_1;
            float _Diff_UV_Speed_X;
            float _Diff_UV_Speed_Y;
            float _Distortion_Speed_X;
            float _Distortion_Speed_Y;
            float4 _Distortion_Texture_TexelSize;
            float4 _Distortion_Texture_ST;
            float _Alpha;
            float _Seperator_3;
            float _Title_Surface_Inputs;
            float _Title_Distortion;
            float _Title_Glitter;
            float _Seperator;
            float _Glitter_Speed;
            float _Gliter_Amount;
            float _Glitter_UV_Tiling;
            float _Seperator_4;
            float4 _Glitter_Colour;
            float _Glitter_Opacity;
            float4 _Glitter_Texture_TexelSize;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
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
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            TEXTURE2D(_Diff_Texture);
            TEXTURE2D(_Distortion_Texture);
            TEXTURE2D(_Glitter_Texture);
            // The graph's inline sampler: linear, repeat.
            SAMPLER(SamplerState_Linear_Repeat);

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

            // Shader Graph's Hue node (normalized offset): RGB to HSV, shift and wrap the hue, back to RGB.
            float3 HueNormalized(float3 rgb, float offset)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 P = lerp(float4(rgb.bg, K.wz), float4(rgb.gb, K.xy), step(rgb.b, rgb.g));
                float4 Q = lerp(float4(P.xyw, rgb.r), float4(rgb.r, P.yzx), step(P.x, rgb.r));
                float D = Q.x - min(Q.w, Q.y);
                float E = 1e-10;
                float V = (D == 0) ? Q.x : (Q.x + E);
                float3 hsv = float3(abs(Q.z + (Q.w - Q.y) / (6.0 * D + E)), D / (Q.x + E), V);
                float hue = hsv.x + offset;
                hsv.x = (hue < 0) ? hue + 1 : (hue > 1) ? hue - 1 : hue;
                float4 K2 = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 P2 = abs(frac(hsv.xxx + K2.xyz) * 6.0 - K2.www);
                return hsv.z * lerp(K2.xxx, saturate(P2 - K2.xxx), hsv.y);
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = input.uv0.xy;

                // Glitter: the hue-cycled glitter texture less the amount, normalised, against (1 - view).
                float3 glitterRGB = SAMPLE_TEXTURE2D(_Glitter_Texture, SamplerState_Linear_Repeat, uv * _Glitter_UV_Tiling).rgb;
                float3 sparkle = normalize(HueNormalized(glitterRGB, _Glitter_Speed * _HLShaderTimeGame) - _Gliter_Amount);
                float glitter = saturate(dot(sparkle, 1.0 - GetWorldSpaceNormalizeViewDir(input.positionWS)));
                float glitterMask = glitter * _Glitter_Opacity;

                // Diffuse: UV pushed by the scrolling distortion texture, scrolled, then tiled.
                float2 distortionUV = uv * _Distortion_Texture_ST.xy + (_HLShaderTimeGame * float2(_Distortion_Speed_X, _Distortion_Speed_Y) + _Distortion_Texture_ST.zw);
                float2 distortion = SAMPLE_TEXTURE2D(_Distortion_Texture, SamplerState_Linear_Repeat, distortionUV).xy;
                float2 diffuseUV = (distortion * _Distortion_Intensity + uv + _HLShaderTimeGame * float2(_Diff_UV_Speed_X, _Diff_UV_Speed_Y)) * _Diff_Texture_ST.xy + _Diff_Texture_ST.zw;
                float4 diffuse = SAMPLE_TEXTURE2D(_Diff_Texture, SamplerState_Linear_Repeat, diffuseUV);

                outColor.rgb = lerp(diffuse.rgb, glitter * _Glitter_Colour.rgb, glitterMask);
                outColor.a = saturate((diffuse.a * input.color.r + glitterMask) * _Alpha);
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
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
