// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Character/HL_Char_Boss_4_Eggman, a URP 14 Lit Shader Graph (platform 14), transparent
// (alpha from the ID mask's blue) with depth writes. Subsurface scattering, a fabric normal map, a rotated detail
// overlay and a distance-faded fresnel emission are driven by an ID mask.
Shader "HardLight/Character/HL_Char_Boss_4_Eggman"
{
    Properties
    {
        _Separator_0 ("# (0)", Float) = 0
        _Title_Main_Textures ("## Main Textures", Float) = 0
        [NoScaleOffset]_Diff_Texture ("-- Diff Texture", 2D) = "white" {}
        [NoScaleOffset]_MS_Texture ("-- MS Texture", 2D) = "black" {}
        [NoScaleOffset][Normal]_Norm_Texture ("-- Norm Texture", 2D) = "bump" {}
        [NoScaleOffset][Normal]_Fabric_Norm_Texture ("-- Fabric Norm Texture", 2D) = "bump" {}
        _Fabric_Norm_Size ("-- Fabric Norm Size", Range(0, 100)) = 1
        [NoScaleOffset]_Detail_Texture ("-- Detail Texture", 2D) = "white" {}
        _Detail_Strength ("-- Detail Strength", Range(0, 1)) = 1
        _Detail_Size ("-- Detail Size", Range(0, 100)) = 1
        _Detail_Rotation ("-- Detail Rotation", Range(0, 180)) = 0
        _Title_ID_Mask ("## ID Mask", Float) = 0
        _NOTE_R_Skin_G_Boots_B_Opacity_A_Ball_Mask ("!NOTE R=Skin-G=Fabric-B=Opacity-A=Metal", Float) = 0
        [NoScaleOffset]_ID_Mask ("-- ID Mask", 2D) = "black" {}
        _Separator_1 ("# (1)", Float) = 0
        _Title_Emission ("## Emission", Float) = 0
        [NoScaleOffset]_Emissive_Texture ("-- Emissive Texture", 2D) = "black" {}
        [HDR]_Emissive_Colour ("-- Emissive Colour", Color) = (0,0,0,0)
        _Global_Emission_Intensity ("-- Global Emission Intensity", Range(0, 1)) = 1
        _SSS_Emissive_Intensity ("-- SSS Emissive Intensity", Range(0, 1)) = 1
        _Separator_2 ("# (2)", Range(0, 1)) = 0
        _Title_Fresnel ("## Fresnel Texture", Float) = 0
        _Fresnel_Amount ("-- Fresnel Amount]", Range(0, 3)) = 1
        _Fresnel_Intensity ("-- Fresnel Intensity", Range(0, 3)) = 1
        [NoScaleOffset]_Fresnel_Texture ("-- Fresnel Texture", 2D) = "white" {}
        _Title_Fresnel_Distance_Fade ("## Fresnel Distance Fade", Float) = 0
        [ToggleUI]_Fresnel_Distance_Fade ("-- Fresnel Distance Fade?", Float) = 0
        _Fresnel_Fade_Distance ("-- Fresnel Fade Distance [_Fresnel_Distance_Fade]", Range(0, 100)) = 1
        _NOTE_Optionally_fade_fresnel_alpha_with_camera_distance ("!NOTE Optionally fade fresnel alpha with camera distance.", Float) = 0
        _Separator_3 ("# (3)", Float) = 0
        _Title_Sub_Surface_Scattering ("## Sub Surface Scattering", Float) = 0
        [NoScaleOffset]_SSS_Gradient_Ramp_Texture ("-- SSS Gradient Ramp Texture", 2D) = "white" {}
        _SSS_Offset ("-- SSS Offset", Float) = 0
        _SSS_Opacity ("-- SSS Opacity", Range(0, 1)) = 1
        _Separator_4 ("# (4)", Float) = 0
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

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float _Title_Fresnel;
            float _Title_Emission;
            float _Title_ID_Mask;
            float _Title_Main_Textures;
            float4 _Norm_Texture_TexelSize;
            float _Fresnel_Intensity;
            float _Separator_1;
            float _Title_Sub_Surface_Scattering;
            float4 _Fabric_Norm_Texture_TexelSize;
            float4 _MS_Texture_TexelSize;
            float4 _Diff_Texture_TexelSize;
            float _Separator_2;
            float4 _ID_Mask_TexelSize;
            float4 _Emissive_Texture_TexelSize;
            float4 _Emissive_Colour;
            float _Global_Emission_Intensity;
            float _SSS_Emissive_Intensity;
            float _Separator_0;
            float _Fresnel_Amount;
            float _Separator_3;
            float4 _SSS_Gradient_Ramp_Texture_TexelSize;
            float _SSS_Offset;
            float _SSS_Opacity;
            float4 _Fresnel_Texture_TexelSize;
            float _NOTE_R_Skin_G_Boots_B_Opacity_A_Ball_Mask;
            float _Title_Fresnel_Distance_Fade;
            float4 _Detail_Texture_TexelSize;
            float _NOTE_Optionally_fade_fresnel_alpha_with_camera_distance;
            float _Detail_Rotation;
            float _Fabric_Norm_Size;
            float _Detail_Size;
            float _Detail_Strength;
            float _Fresnel_Fade_Distance;
            float _Separator_4;
            float _Fresnel_Distance_Fade;
        CBUFFER_END

        TEXTURE2D(_Diff_Texture);
        TEXTURE2D(_MS_Texture);
        TEXTURE2D(_Norm_Texture);
        TEXTURE2D(_Fabric_Norm_Texture);
        TEXTURE2D(_ID_Mask);
        TEXTURE2D(_Emissive_Texture);
        TEXTURE2D(_SSS_Gradient_Ramp_Texture);
        TEXTURE2D(_Detail_Texture);
        SAMPLER(sampler_Detail_Texture);
        TEXTURE2D(_Fresnel_Texture);
        SAMPLER(sampler_Fresnel_Texture);
        // The graph's inline samplers: linear repeat for most textures, linear clamp for the SSS ramp.
        SAMPLER(SamplerState_Linear_Repeat);
        SAMPLER(SamplerState_Linear_Clamp);

        // The normal map blended (Shader Graph's Normal Blend) with the fabric normal map, tiled by the fabric size
        // and faded in by the ID mask's green.
        float3 EggmanNormalTS(float2 uv)
        {
            float3 base = UnpackNormalmapRGorAG(SAMPLE_TEXTURE2D(_Norm_Texture, SamplerState_Linear_Repeat, uv));
            float3 fabric = UnpackNormalmapRGorAG(SAMPLE_TEXTURE2D(_Fabric_Norm_Texture, SamplerState_Linear_Repeat, uv * _Fabric_Norm_Size));
            fabric = lerp(float3(0.0, 0.0, 1.0), fabric, SAMPLE_TEXTURE2D(_ID_Mask, SamplerState_Linear_Repeat, uv).g);
            return SafeNormalize(float3(base.xy + fabric.xy, base.z * fabric.z));
        }

        // Shader Graph's Blend (Overlay) node.
        float3 BlendOverlay(float3 base, float3 blend, float opacity)
        {
            float3 result1 = 1.0 - 2.0 * (1.0 - base) * (1.0 - blend);
            float3 result2 = 2.0 * base * blend;
            float3 zeroOrOne = step(base, 0.5);
            return lerp(base, result2 * zeroOrOne + (1.0 - zeroOrOne) * result1, opacity);
        }

        // The detail UV: UV0 rotated about the centre by _Detail_Rotation (radians, through Shader Graph's Rotate
        // node matrix), then scaled by the detail size.
        float2 DetailUV(float2 uv)
        {
            float s = sin(_Detail_Rotation);
            float c = cos(_Detail_Rotation);
            float2x2 rotation = float2x2(c, -s, s, c);
            rotation = rotation * 0.5 + 0.5;
            rotation = rotation * 2.0 - 1.0;
            return (mul(uv - 0.5, rotation) + 0.5) * _Detail_Size;
        }

        struct EggmanSurface
        {
            float3 albedo;
            float3 emission;
            float metallic;
            float smoothness;
            float alpha;
        };

        // The surface from UV0, the world position, the (normalised) vertex normal and the mapped normal. The ID mask
        // channels: red the subsurface scattering, green the fabric normal, blue the opacity, alpha the detail.
        EggmanSurface EggmanSurfaceInputs(float2 uv, float3 positionWS, float3 vertexNormalWS, float3 normalWS)
        {
            EggmanSurface surface;
            float4 id = SAMPLE_TEXTURE2D(_ID_Mask, SamplerState_Linear_Repeat, uv);
            float3 diffuse = SAMPLE_TEXTURE2D(_Diff_Texture, SamplerState_Linear_Repeat, uv).rgb;

            // Subsurface scattering: the ramp at the wrapped Lambert main light times the offset (v 0) multiplies the
            // diffuse, faded in by that ramp coordinate, then by the ID mask's red times the opacity.
            float rampU = saturate((dot(vertexNormalWS, _MainLightPosition.xyz) * 0.5 + 0.5) * _SSS_Offset);
            float3 ramp = SAMPLE_TEXTURE2D(_SSS_Gradient_Ramp_Texture, SamplerState_Linear_Clamp, float2(rampU, saturate(0.0))).rgb;
            float3 scattered = lerp(diffuse, diffuse * ramp, rampU);
            float sss = id.r * _SSS_Opacity;
            float3 albedo = lerp(diffuse, scattered, sss);

            // Detail: overlaid on the albedo (at 0.3 of the strength) and its red on the smoothness, by the ID alpha.
            float3 detail = SAMPLE_TEXTURE2D(_Detail_Texture, sampler_Detail_Texture, DetailUV(uv)).rgb;
            surface.albedo = BlendOverlay(albedo, detail, _Detail_Strength * 0.3003003 * id.a);

            // Emission: the fresnel texture at facing angles towards the tinted emissive texture, optionally faded by
            // camera distance, then towards the scattered diffuse by the SSS emissive intensity.
            float3 viewWS = unity_OrthoParams.w == 0.0 ? normalize(_WorldSpaceCameraPos - positionWS) : UNITY_MATRIX_V[2].xyz;
            float facing = saturate(dot(normalize(normalWS), normalize(viewWS)));
            float fresnel = saturate(1.0 - pow(1.0 - facing, _Fresnel_Amount) * _Fresnel_Intensity);
            float3 emissive = SAMPLE_TEXTURE2D(_Emissive_Texture, SamplerState_Linear_Repeat, uv).rgb * _Emissive_Colour.rgb;
            float3 emission = lerp(SAMPLE_TEXTURE2D(_Fresnel_Texture, sampler_Fresnel_Texture, uv).rgb, emissive, fresnel);
            float fade = saturate((distance(positionWS, _WorldSpaceCameraPos) - _Fresnel_Fade_Distance) / (_Fresnel_Fade_Distance * 5.0));
            emission = lerp(emission, emission * fade, _Fresnel_Distance_Fade);
            surface.emission = lerp(emission, scattered, sss * _SSS_Emissive_Intensity) * _Global_Emission_Intensity;

            // Metal (red) and smoothness (green, with the detail's red overlaid at full strength by the ID alpha).
            float2 metalSmoothness = SAMPLE_TEXTURE2D(_MS_Texture, SamplerState_Linear_Repeat, uv).xy;
            surface.metallic = saturate(metalSmoothness.x);
            surface.smoothness = saturate(BlendOverlay(metalSmoothness.yyy, detail.rrr, id.a * _Detail_Strength).x);
            surface.alpha = saturate(id.b);
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
                float3 normalTS = EggmanNormalTS(uv);
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

                EggmanSurface surface = EggmanSurfaceInputs(uv, input.positionWS, normalize(input.normalWS), inputData.normalWS);
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = surface.albedo;
                surfaceData.metallic = surface.metallic;
                surfaceData.smoothness = surface.smoothness;
                surfaceData.emission = surface.emission;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1;
                surfaceData.alpha = surface.alpha;
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
                outNormalWS = float4(NormalizeNormalPerPixel(TransformTangentToWorld(EggmanNormalTS(input.uv.xy), tangentToWorld)), 0.0);
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
                float3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(EggmanNormalTS(input.uv0.xy), tangentToWorld));
                EggmanSurface surface = EggmanSurfaceInputs(input.uv0.xy, input.positionWS, normalize(input.normalWS), normalWS);
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = surface.albedo;
                metaInput.Emission = surface.emission;
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
