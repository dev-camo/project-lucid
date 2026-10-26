// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Zone5_Goo, a URP 14 Unlit Shader Graph (platform 14), opaque.
// Flow-mapped goo noise picks between two colour pairs (by vertex alpha) over a distorted dream texture with stars,
// contrasted and faded to a fake fog colour towards the UV rim.
Shader "HardLight/Environment/HL_Env_Zone5_Goo"
{
    Properties
    {
        _Separator_0 ("#", Float) = 0
        _Title_Vertex_Colour ("## Vertex Colour", Float) = 1
        _Title_Red ("### Red =", Float) = 1
        _Title_Green ("### Green =", Float) = 1
        _Title_Blue ("### Blue =", Float) = 1
        _Title_Alpha_Colour_Blend ("### Alpha = Colour Blend", Float) = 1
        _Separator_1 ("# (1)", Float) = 0
        _Title_Surface_Input ("## Surface Input", Float) = 0
        _Separator_2 ("# (2)", Float) = 0
        _NOTE_Mask_Vertex_Alpha_Channel ("!NOTE Mask: Vertex Alpha Channel", Float) = 0
        _Color_A ("-- Color A", Color) = (0.2470588,0.1137255,0.5372549,1)
        _Color_B ("-- Color B", Color) = (0.2392157,0.3333333,0.8196079,1)
        _Color_C ("-- Color C", Color) = (0.7960784,0.4470588,0.972549,1)
        _Color_D ("-- Color D", Color) = (0.2784314,0.5019608,0.7450981,1)
        _Separator_4 ("# (4)", Float) = 0
        _Title_Noise ("## Noise", Float) = 0
        _Goo_Noise_Tex ("-- Goo Noise Texture", 2D) = "white" {}
        _Goo_Noise_Contrast ("-- Red Goo Noise Contrast", Float) = 2
        _Green_Goo_Noise_Contrast ("-- Green Goo Noise Contrast", Float) = 1
        _Separator_5 ("# (5)", Float) = 0
        _Title_FlowMap ("## FlowMap", Float) = 0
        _Flow_Map_Strength ("-- Flow Map Strength", Float) = 1
        [NoScaleOffset]_Flow_Map_Tex ("-- Flow Map Texture", 2D) = "linearGrey" {}
        _Flow_Map_Speed ("-- Flow Map Speed", Float) = 0.2
        _Separator_6 ("# (6)", Float) = 0
        _Title_Vertex_Displacement ("## Dream Distortion", Float) = 0
        [NoScaleOffset]_Dream_Texture ("-- Dream Texture", 2D) = "white" {}
        _Distortion_Intensity ("-- Distortion Intensity", Range(0, 0.2)) = 0
        _Distortion_Intensity_Stars ("-- Distortion Intensity Stars", Range(0, 1)) = 0
        _Distortion_Texture ("-- Distortion Texture", 2D) = "white" {}
        _Distortion_Speed_X ("-- Distortion Speed X", Range(0, 1)) = 0
        _Distortion_Speed_Y ("-- Distortion Speed Y", Range(0, 1)) = 0
        _Separator_8 ("# (7)", Float) = 0
        _Title_Vertex_Displacement_2 ("## Stars", Float) = 0
        _Stars_Texture ("-- Stars Texture", 2D) = "white" {}
        _Stars_Opacity ("-- Stars Opacity", Range(0, 1)) = 1
        [HDR]_Stars_Emission ("-- Stars Colour", Color) = (0,0.8931146,2,0)
        _Separator_11 ("# (8)", Float) = 0
        _Title_Global_Properties_1 ("## Fake Fog", Float) = 1
        _Fake_Fog_Color ("-- Fake Fog Color", Color) = (1,0.5921569,0.8941177,1)
        _Separator_9 ("# (9)", Float) = 1
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
            float _Title_Red;
            float _Title_Green;
            float _Title_Vertex_Colour;
            float _Title_Alpha_Colour_Blend;
            float _Title_Blue;
            float _Separator_9;
            float _Separator_4;
            float _Title_Surface_Input;
            float _Separator_5;
            float _Separator_0;
            float _Separator_1;
            float4 _Color_C;
            float4 _Color_D;
            float4 _Goo_Noise_Tex_TexelSize;
            float4 _Goo_Noise_Tex_ST;
            float _Separator_2;
            float _Title_FlowMap;
            float4 _Color_B;
            float4 _Color_A;
            float _Flow_Map_Strength;
            float _Green_Goo_Noise_Contrast;
            float _Flow_Map_Speed;
            float _Goo_Noise_Contrast;
            float4 _Flow_Map_Tex_TexelSize;
            float _Title_Vertex_Displacement;
            float _Title_Noise;
            float _Separator_6;
            float _Separator_11;
            float _Title_Vertex_Displacement_2;
            float _Separator_8;
            float _NOTE_Mask_Vertex_Alpha_Channel;
            float _Distortion_Intensity;
            float _Distortion_Speed_X;
            float _Distortion_Speed_Y;
            float4 _Distortion_Texture_TexelSize;
            float4 _Distortion_Texture_ST;
            float4 _Dream_Texture_TexelSize;
            float4 _Stars_Texture_TexelSize;
            float4 _Stars_Texture_ST;
            float _Stars_Opacity;
            float4 _Stars_Emission;
            float _Distortion_Intensity_Stars;
            float _Title_Global_Properties_1;
            float4 _Fake_Fog_Color;
        CBUFFER_END

        // Game-scaled shader time published by the time manager, and the global goo scroll direction.
        float _HLShaderTimeGame;
        float _UV_Scrolling_Direction_X;
        float _UV_Scrolling_Direction_Y;
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

            TEXTURE2D(_Goo_Noise_Tex);
            TEXTURE2D(_Flow_Map_Tex);
            TEXTURE2D(_Distortion_Texture);
            TEXTURE2D(_Dream_Texture);
            TEXTURE2D(_Stars_Texture);
            // The graph's inline sampler: linear, repeat.
            SAMPLER(SamplerState_Linear_Repeat);

            // The goo colour. The distortion texture (tiled, offset and scrolled by game time times its speed)
            // offsets UV0 for the dream texture (lifted by 0.05, its alpha lowered by 0.24) and, by its own intensity,
            // for the stars (emission-tinted, plus 0.45 minus their alpha), which cover the saturated dream by
            // saturate(dream alpha * star alpha * opacity). The goo noise is flow-mapped: two phases 0.5 apart
            // offset the scrolled, tiled UV along the flow map (times -2 about 0.5, times its strength) and are tiled
            // again, blending by their triangle wave. Its red, contrasted about 0.1, picks within A..B and C..D, and
            // vertex alpha picks between the pairs; it adds to the dream, contrasted 1.3 about 0.2176. Towards the
            // UV rim (a smoothstep from radius 0.23 to 0.46) it fades to the fake fog colour plus the noise's green
            // (times its contrast). The graph's output scale is 1.498.
            float3 Zone5GooColour(float2 uv, float4 color)
            {
                float2 distortionUV = uv * _Distortion_Texture_ST.xy + (_HLShaderTimeGame * float2(_Distortion_Speed_X, _Distortion_Speed_Y) + _Distortion_Texture_ST.zw);
                float2 distortion = SAMPLE_TEXTURE2D(_Distortion_Texture, SamplerState_Linear_Repeat, distortionUV).xy;
                float4 dream = SAMPLE_TEXTURE2D(_Dream_Texture, SamplerState_Linear_Repeat, uv + distortion * _Distortion_Intensity) + float4(0.05, 0.05, 0.05, -0.24);
                float2 starsUV = (uv + _Distortion_Intensity_Stars * distortion) * _Stars_Texture_ST.xy + _Stars_Texture_ST.zw;
                float4 stars = SAMPLE_TEXTURE2D(_Stars_Texture, SamplerState_Linear_Repeat, starsUV);
                float3 starColour = _Stars_Emission.rgb * stars.rgb + (0.45 - stars.a);
                float3 colour = lerp(saturate(dream.rgb), starColour, saturate(dream.a * stars.a * _Stars_Opacity));

                float2 gooUV = (uv + _HLShaderTimeGame * float2(_UV_Scrolling_Direction_X, _UV_Scrolling_Direction_Y)) * _Goo_Noise_Tex_ST.xy + _Goo_Noise_Tex_ST.zw;
                float2 flow = (SAMPLE_TEXTURE2D(_Flow_Map_Tex, SamplerState_Linear_Repeat, gooUV).xy - 0.5) * -2.0 * _Flow_Map_Strength;
                float phaseA = frac(_HLShaderTimeGame * _Flow_Map_Speed + 0.5);
                float phaseB = frac(_HLShaderTimeGame * _Flow_Map_Speed);
                float2 noiseA = SAMPLE_TEXTURE2D(_Goo_Noise_Tex, SamplerState_Linear_Repeat, (gooUV + flow * phaseA) * _Goo_Noise_Tex_ST.xy + _Goo_Noise_Tex_ST.zw).xy;
                float2 noiseB = SAMPLE_TEXTURE2D(_Goo_Noise_Tex, SamplerState_Linear_Repeat, (gooUV + flow * phaseB) * _Goo_Noise_Tex_ST.xy + _Goo_Noise_Tex_ST.zw).xy;
                float2 noise = lerp(noiseB, noiseA, abs(phaseB * 2.0 - 1.0));
                float red = saturate((noise.x - 0.1) * _Goo_Noise_Contrast + 0.1);
                float green = saturate(noise.y * _Green_Goo_Noise_Contrast);
                float3 pairAB = lerp(_Color_A.rgb, _Color_B.rgb, red);
                float3 pairCD = lerp(_Color_C.rgb, _Color_D.rgb, red);
                colour += lerp(pairCD, pairAB, saturate(color.a));
                colour = (colour - 0.217637643) * 1.3 + 0.217637643;

                float rim = saturate((length(uv - 0.5) - 0.23) * 4.347826);
                rim = min(rim * rim * (3.0 - 2.0 * rim), 1.0);
                colour = lerp(rim * green + _Fake_Fog_Color.rgb, colour, 1.0 - rim);
                return colour * 1.49803901;
            }

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

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                outColor = float4(Zone5GooColour(input.uv0.xy, input.color), 1.0);
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
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
