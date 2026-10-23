// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Character/HL_Char_Standard, a URP 14 Lit Shader Graph (platform 14).
// The graph's surface inputs are rebuilt from the Metal programs; lighting, GI, shadows, fog
// and depth/meta output use URP 14.0.11's own library functions, as the graph's generated passes do.
Shader "HardLight/Character/HL_Char_Standard"
{
    Properties
    {
        _Documentation_Link_Below ("## Documentation Link Below : HL_Char_Standard", Float) = 0
        _Documentation_Link ("[https://hardlight.atlassian.net/wiki/spaces/HL/pages/8326272/SDT+-+HL+Char+Standard](https://hardlight.atlassian.net/wiki/spaces/HL/pages/8326272/SDT+-+HL+Char+Standard)", Float) = 0
        _Separator_0 ("# (0)", Float) = 0
        _Title_Main_Textures ("## Main Textures", Float) = 0
        [NoScaleOffset]_Diff_Texture ("-- Diff Texture", 2D) = "white" {}
        _Diff_Colour ("-- Diff Colour", Color) = (1,1,1,1)
        [NoScaleOffset][Normal]_Norm_Texture ("-- Norm Texture", 2D) = "bump" {}
        [ToggleUI]_Update_backface_Normals ("-- Update backface Normals?", Float) = 0
        [NoScaleOffset]_MS_Texture ("-- MS Texture", 2D) = "black" {}
        _Title_ID_Mask ("## ID Mask", Float) = 0
        _NOTE_R_Skin_G_Boots_B_Opacity_A_Ball_Mask ("!NOTE R=Skin-G=Boots-B=Opacity-A=Ball Mask", Float) = 0
        [NoScaleOffset]_ID_Mask ("-- ID Mask", 2D) = "black" {}
        _NOTE_If_using_B_channel_change_Surface_Options_to_Transparent_Alpha_Clipping ("!NOTE ** If using B channel, change Surface Options to Transparent/Alpha Clipping. **", Float) = 0
        _Separator_1 ("# (1)", Float) = 0
        _Title_Emission ("## Emission", Float) = 0
        [NoScaleOffset]_Emissive_Texture ("-- Emissive Texture", 2D) = "white" {}
        [HDR]_Emissive_Colour ("-- Emissive Colour", Color) = (0,0,0,0)
        _Global_Emission_Intensity ("-- Global Emission Intensity", Range(0, 1)) = 1
        _Matcap_Emissive_Intensity ("-- Matcap Emissive Intensity [_USE_MATCAP]", Range(0, 1)) = 1
        _SSS_Emissive_Intensity ("-- SSS Emissive Intensity [_USE_SUBSURFACE_SCATTERING]", Range(0, 1)) = 1
        _Separator_2 ("# (2)", Range(0, 1)) = 0
        _Title_Fresnel ("## Fresnel Texture", Float) = 0
        _Fresnel_Amount ("-- Fresnel Amount", Range(0, 10)) = 1
        _Fresnel_Intensity ("-- Fresnel Intensity", Range(0, 10)) = 1
        [HDR]_Fresnel_Boost_Colour ("-- Fresnel Boost Colour", Color) = (2,2,2,1)
        [NoScaleOffset]_Fresnel_Texture ("-- Fresnel Texture", 2D) = "white" {}
        _Separator_3 ("# (3)", Float) = 0
        _Title_Matcap ("## Matcap", Float) = 0
        _REF_USE_CUBEMAP ("!REF _USE_MATCAP", Float) = 0
        [Toggle(_USE_MATCAP)]_USE_MATCAP ("Use Matcap?", Float) = 0
        [NoScaleOffset]__Matcap_Texture__USE_MATCAP ("-- Matcap Texture [_USE_MATCAP]", 2D) = "white" {}
        _Matcap_Blend_Amount ("-- Matcap Blend Amount", Range(0, 1)) = 0
        _Separator_4 ("# (4)", Float) = 0
        _Title_Sub_Surface_Scattering ("## Sub Surface Scattering", Float) = 0
        _REF_USE_SUBSURFACE_SCATTERING ("!REF _USE_SUBSURFACE_SCATTERING", Float) = 0
        [Toggle(_USE_SUBSURFACE_SCATTERING)]_USE_SUBSURFACE_SCATTERING ("Use SubSurface Scattering?", Float) = 0
        [NoScaleOffset]_SSS_Gradient_Ramp_Texture ("-- SSS Gradient Ramp Texture [_USE_SUBSURFACE_SCATTERING]", 2D) = "white" {}
        _SSS_Offset ("-- SSS Offset [_USE_SUBSURFACE_SCATTERING]", Float) = 0
        _SSS_Opacity ("-- SSS Opacity [_USE_SUBSURFACE_SCATTERING]", Range(0, 1)) = 1
        _Separator_5 ("# (5)", Float) = 0
        _Title_Ball_Mode ("## Ball Mode", Float) = 0
        _REF_USE_BALLMODE ("!REF _USE_BALLMODE", Float) = 0
        [Toggle(_USE_BALLMODE)]_USE_BALLMODE ("Use Ballmode?", Float) = 0
        _Ball_Noise_Opacity ("-- Ball Noise Opacity [_USE_BALLMODE]", Range(0, 1)) = 0
        _Ball_Noise_Intensity ("-- Ball Noise Intensity [_USE_BALLMODE]", Float) = 1
        [NoScaleOffset]_Ball_Noise_Texture ("-- Ball Noise Texture [_USE_BALLMODE]", 2D) = "black" {}
        _Ball_Noise_Colour ("-- Ball Noise Colour [_USE_BALLMODE]", Color) = (0,0,0,0)
        _Ball_UV_Tiling ("-- Ball UV Tiling [_USE_BALLMODE]", Vector) = (1,1,0,0)
        _Ball_UV_Offset ("-- Ball UV Offset [_USE_BALLMODE]", Vector) = (0,0,0,0)
        _Separator_6 ("# (6)", Range(0, 1)) = 0
        _Title_Erosion ("## Erosion", Float) = 0
        _Erosion_Progress ("-- Erosion Progress", Range(0, 1)) = 0
        [HDR]_ErosionColor ("-- ErosionColor", Color) = (1,1,1,0)
        _ErosionTiling ("-- ErosionTiling", Float) = 1
        _Separator_7 ("# (7)", Range(0, 1)) = 0
        _Title_Rim_Light ("## Rim Light", Float) = 0
        _NOTE_Effects_to_skin_only ("!NOTE **Effects to skin only**", Float) = 0
        [HDR]_Rim_Colour ("-- Rim Colour", Color) = (1,1,1,0)
        _Rim_Opacity ("-- Rim Opacity", Range(0, 1)) = 0
        _Rim_Light_Position ("-- Rim Light Position", Vector) = (0,0,0,0)
        _Rim_Amount ("-- Rim Amount", Range(0, 1)) = 0.75
        _Rim_Softness ("-- Rim Softness", Range(0, 2)) = 1
        _Rim_Camera_Fade_Position ("-- Rim Camera Fade Position", Float) = 0.1
        _Separator_8 ("# (8)", Range(0, 1)) = 0
        [NoScaleOffset][NonModifiableTextureData]_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
        [HideInInspector]_WorkflowMode ("_WorkflowMode", Float) = 1
        [HideInInspector]_CastShadows ("_CastShadows", Float) = 0
        [HideInInspector]_ReceiveShadows ("_ReceiveShadows", Float) = 1
        [HideInInspector]_Surface ("_Surface", Float) = 0
        [HideInInspector]_Blend ("_Blend", Float) = 0
        [HideInInspector]_AlphaClip ("_AlphaClip", Float) = 0
        [HideInInspector]_BlendModePreserveSpecular ("_BlendModePreserveSpecular", Float) = 0
        [HideInInspector]_SrcBlend ("_SrcBlend", Float) = 1
        [HideInInspector]_DstBlend ("_DstBlend", Float) = 0
        [HideInInspector][ToggleUI]_ZWrite ("_ZWrite", Float) = 1
        [HideInInspector]_ZWriteControl ("_ZWriteControl", Float) = 1
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
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" "Queue"="Geometry" "DisableBatching"="False" "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalLitSubTarget" }

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // The shipped UnityPerMaterial, every member in its decoded order (SRP Batcher layout parity).
        CBUFFER_START(UnityPerMaterial)
            float4 _SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D_TexelSize;
            float _Documentation_Link;
            float _Documentation_Link_Below;
            float _Title_Fresnel;
            float _Title_Emission;
            float _Title_ID_Mask;
            float _Title_Main_Textures;
            float _REF_USE_SUBSURFACE_SCATTERING;
            float _Title_Matcap;
            float _NOTE_If_using_B_channel_change_Surface_Options_to_Transparent_Alpha_Clipping;
            float _REF_USE_CUBEMAP;
            float _Fresnel_Intensity;
            float _Separator_1;
            float _Title_Sub_Surface_Scattering;
            float4 _Norm_Texture_TexelSize;
            float4 _MS_Texture_TexelSize;
            float4 _Diff_Texture_TexelSize;
            float _Separator_2;
            float4 _ID_Mask_TexelSize;
            float4 _Emissive_Texture_TexelSize;
            float _Title_Ball_Mode;
            float4 _Emissive_Colour;
            float _Global_Emission_Intensity;
            float _Matcap_Emissive_Intensity;
            float _Separator_3;
            float _SSS_Emissive_Intensity;
            float _Separator_0;
            float _Fresnel_Amount;
            float _REF_USE_BALLMODE;
            float _Separator_4;
            float4 _SSS_Gradient_Ramp_Texture_TexelSize;
            float _SSS_Offset;
            float _SSS_Opacity;
            float4 _Ball_Noise_Texture_TexelSize;
            float _Separator_5;
            float _Ball_Noise_Opacity;
            float4 _Ball_Noise_Colour;
            float4 _Fresnel_Texture_TexelSize;
            float _NOTE_R_Skin_G_Boots_B_Opacity_A_Ball_Mask;
            float _Title_Erosion;
            float _Separator_6;
            float2 _Ball_UV_Offset;
            float2 _Ball_UV_Tiling;
            float _Ball_Noise_Intensity;
            float4 __Matcap_Texture__USE_MATCAP_TexelSize;
            float _Erosion_Progress;
            float4 _ErosionColor;
            float _ErosionTiling;
            float4 _Rim_Colour;
            float3 _Rim_Light_Position;
            float _Rim_Amount;
            float4 _Fresnel_Boost_Colour;
            float _Title_Rim_Light;
            float _Separator_7;
            float _NOTE_Effects_to_skin_only;
            float _Rim_Opacity;
            float _Separator_8;
            float _Rim_Camera_Fade_Position;
            float _Rim_Softness;
            float4 _Diff_Colour;
            float _Matcap_Blend_Amount;
            float _Update_backface_Normals;
        CBUFFER_END

        TEXTURE2D(_Diff_Texture);
        TEXTURE2D(_Norm_Texture);
        TEXTURE2D(_MS_Texture);
        TEXTURE2D(_ID_Mask);
        TEXTURE2D(_Emissive_Texture);
        TEXTURE2D(_SSS_Gradient_Ramp_Texture);
        TEXTURE2D(_Ball_Noise_Texture);
        TEXTURE2D(_Fresnel_Texture);
        SAMPLER(sampler_Fresnel_Texture);
        TEXTURE2D(__Matcap_Texture__USE_MATCAP);
        SAMPLER(sampler__Matcap_Texture__USE_MATCAP);
        TEXTURE2D(_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D);
        SAMPLER(sampler_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D);
        // The graph's inline sampler states.
        SAMPLER(SamplerState_Linear_Repeat);
        SAMPLER(SamplerState_Linear_Clamp);
        // Core.hlsl's SAMPLE_TEXTURE2D adds _GlobalMipBias.x once, as the original programs do.
        #define HL_SAMPLE(tex, samp, uv) SAMPLE_TEXTURE2D(tex, samp, uv)

        struct HLCharSurface
        {
            half3 albedo;
            half3 emission;
            half metallic;
            half smoothness;
            half3 normalTS;
            half alpha;
        };

        // The graph's surface, decoded from the forward programs. normalWS is the interpolated
        // vertex normal (normalized); the fresnel uses the unflipped tangent-space normal while the
        // lighting normal may be flipped on back faces.
        HLCharSurface EvaluateHLChar(float2 uv, float3 positionWS, float3 rawNormalWS, float3 rawTangentWS, float bitangentSign,
                                     bool isFrontFace, float3 mainLightDirection)
        {
            HLCharSurface s;
            float3 normalWS = normalize(rawNormalWS);
            float3 normalVS = mul(normalWS, (float3x3)UNITY_MATRIX_I_V);
            float3 viewDirWS = unity_OrthoParams.w == 0.0 ? normalize(_WorldSpaceCameraPos - positionWS)
                                                          : UNITY_MATRIX_V[2].xyz;   // the view matrix's third row
            float3 viewDirVS = mul((float3x3)UNITY_MATRIX_V, viewDirWS);
            float clipW = TransformWorldToHClip(positionWS).w;

            #if defined(_USE_MATCAP)
            // Matcap: a view-space cross of the view direction and the vertex normal.
            float3 viewVS = normalize(viewDirVS);
            float2 matcapUV = float2(viewVS.z * normalVS.x - viewVS.x * normalVS.z, viewVS.y * normalVS.z - viewVS.z * normalVS.y) * float2(0.5, -0.5) + 0.5;
            float3 matcap = HL_SAMPLE(__Matcap_Texture__USE_MATCAP, sampler__Matcap_Texture__USE_MATCAP, matcapUV).rgb;
            #endif

            float3 diffuse = HL_SAMPLE(_Diff_Texture, SamplerState_Linear_Repeat, uv).rgb;
            float4 id = HL_SAMPLE(_ID_Mask, SamplerState_Linear_Repeat, uv);   // r skin, g boots, b opacity, a ball

            float3 colour = diffuse;
            #if defined(_USE_SUBSURFACE_SCATTERING)
            // Subsurface ramp along the half-Lambert main-light term, on skin.
            float halfLambert = dot(normalWS, mainLightDirection) * 0.5 + 0.5;
            float rampU = saturate(halfLambert * _SSS_Offset);
            float3 ramp = HL_SAMPLE(_SSS_Gradient_Ramp_Texture, SamplerState_Linear_Clamp, float2(rampU, 0)).rgb;
            float3 sssColour = lerp(diffuse, diffuse * ramp, rampU);
            colour += id.r * _SSS_Opacity * rampU * (diffuse * ramp - diffuse);
            #endif

            // The boots mask: matcap where it is clear of the blend amount.
            float notBoots = 1.0 - id.g;
            #if defined(_USE_MATCAP)
            colour = lerp(matcap, colour, saturate(notBoots + _Matcap_Blend_Amount));
            #endif

            #if defined(_USE_BALLMODE)
            // Ball-mode noise, weighted by the ball mask.
            float3 noise = saturate(HL_SAMPLE(_Ball_Noise_Texture, SamplerState_Linear_Repeat, uv * _Ball_UV_Tiling + _Ball_UV_Offset).rgb + _Ball_Noise_Intensity);
            colour = lerp(colour, (colour + noise) * _Ball_Noise_Colour.rgb, id.a * _Ball_Noise_Opacity);
            #endif

            // Skin-only rim light from a camera-faded light position, smoothstepped.
            float3 fresnelTexture = HL_SAMPLE(_Fresnel_Texture, sampler_Fresnel_Texture, uv).rgb;
            float rimLow = _Rim_Amount - _Rim_Softness;
            float rimHigh = _Rim_Amount + _Rim_Softness;
            float facing = 1.0 - dot(viewDirVS, normalVS);
            float cameraFade = saturate((clipW - 0.5) * _Rim_Camera_Fade_Position) * 4.5;
            float rim = saturate((facing * dot(cameraFade * _Rim_Light_Position, normalVS) - rimLow) / (rimHigh - rimLow));
            rim = min(rim * rim * (3.0 - 2.0 * rim), 1.0);
            rim = saturate(id.r * rim * _Rim_Opacity);
            float3 rimColour = fresnelTexture * _Rim_Colour.rgb;
            float3 albedo = lerp(colour, rimColour, rim);
            s.albedo = albedo * _Diff_Colour.rgb;

            // Normals: the lighting normal flips on back faces when requested.
            float3 normalTS = UnpackNormal(HL_SAMPLE(_Norm_Texture, SamplerState_Linear_Repeat, uv));
            s.normalTS = _Update_backface_Normals != 0.0 ? (isFrontFace ? 1.0 : -1.0) * normalTS : normalTS;
            // As the original: the bitangent is built from the raw interpolants, and the basis shares one
            // normalisation factor, so only the final normal is normalised.
            float3 bitangentWS = bitangentSign * cross(rawNormalWS, rawTangentWS);
            float3 fresnelNormalWS = normalize(rawTangentWS * normalTS.x + bitangentWS * normalTS.y + rawNormalWS * normalTS.z);

            // Emission: fresnel boost toward the emissive texture, the subsurface colour by the
            // half-Lambert term, matcap emission where the boots mask is set, erosion edge glow, rim.
            float fresnel = saturate(1.0 - pow(1.0 - saturate(dot(fresnelNormalWS, normalize(viewDirWS))), _Fresnel_Amount) * _Fresnel_Intensity);
            float3 emission = lerp(fresnelTexture * _Fresnel_Boost_Colour.rgb, HL_SAMPLE(_Emissive_Texture, SamplerState_Linear_Repeat, uv).rgb * _Emissive_Colour.rgb, fresnel);
            #if defined(_USE_SUBSURFACE_SCATTERING)
            // The emission weight is the skin mask times the opacity (Forward 309/315, Meta 735/737); the
            // half-Lambert term only shapes the ramp coordinate.
            emission = lerp(emission, sssColour, id.r * _SSS_Opacity * _SSS_Emissive_Intensity);
            #endif
            // Boots emit the matcap when it is enabled, and nothing otherwise.
            #if defined(_USE_MATCAP)
            emission = lerp(matcap * _Matcap_Emissive_Intensity, emission, notBoots);
            #else
            emission *= notBoots;
            #endif
            float erosion = HL_SAMPLE(_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D,
                                      sampler_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D, uv * _ErosionTiling).b;
            erosion = saturate((_Erosion_Progress - 0.5) * 2.0 + erosion);
            float erosionEdge = saturate((erosion - 0.35) * 3.33333325);
            emission = emission * _Global_Emission_Intensity + erosionEdge * _ErosionColor.rgb;
            s.emission = lerp(emission, rimColour, rim);

            half2 metalSmooth = saturate(HL_SAMPLE(_MS_Texture, SamplerState_Linear_Repeat, uv).rg);
            s.metallic = metalSmooth.x;
            s.smoothness = metalSmooth.y;

            // Opacity mask minus erosion, times the colour alpha.
            s.alpha = saturate(id.b - erosion) * _Diff_Colour.a;
            return s;
        }

        // The Forward pass clips the graph alpha at 0.5, sharpened for alpha to coverage when it is
        // available (and then keeps the sharpened alpha). The Meta, depth and shadow passes clip the bare
        // alpha at 0.5.
        void HLCharForwardClip(inout HLCharSurface s)
        {
            #if defined(_ALPHATEST_ON)
            float clipValue = s.alpha - 0.5;
            float width = abs(ddx(s.alpha)) + abs(ddy(s.alpha));
            float sharpened = saturate((clipValue - width * 0.5) / max(width, 9.99999975e-05) + 1.0);
            if (_AlphaToMaskAvailable != 0.0)
            {
                clipValue = sharpened - 9.99999975e-05;
                s.alpha = sharpened;
            }
            s.alpha = saturate(s.alpha);
            clip(clipValue);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend], [_SrcBlend] [_DstBlend]
            ZTest [_ZTest]
            ZWrite [_ZWrite]
            Cull [_Cull]
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // The ShaderGraph Lit target computes fog per fragment from view depth.
            #define _FOG_FRAGMENT 1
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
            #pragma shader_feature_local_fragment _ _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma shader_feature_local _ _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _USE_MATCAP
            #pragma shader_feature_local _ _USE_SUBSURFACE_SCATTERING
            #pragma shader_feature_local _ _USE_BALLMODE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv0 : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                half4 fogFactorAndVertexLight : TEXCOORD5;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD6;
                #endif
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
                #if defined(DYNAMICLIGHTMAP_ON)
                float2 dynamicLightmapUV : TEXCOORD8;
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
                half fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #endif
                output.fogFactorAndVertexLight = half4(fogFactor, VertexLighting(vertexInput.positionWS, normalInput.normalWS));
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

            void Frag(Varyings input, bool isFrontFace : SV_IsFrontFace, out half4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                HLCharSurface s = EvaluateHLChar(input.uv, input.positionWS, input.normalWS, input.tangentWS.xyz, tangentSign, isFrontFace, _MainLightPosition.xyz);
                HLCharForwardClip(s);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // The raw interpolated basis, as the original transforms the lighting normal before normalising.
                inputData.tangentToWorld = half3x3(input.tangentWS.xyz, tangentSign * cross(input.normalWS, input.tangentWS.xyz), input.normalWS);
                inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(s.normalTS, inputData.tangentToWorld));
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

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = s.albedo;
                surfaceData.metallic = s.metallic;
                surfaceData.smoothness = s.smoothness;
                surfaceData.normalTS = s.normalTS;
                surfaceData.emission = s.emission;
                surfaceData.occlusion = 1;
                // As the generated pass: alpha test already clipped, transparency keeps the graph alpha,
                // and opaque surfaces use 1; the surface alpha is then saturated.
                #if defined(_ALPHATEST_ON) || defined(_SURFACE_TYPE_TRANSPARENT)
                surfaceData.alpha = saturate(s.alpha);
                #else
                surfaceData.alpha = 1;
                #endif
                #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                #if defined(_SURFACE_TYPE_TRANSPARENT)
                color.a = OutputAlpha(color.a, true);
                #else
                color.a = OutputAlpha(color.a, false);
                #endif
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv0 : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
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
                output.uv = input.uv0;
                return output;
            }

            // Shadow and depth passes clip on the same opacity: ID mask blue minus erosion, times colour alpha.
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                #if defined(_ALPHATEST_ON)
                float erosion = saturate((_Erosion_Progress - 0.5) * 2.0 + HL_SAMPLE(_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D,
                    sampler_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D, input.uv * _ErosionTiling).b);
                clip(saturate(HL_SAMPLE(_ID_Mask, SamplerState_Linear_Repeat, input.uv).b - erosion) * _Diff_Colour.a - 0.5);
                #endif
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON

            struct Attributes { float4 positionOS : POSITION; float2 uv0 : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

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

            half Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(_ALPHATEST_ON)
                float erosion = saturate((_Erosion_Progress - 0.5) * 2.0 + HL_SAMPLE(_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D,
                    sampler_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D, input.uv * _ErosionTiling).b);
                clip(saturate(HL_SAMPLE(_ID_Mask, SamplerState_Linear_Repeat, input.uv).b - erosion) * _Diff_Colour.a - 0.5);
                #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZTest LEqual
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv0 : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
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
                output.uv = input.uv0;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                return output;
            }

            void Frag(Varyings input, bool isFrontFace : SV_IsFrontFace, out half4 outNormalWS : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(_ALPHATEST_ON)
                float erosion = saturate((_Erosion_Progress - 0.5) * 2.0 + HL_SAMPLE(_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D,
                    sampler_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D, input.uv * _ErosionTiling).b);
                clip(saturate(HL_SAMPLE(_ID_Mask, SamplerState_Linear_Repeat, input.uv).b - erosion) * _Diff_Colour.a - 0.5);
                #endif
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                float3 normalTS = UnpackNormal(HL_SAMPLE(_Norm_Texture, SamplerState_Linear_Repeat, input.uv));
                normalTS = _Update_backface_Normals != 0.0 ? (isFrontFace ? 1.0 : -1.0) * normalTS : normalTS;
                // The raw interpolated basis, normalised once after the transform, as the original does.
                float3 normalMapped = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,
                    half3x3(input.tangentWS.xyz, tangentSign * cross(input.normalWS, input.tangentWS.xyz), input.normalWS)));
                outNormalWS = half4(normalMapped, 0.0);
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
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma shader_feature_local _ _USE_MATCAP
            #pragma shader_feature_local _ _USE_SUBSURFACE_SCATTERING
            #pragma shader_feature_local _ _USE_BALLMODE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                #if defined(EDITOR_VISUALIZATION)
                float2 VizUV : TEXCOORD4;
                float4 LightCoord : TEXCOORD5;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
                output.uv = input.uv0;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(input.positionOS.xyz, input.uv0, input.uv1, input.uv2, output.VizUV, output.LightCoord);
                #endif
                return output;
            }

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                HLCharSurface s = EvaluateHLChar(input.uv, input.positionWS, input.normalWS, input.tangentWS.xyz, tangentSign, isFrontFace, _MainLightPosition.xyz);
                #if defined(_ALPHATEST_ON)
                clip(s.alpha - 0.5);   // Meta 736: the bare alpha, without the Forward pass's sharpening
                #endif
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = s.albedo;
                metaInput.Emission = s.emission;
                #if defined(EDITOR_VISUALIZATION)
                metaInput.VizUV = input.VizUV;
                metaInput.LightCoord = input.LightCoord;
                #endif
                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
