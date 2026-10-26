// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Character/HL_Char_Boss_02_Goo, a URP 14 Lit Shader Graph (platform 14), transparent
// (alpha 1) with depth writes. Two scrolling packed-noise colour layers under matcap fresnel and reflection, faded
// towards a colour against the scene depth; the colour drives both the base colour and the emission.
Shader "HardLight/Character/HL_Char_Boss_02_Goo"
{
    Properties
    {
        _Documentation_Link_Below ("## Documentation Link Below : HL_Char_Boss_02_Goo", Float) = 0
        _Documentation_Link ("[Insert Link](Insert Link)", Float) = 0
        _Separator_0 ("#", Float) = 0
        _Title_Surface_Input ("## Surface Input", Float) = 0
        _Smoothness_Intensity ("-- Smoothness Intensity", Range(0, 1)) = 0.7
        _NOTE_R_Noise_1_G_Noise_2 ("!NOTE R = Noise 1, G = Noise 2", Float) = 0.7
        [NoScaleOffset]_Packed_Noise_Tex ("-- Packed Noise Texture", 2D) = "white" {}
        _Separator_1 ("# (1)", Float) = 0
        _Title_Red_Channel_Noise ("## Red Channel Noise", Float) = 0
        _Tiling_R_Noise_UV ("-- R Noise: UV Tiling &", Vector) = (1,1,0,0)
        _NOTE_Uses_XY_only ("!NOTE Uses XY only", Float) = 0.7
        _R_Noise_UV_Speed_Direction_X ("-- R Noise: UV Speed+Direction X", Range(-1, 1)) = 0
        _R_Noise_UV_Speed_Direction_Y ("-- R Noise: UV Speed+Direction Y", Range(-1, 1)) = 0
        _R_Noise_Contrast ("-- R Noise: Contrast", Float) = 2.11
        _R_Noise_Color_A ("-- R Noise: Color A", Color) = (1,0.7019608,0,1)
        _R_Noise_Color_B ("-- R Noise: Color B", Color) = (1,0.5019608,0,1)
        _Separator_2 ("# (2)", Float) = 0
        _Title_Green_Channel_Noise ("## Green Channel Noise", Float) = 0
        _Tiling_G_Noise_UV ("-- G Noise: UV Tiling &", Vector) = (1,1,0,0)
        _NOTE_Use_XY_only ("!NOTE Use XY only", Float) = 0.7
        _G_Noise_UV_Speed_Direction_X ("-- G Noise: UV Speed+Direction X", Range(-1, 1)) = 0
        _G_Noise_UV_Speed_Direction_Y ("-- G Noise: UV Speed+Direction Y", Range(-1, 1)) = 0
        _G_Noise_Opacity ("-- G Noise: Opacity", Range(0, 1)) = 0.4
        _G_Noise_Colour ("-- G Noise: Colour", Color) = (1,0.2784314,0,1)
        _Separator_3 ("# (3)", Float) = 0
        _Title_Depth ("## Depth", Float) = 0
        _Depth_Fade_Color ("-- Depth Fade Color", Color) = (1,0,0,1)
        _Depth_Fade_Distance ("-- Depth Fade Distance", Range(0, 10)) = 2
        _Separator_4 ("# (4)", Float) = 0
        _Title_Matcaps ("## Matcaps", Float) = 0
        [NoScaleOffset]_Matcap_Reflection_Tex ("-- Matcap Reflection Texture", 2D) = "white" {}
        [NoScaleOffset]_Matcap_Fresnel_Tex ("-- Matcap Fresnel Texture", 2D) = "white" {}
        _NOTE_Use_Matcap_Fresnel_Alpha_to_mask_the_effect ("!NOTE Use Matcap Fresnel Alpha to mask the effect", Float) = 0.7
        _Separator_5 ("# (5)", Float) = 0
        [HideInInspector]_QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl ("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "UniversalMaterialType"="Lit" "Queue"="Transparent" "DisableBatching"="False" "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalLitSubTarget" }

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float4 _Packed_Noise_Tex_TexelSize;
            float _NOTE_R_Noise_1_G_Noise_2;
            float _Separator_0;
            float _Separator_1;
            float _Title_Red_Channel_Noise;
            float4 _R_Noise_Color_A;
            float4 _R_Noise_Color_B;
            float _NOTE_Uses_XY_only;
            float4 _Tiling_G_Noise_UV;
            float _R_Noise_UV_Speed_Direction_X;
            float _R_Noise_UV_Speed_Direction_Y;
            float _Separator_2;
            float _Title_Green_Channel_Noise;
            float _NOTE_Use_XY_only;
            float _Smoothness_Intensity;
            float _G_Noise_Opacity;
            float4 _G_Noise_Colour;
            float _Separator_3;
            float _R_Noise_Contrast;
            float _G_Noise_UV_Speed_Direction_X;
            float _G_Noise_UV_Speed_Direction_Y;
            float _Documentation_Link_Below;
            float _Documentation_Link;
            float _Title_Surface_Input;
            float _Title_Depth;
            float4 _Tiling_R_Noise_UV;
            float4 _Depth_Fade_Color;
            float _Depth_Fade_Distance;
            float _Separator_4;
            float _Separator_5;
            float _Title_Matcaps;
            float _NOTE_Use_Matcap_Fresnel_Alpha_to_mask_the_effect;
            float4 _Matcap_Fresnel_Tex_TexelSize;
            float4 _Matcap_Reflection_Tex_TexelSize;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;

        TEXTURE2D(_Packed_Noise_Tex);
        TEXTURE2D(_Matcap_Reflection_Tex);
        SAMPLER(sampler_Matcap_Reflection_Tex);
        TEXTURE2D(_Matcap_Fresnel_Tex);
        SAMPLER(sampler_Matcap_Fresnel_Tex);
        // The graph's inline sampler: linear, repeat.
        SAMPLER(SamplerState_Linear_Repeat);

        // Saturated (scene eye depth - fragment clip w) / distance, from the camera depth texture: _ZBufferParams
        // for perspective, inverse view-projection for orthographic.
        float Goo2DepthFade(float3 positionWS, float4 positionCS)
        {
            float y = _ProjectionParams.x < 0.0 ? _ScaledScreenParams.y - positionCS.y : positionCS.y;
            float2 screenUV = float2(positionCS.x, y) / _ScaledScreenParams.xy;
            #if UNITY_UV_STARTS_AT_TOP
            screenUV.y = 1.0 - screenUV.y;
            #endif
            float rawDepth = SampleSceneDepth(screenUV);
            float sceneDepth;
            if (unity_OrthoParams.w == 1.0)
            {
                // Metal stores reversed 0..1 depth; GL stores 0..1 but its inverse VP expects -1..1.
                #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float3 scenePositionWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                sceneDepth = abs(mul(UNITY_MATRIX_V, float4(scenePositionWS, 1.0)).z);
            }
            else
            {
                sceneDepth = 1.0 / (_ZBufferParams.z * rawDepth + _ZBufferParams.w);
            }
            float clipW = TransformWorldToHClip(positionWS).w;
            return saturate((sceneDepth - clipW) / _Depth_Fade_Distance);
        }

        // The matcap UV: the view-space view direction crossed with the view-space normal, r = cross(v, n), as
        // (r.y, -r.x) * 0.45 + 0.5. The original takes the normal through the inverse view's transpose, which is
        // the view rotation itself for a camera's rigid view matrix.
        float2 Goo2MatcapUV(float3 positionWS, float3 normalWS)
        {
            float3 viewWS = unity_OrthoParams.w == 0.0 ? normalize(_WorldSpaceCameraPos - positionWS) : UNITY_MATRIX_V[2].xyz;
            float3 viewVS = normalize(mul((float3x3)UNITY_MATRIX_V, viewWS));
            float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
            float3 r = cross(viewVS, normalVS);
            return float2(r.y, r.x) * float2(0.45, -0.45) + 0.5;
        }

        struct Goo2Surface
        {
            float3 color;
            float smoothness;
        };

        // The colour: the red noise (contrasted about 0.1) between colours A and B, the green noise (offset (0.3,
        // 0.2), times its opacity) towards the green colour, the matcap fresnel over it by its alpha, plus the matcap
        // reflection; then the depth fade: lerp(lerp(fade colour, colour, fade), colour / 2, 1 - fade). The graph
        // feeds it to both the base colour and the emission.
        Goo2Surface Goo2SurfaceInputs(float2 uv, float3 positionWS, float4 positionCS, float3 normalWS)
        {
            Goo2Surface surface;
            float time = _HLShaderTimeGame;
            float2 uvR = (uv + time * float2(_R_Noise_UV_Speed_Direction_X, _R_Noise_UV_Speed_Direction_Y)) * _Tiling_R_Noise_UV.xy + _Tiling_R_Noise_UV.zw;
            float2 uvG = (uv + float2(0.3, 0.2) + time * float2(_G_Noise_UV_Speed_Direction_X, _G_Noise_UV_Speed_Direction_Y)) * _Tiling_G_Noise_UV.xy + _Tiling_G_Noise_UV.zw;
            float red = saturate((SAMPLE_TEXTURE2D(_Packed_Noise_Tex, SamplerState_Linear_Repeat, uvR).x - 0.1) * _R_Noise_Contrast + 0.1);
            float3 color = lerp(_R_Noise_Color_A.rgb, _R_Noise_Color_B.rgb, red);
            float green = SAMPLE_TEXTURE2D(_Packed_Noise_Tex, SamplerState_Linear_Repeat, uvG).y * _G_Noise_Opacity;
            color = lerp(color, _G_Noise_Colour.rgb, green);
            float2 matcapUV = Goo2MatcapUV(positionWS, normalWS);
            float4 fresnel = SAMPLE_TEXTURE2D(_Matcap_Fresnel_Tex, sampler_Matcap_Fresnel_Tex, matcapUV);
            color = lerp(color, fresnel.rgb, fresnel.a) + SAMPLE_TEXTURE2D(_Matcap_Reflection_Tex, sampler_Matcap_Reflection_Tex, matcapUV).rgb;
            float fade = Goo2DepthFade(positionWS, positionCS);
            surface.color = lerp(lerp(_Depth_Fade_Color.rgb, color, fade), color * 0.5, 1.0 - fade);
            surface.smoothness = saturate(_Smoothness_Intensity);
            return surface;
        }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest LEqual
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // The ShaderGraph Lit target computes fog per fragment from view depth.
            #define _FOG_FRAGMENT 1
            #define _SURFACE_TYPE_TRANSPARENT 1
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma multi_compile_fog
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 uv0 : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float4 uv : TEXCOORD3;
                float4 fogFactorAndVertexLight : TEXCOORD4;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD5;
                #endif
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 6);
                #if defined(DYNAMICLIGHTMAP_ON)
                float2 dynamicLightmapUV : TEXCOORD7;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv = input.uv0;
                float fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #endif
                output.fogFactorAndVertexLight = float4(fogFactor, VertexLighting(vertexInput.positionWS, normalInput.normalWS));
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                #if defined(DYNAMICLIGHTMAP_ON)
                output.dynamicLightmapUV = input.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                OUTPUT_SH(output.normalWS, output.vertexSH);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vertexInput);
                #endif
                return output;
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv.xy;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // The raw interpolated basis, as the original transforms the lighting normal before normalising.
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                inputData.tangentToWorld = float3x3(input.tangentWS.xyz, tangentSign * cross(input.normalWS, input.tangentWS.xyz), input.normalWS);
                float3 normalTS = float3(0.0, 0.0, 1.0);
                inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, inputData.tangentToWorld));
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
                #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                #if defined(DYNAMICLIGHTMAP_ON)
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
                #else
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                #endif
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);

                Goo2Surface surface = Goo2SurfaceInputs(uv, input.positionWS, input.positionCS, normalize(input.normalWS));
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = surface.color;
                surfaceData.metallic = 0;
                surfaceData.smoothness = surface.smoothness;
                surfaceData.emission = surface.color;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1;
                surfaceData.alpha = 1;
                #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                outColor = color;
                #if defined(_WRITE_RENDERING_LAYERS)
                outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
                #endif
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 uv0 : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv0;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, output.normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
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

            struct Attributes { float4 positionOS : POSITION; float4 uv0 : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float4 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv0;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.zzzz;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZTest LEqual
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float4 uv0 : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 tangentWS : TEXCOORD0;
                float4 uv : TEXCOORD1;
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
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv = input.uv0;
                output.normalWS = normalInput.normalWS;
                return output;
            }

            void Frag(Varyings input, out float4 outNormalWS : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, tangentSign * cross(input.normalWS, input.tangentWS.xyz), input.normalWS);
                outNormalWS = float4(NormalizeNormalPerPixel(TransformTangentToWorld(float3(0.0, 0.0, 1.0), tangentToWorld)), 0.0);
                #if defined(_WRITE_RENDERING_LAYERS)
                outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 tangentWS : TEXCOORD0;
                float4 uv0 : TEXCOORD1;
                float4 uv1 : TEXCOORD2;
                float4 uv2 : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float3 normalWS : TEXCOORD5;
                #if defined(EDITOR_VISUALIZATION)
                float2 vizUV : TEXCOORD6;
                float4 lightCoord : TEXCOORD7;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1.xy, input.uv2.xy);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = normalInput.normalWS;
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(input.positionOS.xyz, input.uv0.xy, input.uv1.xy, input.uv2.xy, output.vizUV, output.lightCoord);
                #endif
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, tangentSign * cross(input.normalWS, input.tangentWS.xyz), input.normalWS);
                float3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(float3(0.0, 0.0, 1.0), tangentToWorld));
                Goo2Surface surface = Goo2SurfaceInputs(input.uv0.xy, input.positionWS, input.positionCS, normalize(input.normalWS));
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = surface.color;
                metaInput.Emission = surface.color;
                #if defined(EDITOR_VISUALIZATION)
                metaInput.VizUV = input.vizUV;
                metaInput.LightCoord = input.lightCoord;
                #endif
                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
