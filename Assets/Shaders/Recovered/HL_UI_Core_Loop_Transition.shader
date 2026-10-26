// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: Hidden/HardLight/UI/HL_UI_Core_Loop_Transition, a URP 14 Unlit Shader Graph (platform 14),
// transparent. In screen space about a centre point: a flowmapped background over rotating polar coordinates,
// a centre colour fade, a rotated foreground overlay, and an alpha from the background plus polar noise.
Shader "Hidden/HardLight/UI/HL_UI_Core_Loop_Transition"
{
    Properties
    {
        _Separator ("#", Float) = 1
        _Title_Surface_Input ("## Surface Input", Float) = 1
        _Alpha ("-- Alpha", Range(0, 1)) = 1
        _Separator_1 ("# (1)", Float) = 1
        _Title_Noise ("## Noise", Float) = 1
        _Noise_Radial_Amount ("-- Noise Radial Amount", Float) = 1
        _Noise_Length_Amount ("-- Noise Length Amount", Float) = 1
        _Separator_2 ("# (2)", Float) = 1
        _Title_Dream_Effect ("## Background", Float) = 1
        _Background_Flowmap_Speed ("-- Background Flowmap Speed", Float) = -1
        _Background_Flowmap_Amount ("-- Background Flowmap Amount", Float) = 0.5
        _Background_Flowmap_Radial_Amount ("-- Background Flowmap Radial Amount", Float) = 1
        _Background_Flowmap_Length_Amount ("-- Background Flowmap Length Amount", Float) = 1
        _Background_Flowmap_UV_Rotation_Speed ("-- Background Flowmap UV Rotation Speed", Float) = 0
        _Separator_3 ("# (3)", Float) = 1
        _Title_Colour_Overlay ("## Foreground", Float) = 1
        _Foreground_Blend_Amount ("-- Foreground Blend Amount", Range(0, 1)) = 0
        _Foreground_Colour_A ("-- Foreground Colour A", Color) = (0,0.5694911,1,1)
        __Foreground_Colour_B ("-- Foreground Colour B", Color) = (1,1,1,1)
        _Foreground_UV_Rotation ("-- Foreground UV Rotation", Float) = 0
        _Foreground_UV_Scale ("-- Foreground UV Scale", Float) = 1
        _Title_Center ("## Center", Float) = 1
        _Center_Point_X ("-- Center Point X", Float) = 0.5
        _Center_Point_Y ("-- Center Point Y", Float) = 0.5
        _Center_Fade_Min ("-- Center Fade Min", Float) = 0
        _Center_Fade_Max ("-- Center Fade Max", Float) = 1
        _Center_Colour ("-- Center Colour", Color) = (0.6156863,0.172549,0.8431373,0)
        _Separator_4 ("# (4)", Float) = 1
        _Separator_5 ("# (5)", Float) = 1
        [NoScaleOffset][MainTexture]_MainTex ("DO NOT TOUCH", 2D) = "white" {}
        _Separator_6 ("# (6)", Float) = 1
        [NoScaleOffset][NonModifiableTextureData]_HLSubSgFlowMapInput_acd9b3a5411347599d1a42d501175ad2_FlowMapTexture_4128534274_Texture2D ("Texture2D", 2D) = "white" {}
        [NoScaleOffset][NonModifiableTextureData]_HLSubSgTex2DGrad_5da7d4cba8b4452091965912b7f28090_Texture2D_4287008460_Texture2D ("Texture2D", 2D) = "white" {}
        [NoScaleOffset][NonModifiableTextureData]_HLSubSgTex2DGrad_6a07e9d27201494eaa0ba86ed1f50c3a_Texture2D_4287008460_Texture2D ("Texture2D", 2D) = "white" {}
        [NoScaleOffset][NonModifiableTextureData]_SampleTexture2D_ffc956bac9fb4289b903d92cc5924e93_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
        [NoScaleOffset][NonModifiableTextureData]_Texture2DAsset_bade7436e4bd476da86479d094bcf974_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
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
            float4 _HLSubSgFlowMapInput_acd9b3a5411347599d1a42d501175ad2_FlowMapTexture_4128534274_Texture2D_TexelSize;
            float4 _HLSubSgTex2DGrad_5da7d4cba8b4452091965912b7f28090_Texture2D_4287008460_Texture2D_TexelSize;
            float4 _HLSubSgTex2DGrad_6a07e9d27201494eaa0ba86ed1f50c3a_Texture2D_4287008460_Texture2D_TexelSize;
            float4 _SampleTexture2D_ffc956bac9fb4289b903d92cc5924e93_Texture_1_Texture2D_TexelSize;
            float4 _Texture2DAsset_bade7436e4bd476da86479d094bcf974_Out_0_Texture2D_TexelSize;
            float _Title_Surface_Input;
            float _Title_Dream_Effect;
            float _Separator_1;
            float _Title_Noise;
            float _Separator_2;
            float _Title_Colour_Overlay;
            float _Separator_3;
            float _Separator_4;
            float _Separator;
            float _Noise_Radial_Amount;
            float _Noise_Length_Amount;
            float _Background_Flowmap_Speed;
            float _Background_Flowmap_Amount;
            float _Background_Flowmap_Length_Amount;
            float _Background_Flowmap_UV_Rotation_Speed;
            float _Title_Center;
            float _Background_Flowmap_Radial_Amount;
            float _Foreground_Blend_Amount;
            float4 __Foreground_Colour_B;
            float4 _Foreground_Colour_A;
            float _Foreground_UV_Scale;
            float _Foreground_UV_Rotation;
            float _Alpha;
            float _Separator_5;
            float _Separator_6;
            float4 _MainTex_TexelSize;
            float _Center_Point_Y;
            float _Center_Point_X;
            float _Center_Fade_Max;
            float _Center_Fade_Min;
            float4 _Center_Colour;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest Always
            ZWrite On
            Cull Off

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

            // UI-scaled shader time published by the time manager.
            float _HLShaderTimeUI;

            TEXTURE2D(_HLSubSgFlowMapInput_acd9b3a5411347599d1a42d501175ad2_FlowMapTexture_4128534274_Texture2D);
            TEXTURE2D(_HLSubSgTex2DGrad_5da7d4cba8b4452091965912b7f28090_Texture2D_4287008460_Texture2D);
            TEXTURE2D(_HLSubSgTex2DGrad_6a07e9d27201494eaa0ba86ed1f50c3a_Texture2D_4287008460_Texture2D);
            TEXTURE2D(_SampleTexture2D_ffc956bac9fb4289b903d92cc5924e93_Texture_1_Texture2D);
            SAMPLER(sampler_SampleTexture2D_ffc956bac9fb4289b903d92cc5924e93_Texture_1_Texture2D);
            TEXTURE2D(_Texture2DAsset_bade7436e4bd476da86479d094bcf974_Out_0_Texture2D);
            // The graph's inline sampler: linear, repeat (no mip blending).
            SAMPLER(SamplerState_Linear_Repeat);
            #define FLOW_MAP _HLSubSgFlowMapInput_acd9b3a5411347599d1a42d501175ad2_FlowMapTexture_4128534274_Texture2D
            #define BACKGROUND_B _HLSubSgTex2DGrad_5da7d4cba8b4452091965912b7f28090_Texture2D_4287008460_Texture2D
            #define BACKGROUND_A _HLSubSgTex2DGrad_6a07e9d27201494eaa0ba86ed1f50c3a_Texture2D_4287008460_Texture2D
            #define FOREGROUND _SampleTexture2D_ffc956bac9fb4289b903d92cc5924e93_Texture_1_Texture2D
            #define NOISE _Texture2DAsset_bade7436e4bd476da86479d094bcf974_Out_0_Texture2D

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

            // Shader Graph's screen position (Default): the pixel position over the scaled screen size, y up.
            float2 ScreenUV(float4 positionCS)
            {
                float2 pixel = positionCS.xy;
                #if UNITY_UV_STARTS_AT_TOP
                pixel.y = _ProjectionParams.x < 0.0 ? _ScaledScreenParams.y - pixel.y : pixel.y;
                #else
                pixel.y = _ProjectionParams.x > 0.0 ? _ScaledScreenParams.y - pixel.y : pixel.y;
                #endif
                float2 ndc = pixel / _ScaledScreenParams.xy;
                ndc.y = 1.0 - ndc.y;
                return ndc;
            }

            // Shader Graph's Rotate (Radians) matrix applied about the origin.
            float2 Rotate(float2 uv, float radians)
            {
                float s = sin(radians);
                float c = cos(radians);
                float2x2 rotation = float2x2(c, -s, s, c);
                rotation = rotation * 0.5 + 0.5;
                rotation = rotation * 2.0 - 1.0;
                return mul(uv, rotation);
            }

            // Shader Graph's Polar Coordinates (centred already): (length * 2 * radial scale, atan2(x, y) / 6.28 *
            // length scale).
            float2 Polar(float2 delta, float radialScale, float lengthScale)
            {
                return float2(length(delta) * 2.0 * radialScale, atan2(delta.x, delta.y) * (1.0 / 6.28) * lengthScale);
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 screen = ScreenUV(input.positionCS);
                float2 centre = float2(_Center_Point_X, _Center_Point_Y);
                float2 delta = screen - centre;

                // Background: two flowmapped phases of polar coordinates (rotated by UI time) sampled with the
                // screen UV's own derivatives, blended by the phase.
                float2 polar = Polar(Rotate(delta, _HLShaderTimeUI * _Background_Flowmap_UV_Rotation_Speed),
                    _Background_Flowmap_Radial_Amount, _Background_Flowmap_Length_Amount);
                float2 flow = (SAMPLE_TEXTURE2D(FLOW_MAP, SamplerState_Linear_Repeat, polar).xy - 0.5) * _Background_Flowmap_Amount * -2.0;
                float phaseA = frac(_TimeParameters.x * _Background_Flowmap_Speed + 0.5);
                float phaseB = frac(_TimeParameters.x * _Background_Flowmap_Speed);
                float2 dx = ddx(screen), dy = ddy(screen);
                float4 backgroundA = PLATFORM_SAMPLE_TEXTURE2D_GRAD(BACKGROUND_A, SamplerState_Linear_Repeat, flow * phaseA + polar, dx, dy);
                float4 backgroundB = PLATFORM_SAMPLE_TEXTURE2D_GRAD(BACKGROUND_B, SamplerState_Linear_Repeat, flow * phaseB + polar, dx, dy);
                float4 background = lerp(backgroundB, backgroundA, abs(phaseB * 2.0 - 1.0));

                // Noise: the noise texture along unrotated polar angles (one at a constant scale and one scaled by the noise
                // length), plus a radial falloff, with UV0's derivatives.
                float radius = length(delta);
                float angle = atan2(delta.x, delta.y);
                float2 uvDx = ddx(input.uv0.xy), uvDy = ddy(input.uv0.xy);
                float2 fixedNoise = SAMPLE_TEXTURE2D_GRAD(NOISE, SamplerState_Linear_Repeat, float2(0.0, angle * 0.0209235661), uvDx, uvDy).yz;
                float lengthNoise = SAMPLE_TEXTURE2D_GRAD(NOISE, SamplerState_Linear_Repeat, float2(0.0, angle * _Noise_Length_Amount * (1.0 / 6.28)), uvDx, uvDy).y;
                float noise = fixedNoise.x + fixedNoise.y + lengthNoise + (1.0 - radius * 2.0 * _Noise_Radial_Amount) * 1.52;
                float alpha = saturate(background.a + noise) * _Alpha;

                // Centre colour, smoothstepped in towards the centre.
                float3 color = lerp(background.rgb, _Center_Colour.rgb, smoothstep(_Center_Fade_Min, _Center_Fade_Max, 1.0 - radius * 2.0));

                // Foreground: the screen UV rotated about 0.5 and scaled, offset against the centre; its value picks
                // between the foreground colours, which overlay the colour (at their alpha).
                float2 foregroundUV = Rotate(screen - 0.5, _Foreground_UV_Rotation) * _Foreground_UV_Scale + (1.0 - (centre - 0.5)) + 0.5;
                float4 foreground = lerp(_Foreground_Colour_A, __Foreground_Colour_B, SAMPLE_TEXTURE2D(FOREGROUND, sampler_SampleTexture2D_ffc956bac9fb4289b903d92cc5924e93_Texture_1_Texture2D, foregroundUV));
                float3 dark = step(foreground.rgb, 0.5);
                float3 overlay = 2.0 * color * foreground.rgb * dark + (1.0 - 2.0 * (1.0 - color) * (1.0 - foreground.rgb)) * (1.0 - dark);
                color = lerp(color, overlay, foreground.a);

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
            Cull Off

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
