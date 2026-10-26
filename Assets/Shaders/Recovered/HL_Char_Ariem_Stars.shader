// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Character/HL_Char_Ariem_Stars, a URP 14 Unlit Shader Graph (platform 14), opaque.
// Three twinkling star layers (one per channel of a screen-space stars texture) and a cloud noise over a background
// colour, under a fresnel colour masked by the cloud noise read mirrored.
Shader "HardLight/Character/HL_Char_Ariem_Stars"
{
    Properties
    {
        _Separator ("#", Float) = 1
        _Title_Textures ("## Textures", Float) = 1
        _Stars_Texture ("-- Stars Texture", 2D) = "black" {}
        _Cloud_Texture ("-- Cloud Texture", 2D) = "black" {}
        _Separator_1 ("# (1)", Float) = 1
        _Title_Stars_Red ("## Stars Red", Float) = 1
        _Stars_Red_UV_Speed_Direction_X ("-- Stars Red UV Speed/Direction X", Range(-1, 1)) = 0
        _Stars_Red_UV_Speed_Direction_Y ("-- Stars Red UV Speed/Direction Y", Range(-1, 1)) = 0
        _Stars_Phase_Mask_Speed ("-- Stars Phase Mask Speed Red", Range(0, 1)) = 0.1
        [HDR]_Stars_Red_Colour ("-- Stars Red Colour", Color) = (0.8627451,0.3647059,0.4470588,1)
        _Separator_2 ("# (2)", Float) = 1
        _Title_Stars_Green ("## Stars Green", Float) = 1
        _Stars_Green_UV_Speed_Direction_X ("-- Stars Green UV Speed/Direction X", Range(-1, 1)) = 0
        _Stars_Green_UV_Speed_Direction_Y ("-- Stars Green UV Speed/Direction Y", Range(-1, 1)) = 0
        _Stars_Phase_Mask_Speed_1 ("-- Stars Phase Mask Speed Green", Range(0, 1)) = 0.1
        [HDR]_Stars_Green_Colour ("-- Stars Green Colour", Color) = (0.1607843,0.6313726,0.8,1)
        _Separator_3 ("# (3)", Float) = 1
        _Title_Stars_Blue ("## Stars Blue", Float) = 1
        _Stars_Blue_UV_Speed_Direction_X ("-- Stars Blue UV Speed/Direction X", Range(-1, 1)) = 0
        _Stars_Blue_UV_Speed_Direction_Y ("-- Stars Blue UV Speed/Direction Y", Range(-1, 1)) = 0
        _Stars_Phase_Mask_Speed_2 ("-- Stars Phase Mask Speed Blue", Range(0, 1)) = 0.1
        [HDR]_Stars_Blue_Colour ("-- Stars Blue Colour", Color) = (0.8313726,0.8313726,0.8313726,0.3254902)
        _Separator_4 ("# (4)", Float) = 1
        _Title_Noise ("## Noise", Float) = 1
        _Noise_UV_Speed_Direction_Y ("-- Noise UV Speed/Direction Y", Range(-1, 1)) = 0
        _Noise_UV_Speed_Direction_X ("-- Noise UV Speed/Direction X", Range(-1, 1)) = 0
        [HDR]_Noise_Colour ("-- Noise Colour", Color) = (0.2941177,0,0.3411765,1)
        _Separator_5 ("# (5)", Float) = 1
        _Title_Global ("## Global", Float) = 1
        _Global_UV_Speed_Mult ("-- Global UV Speed Mult", Range(0, 2)) = 1
        _Stars_Phase_Mask_Speed_3 ("-- Stars Phase Mask Speed Mult", Range(0, 1)) = 1
        _Background_Colour ("-- Background Colour", Color) = (0.09803922,0.03529412,0.1411765,0)
        _Separator_6 ("# (6)", Float) = 1
        _Title_Fresnel ("## Fresnel", Float) = 1
        _Fresnel_Opacity ("-- Fresnel Opacity", Range(0, 1)) = 1
        _Fresnel_Amount ("-- Fresnel Amount", Range(0, 10)) = 5
        [HDR]_Fresnel_Colour ("-- Fresnel Colour", Color) = (0.8639575,0.3769633,2,0)
        _Fresnel_Intensity ("-- Fresnel Intensity", Range(1, 10)) = 1
        _Separator_7 ("# (7)", Float) = 1
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
            float _Title_Textures;
            float _Title_Stars_Red;
            float _Title_Fresnel;
            float4 _Cloud_Texture_TexelSize;
            float4 _Cloud_Texture_ST;
            float _Title_Stars_Green;
            float _Separator_1;
            float _Title_Stars_Blue;
            float _Stars_Phase_Mask_Speed_1;
            float _Stars_Phase_Mask_Speed_2;
            float4 _Stars_Green_Colour;
            float _Separator_3;
            float _Title_Noise;
            float _Separator;
            float _Noise_UV_Speed_Direction_Y;
            float _Separator_4;
            float _Title_Global;
            float _Noise_UV_Speed_Direction_X;
            float _Stars_Phase_Mask_Speed;
            float _Stars_Phase_Mask_Speed_3;
            float4 _Stars_Red_Colour;
            float _Separator_5;
            float _Fresnel_Opacity;
            float _Fresnel_Intensity;
            float4 _Noise_Colour;
            float4 _Stars_Blue_Colour;
            float _Separator_6;
            float4 _Fresnel_Colour;
            float _Fresnel_Amount;
            float _Separator_2;
            float4 _Background_Colour;
            float _Separator_7;
            float _Stars_Blue_UV_Speed_Direction_Y;
            float _Stars_Blue_UV_Speed_Direction_X;
            float4 _Stars_Texture_TexelSize;
            float4 _Stars_Texture_ST;
            float _Stars_Red_UV_Speed_Direction_X;
            float _Stars_Red_UV_Speed_Direction_Y;
            float _Stars_Green_UV_Speed_Direction_X;
            float _Stars_Green_UV_Speed_Direction_Y;
            float _Global_UV_Speed_Mult;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;
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

            TEXTURE2D(_Stars_Texture);
            TEXTURE2D(_Cloud_Texture);
            // The graph's inline sampler: linear, repeat.
            SAMPLER(SamplerState_Linear_Repeat);

            // The fragment's screen UV, as the decoded program derives it from the pixel position.
            float2 ScreenUV(float4 positionCS)
            {
                float y = _ProjectionParams.x < 0.0 ? _ScaledScreenParams.y - positionCS.y : positionCS.y;
                float2 screenUV = float2(positionCS.x, y) / _ScaledScreenParams.xy;
                #if UNITY_UV_STARTS_AT_TOP
                screenUV.y = 1.0 - screenUV.y;
                #endif
                return screenUV;
            }

            // A star layer's twinkle: a triangle wave of game time times its phase speed and the speed multiplier,
            // at least 0.1.
            float AriemStarsPhase(float speed)
            {
                return max(abs(frac(_HLShaderTimeGame * speed * _Stars_Phase_Mask_Speed_3) * 2.0 - 1.0), 0.1);
            }

            // The stars texture at the screen UV scrolled by game time times a layer's direction and the global
            // multiplier, through its tiling and offset.
            float4 AriemStarsSample(float2 screenUV, float2 direction)
            {
                float2 uv = screenUV * _Stars_Texture_ST.xy + (_HLShaderTimeGame * (direction * _Global_UV_Speed_Mult) + _Stars_Texture_ST.zw);
                return SAMPLE_TEXTURE2D(_Stars_Texture, SamplerState_Linear_Repeat, uv);
            }

            // The view direction: towards the camera for perspective, the view matrix's z row for orthographic.
            float3 AriemStarsViewDirection(float3 positionWS)
            {
                return normalize(unity_OrthoParams.w == 0.0 ? normalize(_WorldSpaceCameraPos - positionWS) : UNITY_MATRIX_V[2].xyz);
            }

            // The star colour. Blue stars (the stars texture's blue) times their colour, twinkle and colour alpha;
            // green and then red stars each cover it by their channel times twinkle and colour alpha, with their colour
            // times the twinkle. The cloud texture (scrolled by the noise direction) times the noise colour adds on, and
            // the sum's red picks it over the background colour. The fresnel colour lies under that by one minus the
            // fresnel term (pow(1 - saturate(n.v), amount) * intensity) times its opacity, saturated, and shows by the
            // cloud's red read at one minus its UV (through the tiling again), halved and lifted by 0.5.
            float3 AriemStarsColour(float2 screenUV, float3 positionWS, float3 normalWS)
            {
                float blueTwinkle = AriemStarsPhase(_Stars_Phase_Mask_Speed_2);
                float blue = AriemStarsSample(screenUV, float2(_Stars_Blue_UV_Speed_Direction_X, _Stars_Blue_UV_Speed_Direction_Y)).z;
                float3 stars = blue * _Stars_Blue_Colour.rgb * blueTwinkle * _Stars_Blue_Colour.a;
                float greenTwinkle = AriemStarsPhase(_Stars_Phase_Mask_Speed_1);
                float green = AriemStarsSample(screenUV, float2(_Stars_Green_UV_Speed_Direction_X, _Stars_Green_UV_Speed_Direction_Y)).y;
                stars = lerp(stars, _Stars_Green_Colour.rgb * greenTwinkle, greenTwinkle * green * _Stars_Green_Colour.a);
                float redTwinkle = AriemStarsPhase(_Stars_Phase_Mask_Speed);
                float red = AriemStarsSample(screenUV, float2(_Stars_Red_UV_Speed_Direction_X, _Stars_Red_UV_Speed_Direction_Y)).x;
                stars = lerp(stars, _Stars_Red_Colour.rgb * redTwinkle, redTwinkle * red * _Stars_Red_Colour.a);

                float2 cloudUV = screenUV * _Cloud_Texture_ST.xy + (_HLShaderTimeGame * (float2(_Noise_UV_Speed_Direction_X, _Noise_UV_Speed_Direction_Y) * _Global_UV_Speed_Mult) + _Cloud_Texture_ST.zw);
                float3 cloud = SAMPLE_TEXTURE2D(_Cloud_Texture, SamplerState_Linear_Repeat, cloudUV).rgb;
                float mask = SAMPLE_TEXTURE2D(_Cloud_Texture, SamplerState_Linear_Repeat, (1.0 - cloudUV) * _Cloud_Texture_ST.xy + _Cloud_Texture_ST.zw).x * 0.5 + 0.5;
                stars += cloud * _Noise_Colour.rgb;
                float3 colour = lerp(_Background_Colour.rgb, stars, stars.x);

                float fresnel = pow(1.0 - saturate(dot(normalize(normalWS), AriemStarsViewDirection(positionWS))), _Fresnel_Amount) * _Fresnel_Intensity;
                float3 rim = lerp(_Fresnel_Colour.rgb, colour, saturate(1.0 - fresnel * _Fresnel_Opacity));
                return lerp(colour, rim, mask);
            }

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
                outColor = float4(AriemStarsColour(ScreenUV(input.positionCS), input.positionWS, input.normalWS), 1.0);
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
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZTest LEqual
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
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
