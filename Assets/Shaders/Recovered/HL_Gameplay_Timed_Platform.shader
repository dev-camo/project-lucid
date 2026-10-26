// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Gameplay/HL_Gameplay_Timed_Platform, a URP 14 Unlit Shader Graph
// (platform 14), transparent. A clock wipe over UV1 picks between two pattern colours; an optional
// glitch offsets and tints them; rotated, scrolling stripes set the alpha; the graph's own fog applies
// unless _Use_Scene_Fog is set.
Shader "HardLight/Gameplay/HL_Gameplay_Timed_Platform"
{
    Properties
    {
        _Seperator ("#", Float) = 0
        _Title_Surface_Inputs ("## Surface Inputs", Float) = 0
        [ToggleUI]_Use_Scene_Fog ("-- Use Scene Fog", Float) = 0
        [NoScaleOffset]__RGB_Pattern_Texture ("-- RGB Pattern Texture", 2D) = "white" {}
        _Main_Colour_UV_Tiling ("-- Main Colour UV Tiling", Float) = 1
        _Main_Colour_Edge_1 ("-- Main Colour Edge 1", Range(0, 1)) = 0
        _Main_Colour_Edge_2 ("-- Main Colour Edge 2", Range(0, 1)) = 1
        [HDR]_Main_Colour_A ("-- Main Colour A", Color) = (1,1,1,0)
        _Main_Colour_A_Lightness ("-- Main Colour A Lightness", Range(0, 1)) = 0
        [HDR]_Main_Colour_B ("-- Main Colour B", Color) = (0,0,0,0)
        _Main_Colour_B_Lightness ("-- Main Colour B Lightness", Range(0, 1)) = 0
        _Title_Mask ("## Mask", Float) = 0
        _Clock_Mask ("-- Clock Mask", Range(0, 1)) = 0
        [NoScaleOffset]_Clock_Mask_Pattern ("-- Clock Mask Pattern", 2D) = "black" {}
        _Title_Alpha ("## Alpha", Float) = 1
        _Alpha ("-- Alpha", Range(0, 1)) = 1
        _Seperator_1 ("# (1)", Float) = 0
        __Stripes ("## Stripes", Float) = 0
        [ToggleUI]_Use_Stripes ("-- Disable Stripes", Float) = 0
        _Stripes_Width_1 ("-- Stripes Width Edge 1", Range(0, 1)) = 0.4
        _Stripes_Width_2 ("-- Stripes Width Edge 2", Range(0, 1)) = 0.6
        _Stripe_Scale ("-- Stripe Scale", Float) = 5
        _Stripes_Rotation ("-- Stripes Rotation", Range(0, 1)) = 0
        _Stripe_Speed ("-- Stripe Speed", Range(-2, 2)) = 0
        _Stripe_Alpha_A ("-- Stripe Alpha A", Range(0, 1)) = 1
        _Stripe_Alpha_B ("-- Stripe Alpha B", Range(0, 1)) = 1
        _Title_Grid_1 ("## Distortion", Float) = 0
        _Distortion_Texture ("-- Distortion Texture", 2D) = "white" {}
        _Distortion_UV_Speed_Offset_X ("-- Distortion UV Speed/Offset X", Range(-1, 1)) = 0
        _Distortion_UV_Speed_Offset_Y ("-- Distortion UV Speed/Offset Y", Range(-1, 1)) = 0
        _Distortion_Amount ("-- Distortion Amount", Range(0, 1)) = 0
        _Seperator_3 ("# (3)", Float) = 0
        _Title_Glitch ("## Glitch", Float) = 0
        [ToggleUI]_Use_Glitch ("-- Use Glitch?", Float) = 0
        [ToggleUI]_Pause_Flicker ("-- Pause Flicker", Float) = 0
        _Flicker_Rate ("-- Flicker Rate", Float) = 1
        _Position_Offset ("-- Position Offset", Float) = 0
        _Min ("-- Min", Float) = 0
        _Max ("-- Max", Float) = 1
        _Glitch_Texture ("-- Glitch Texture", 2D) = "black" {}
        _Glitch_Texture_Contrast ("-- Glitch Texture Contrast", Float) = 1
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
            float _Seperator_1;
            float4 _Main_Colour_B;
            float4 _Main_Colour_A;
            float _Main_Colour_Edge_2;
            float _Main_Colour_Edge_1;
            float _Title_Surface_Inputs;
            float _Main_Colour_UV_Tiling;
            float _Seperator;
            float _Title_Mask;
            float _Main_Colour_B_Lightness;
            float _Title_Alpha;
            float __Stripes;
            float _Stripe_Speed;
            float _Stripe_Scale;
            float _Stripes_Width_2;
            float _Stripe_Alpha_A;
            float _Stripe_Alpha_B;
            float _Stripes_Width_1;
            float _Seperator_3;
            float4 __RGB_Pattern_Texture_TexelSize;
            float _Stripes_Rotation;
            float _Title_Grid_1;
            float _Distortion_Amount;
            float _Distortion_UV_Speed_Offset_Y;
            float _Use_Stripes;
            float _Distortion_UV_Speed_Offset_X;
            float4 _Distortion_Texture_TexelSize;
            float4 _Distortion_Texture_ST;
            float _Clock_Mask;
            float _Title_Glitch;
            float _Use_Glitch;
            float _Seperator_4;
            float _Main_Colour_A_Lightness;
            float _Alpha;
            float _Flicker_Rate;
            float _Use_Scene_Fog;
            float _Pause_Flicker;
            float _Position_Offset;
            float _Max;
            float _Min;
            float4 _Glitch_Texture_TexelSize;
            float4 _Glitch_Texture_ST;
            float _Glitch_Texture_Contrast;
            float4 _Clock_Mask_Pattern_TexelSize;
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
            // A graph keyword that the shipped programs compile identically with and without.
            #pragma shader_feature _ _FOG_LINEAR
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            TEXTURE2D(__RGB_Pattern_Texture);
            TEXTURE2D(_Distortion_Texture);
            TEXTURE2D(_Clock_Mask_Pattern);
            SAMPLER(sampler_Clock_Mask_Pattern);
            TEXTURE2D(_Glitch_Texture);
            SAMPLER(sampler_Glitch_Texture);
            // The graph's inline sampler: linear, repeat.
            SAMPLER(SamplerState_Linear_Repeat);
            #define PLATFORM_SAMPLE(tex, uv) SAMPLE_TEXTURE2D(tex, SamplerState_Linear_Repeat, uv)

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
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
                output.uv1 = input.uv1;
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
                float2 uv = input.uv0.xy;
                float time = _Pause_Flicker != 0.0 ? 0.0 : _HLShaderTimeGame;

                // Clock wipe: the UV1 polar angle, turned and offset by the clock mask, stepped at a full turn,
                // plus the clock pattern's red.
                float2 centred1 = input.uv1.xy - 0.5;
                float clock = step(1.0, atan2(centred1.y, centred1.x) * 0.159154922 + (_Clock_Mask * 2.0 + 1.0) * 0.5);
                float mask = clock + SAMPLE_TEXTURE2D(_Clock_Mask_Pattern, sampler_Clock_Mask_Pattern, uv).r;

                // Distortion: the texture tiled, scrolled by (paused) time, and tiled again.
                float2 distortionUV = (uv * _Distortion_Texture_ST.xy + _Distortion_Texture_ST.zw + time * float2(_Distortion_UV_Speed_Offset_X, _Distortion_UV_Speed_Offset_Y))
                                    * _Distortion_Texture_ST.xy + _Distortion_Texture_ST.zw;
                float2 distortion = PLATFORM_SAMPLE(_Distortion_Texture, distortionUV).xy * _Distortion_Amount;

                // Main colour: the pattern's blue, smoothstepped between the edges, lifting each colour by its
                // lightness, mixed by the clock mask.
                float pattern = PLATFORM_SAMPLE(__RGB_Pattern_Texture, (uv - 0.5) * _Main_Colour_UV_Tiling + 0.5 + distortion).b;
                float edge = smoothstep(_Main_Colour_Edge_1, _Main_Colour_Edge_2, pattern);
                float3 colorB = (edge + _Main_Colour_B_Lightness) * _Main_Colour_B.rgb;
                float3 color = lerp(colorB, (edge + _Main_Colour_A_Lightness) * _Main_Colour_A.rgb, mask);

                // Glitch: Shader Graph's Random Range over a seed that steps with time at the flicker rate,
                // offset per object by its world X/Z.
                float rate = _Flicker_Rate + 2.0;
                float2 seed = floor(fmod((_Position_Offset / GetObjectToWorldMatrix()._m03_m23 + rate) * time, rate));
                float random = frac(sin(dot(seed, float2(12.9898, 78.233))) * 43758.5453);
                float glitch = saturate(lerp(_Min, _Max, random));
                float2 glitchUV = (glitch + uv) * _Glitch_Texture_ST.xy + _Glitch_Texture_ST.zw;
                float3 glitched = color + _Glitch_Texture_Contrast * SAMPLE_TEXTURE2D(_Glitch_Texture, sampler_Glitch_Texture, glitchUV).rgb;
                color = _Use_Glitch != 0.0 ? glitched : color;

                // Shader Graph's Fog node; _Use_Scene_Fog takes it back out.
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                float viewZ = -TransformWorldToView(input.positionWS).z;
                float density = 1.0 - ComputeFogIntensity(ComputeFogFactorZ0ToFar(max(viewZ - _ProjectionParams.y, 0.0)));
                #else
                float density = 0.0;
                #endif
                color = lerp(lerp(color, unity_FogColor.rgb, density), color, _Use_Scene_Fog);

                // Stripes: the pattern's green over UV0 rotated about the centre (by _Stripes_Rotation * 3.145),
                // scaled, scrolled and distorted, between the two stripe alphas.
                float s, c;
                sincos(_Stripes_Rotation * 3.14499998, s, c);
                float2 centred = uv - 0.5;
                float2 rotated = float2(c * centred.x + s * centred.y, c * centred.y - s * centred.x);
                float2 stripeUV = rotated * _Stripe_Scale + 0.5 + (time * _Stripe_Speed + distortion);
                float stripe = smoothstep(_Stripes_Width_1, _Stripes_Width_2, PLATFORM_SAMPLE(__RGB_Pattern_Texture, stripeUV).g);
                float alpha = saturate(lerp(_Stripe_Alpha_A, _Stripe_Alpha_B, stripe) + _Use_Stripes);
                alpha = _Use_Glitch != 0.0 ? alpha * glitch : alpha;

                outColor = float4(color, saturate(alpha * _Alpha));
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
            #pragma shader_feature _ _FOG_LINEAR
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
                output.uv0 = input.uv0;
                return output;
            }
            float4 Frag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.zzzz; }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
