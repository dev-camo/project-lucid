// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Zone2_Barrier, a URP 14 Unlit Shader Graph (platform 14),
// transparent. A distorted pattern picks between two colours, an optional grid (faded out near the camera) overlays
// its colour, and rotated, scrolling stripes set the alpha.
Shader "HardLight/Environment/HL_Env_Zone2_Barrier"
{
    Properties
    {
        _Documentation_Link_Below ("## Documentation Link Below : HL_Env_Hologram", Float) = 0
        _Documentation_Link ("[https://hardlight.atlassian.net/wiki/spaces/HL/pages/8334020/SDT+-+HL+Env+Hologram](https://hardlight.atlassian.net/wiki/spaces/HL/pages/8334020/SDT+-+HL+Env+Hologram)", Float) = 0
        _Seperator ("#", Float) = 0
        _Title_Surface_Inputs ("## Surface Inputs", Float) = 0
        [NoScaleOffset]__RGB_Pattern_Texture ("-- RGB Pattern Texture", 2D) = "white" {}
        _Main_Colour_UV_Tiling ("-- Main Colour UV Tiling", Float) = 1
        _Main_Colour_Edge_1 ("-- Main Colour Edge 1", Range(0, 1)) = 0
        _Main_Colour_Edge_2 ("-- Main Colour Edge 2", Range(0, 1)) = 1
        [HDR]_Main_Colour_A ("-- Main Colour A", Color) = (1,1,1,0)
        [HDR]_Main_Colour_B ("-- Main Colour B", Color) = (0,0,0,0)
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
        _Title_Grid ("## Grid", Float) = 0
        [ToggleUI]_Enable_Grid ("-- Enable Grid?", Float) = 0
        _Grid_Color ("-- Grid Color", Color) = (0,0,0,1)
        _Grid_UV_Scale ("-- Grid UV Scale", Float) = 50
        _Grid_Pattern_Width ("-- Grid Pattern Width", Range(0, 1)) = 0.5
        _Grid_Camera_Fade_Distance ("-- Grid Camera Fade Distance", Float) = 20
        _Seperator_3 ("# (3)", Float) = 0
        _Title_Grid_1 ("## Distortion", Float) = 0
        _Distortion_Texture ("-- Distortion Texture", 2D) = "white" {}
        _Distortion_UV_Speed_Offset_X ("-- Distortion UV Speed/Offset X", Range(-1, 1)) = 0
        _Distortion_UV_Speed_Offset_Y ("-- Distortion UV Speed/Offset Y", Range(-1, 1)) = 0
        _Distortion_Amount ("-- Distortion Amount", Range(0, 1)) = 0
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
            float _Documentation_Link;
            float _Seperator_1;
            float4 _Main_Colour_B;
            float4 _Main_Colour_A;
            float _Main_Colour_Edge_2;
            float _Main_Colour_Edge_1;
            float _Title_Surface_Inputs;
            float _Main_Colour_UV_Tiling;
            float _Seperator;
            float __Stripes;
            float _Stripe_Speed;
            float _Stripe_Scale;
            float _Stripes_Width_2;
            float _Stripe_Alpha_A;
            float _Stripe_Alpha_B;
            float _Stripes_Width_1;
            float _Documentation_Link_Below;
            float _Seperator_3;
            float4 __RGB_Pattern_Texture_TexelSize;
            float _Title_Grid;
            float _Grid_UV_Scale;
            float _Grid_Pattern_Width;
            float _Grid_Camera_Fade_Distance;
            float _Enable_Grid;
            float _Seperator_4;
            float _Stripes_Rotation;
            float _Title_Grid_1;
            float4 _Grid_Color;
            float _Distortion_Amount;
            float _Distortion_UV_Speed_Offset_Y;
            float _Use_Stripes;
            float _Distortion_UV_Speed_Offset_X;
            float4 _Distortion_Texture_TexelSize;
            float4 _Distortion_Texture_ST;
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
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            TEXTURE2D(__RGB_Pattern_Texture);
            TEXTURE2D(_Distortion_Texture);
            // The graph's inline sampler: linear, repeat.
            SAMPLER(SamplerState_Linear_Repeat);
            #define PLATFORM_SAMPLE(tex, uv) SAMPLE_TEXTURE2D(tex, SamplerState_Linear_Repeat, uv)

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
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv0 = input.uv0;
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
                float time = _HLShaderTimeGame;

                // Distortion: the texture tiled, scrolled by time, and tiled again.
                float2 distortionUV = (uv * _Distortion_Texture_ST.xy + _Distortion_Texture_ST.zw + time * float2(_Distortion_UV_Speed_Offset_X, _Distortion_UV_Speed_Offset_Y))
                                    * _Distortion_Texture_ST.xy + _Distortion_Texture_ST.zw;
                float2 distortion = PLATFORM_SAMPLE(_Distortion_Texture, distortionUV).xy * _Distortion_Amount;

                // Main colour: the pattern's blue, smoothstepped between the edges, from colour A to colour B.
                float pattern = PLATFORM_SAMPLE(__RGB_Pattern_Texture, (uv - 0.5) * _Main_Colour_UV_Tiling + 0.5 + distortion).b;
                float3 color = lerp(_Main_Colour_A.rgb, _Main_Colour_B.rgb, smoothstep(_Main_Colour_Edge_1, _Main_Colour_Edge_2, pattern));

                // Grid: the pattern's red below the width, over the distorted UV0 scaled by the grid scale, in the grid
                // colour (weighted by its alpha). The camera fade is the graph's constant one, from clip w 10 to 20
                // (_Grid_Camera_Fade_Distance does not reach the shipped program).
                float grid = PLATFORM_SAMPLE(__RGB_Pattern_Texture, uv * _Grid_UV_Scale + distortion).r >= _Grid_Pattern_Width ? 0.0 : 1.0;
                float fade = saturate((TransformWorldToHClip(input.positionWS).w - 20.0) * -0.1) - 1.0;
                float gridAlpha = saturate(_Enable_Grid - (_Enable_Grid * fade + grid)) * _Grid_Color.a;
                color = lerp(color, _Grid_Color.rgb, gridAlpha);

                // Stripes: the pattern's green over UV0 rotated about the centre (by _Stripes_Rotation * 3.145),
                // scaled, scrolled and distorted, between the two stripe alphas.
                float s, c;
                sincos(_Stripes_Rotation * 3.14499998, s, c);
                float2 centred = uv - 0.5;
                float2 rotated = float2(c * centred.x + s * centred.y, c * centred.y - s * centred.x);
                float2 stripeUV = rotated * _Stripe_Scale + 0.5 + (time * _Stripe_Speed + distortion);
                float stripe = smoothstep(_Stripes_Width_1, _Stripes_Width_2, PLATFORM_SAMPLE(__RGB_Pattern_Texture, stripeUV).g);
                float alpha = saturate(lerp(_Stripe_Alpha_A, _Stripe_Alpha_B, stripe) + _Use_Stripes);

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
