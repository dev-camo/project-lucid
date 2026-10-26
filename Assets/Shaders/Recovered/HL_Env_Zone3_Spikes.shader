// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Zone3_Spikes, a URP 14 Lit Shader Graph (platform 14), opaque.
// A scrolling displacement texture pushes the vertices along their normals (masked by vertex blue); the surface is
// the tinted diffuse under a fresnel colour, a normal map, the metal/smoothness texture and an emissive texture.
Shader "HardLight/Environment/HL_Env_Zone3_Spikes"
{
    Properties
    {
        _Separator_0 ("#", Float) = 1
        _Title_Vertex_Colour ("## Vertex Colour", Float) = 1
        _Title_Red ("### Red =", Float) = 1
        _Title_Green ("### Green =", Float) = 1
        _Title_Blue_Vertex_Displacement_Mask ("### Blue = Vertex Displacement Mask", Float) = 1
        _Title_Alpha ("### Alpha =", Float) = 1
        _Separator_1 ("# (1)", Float) = 1
        _Title_Diffuse ("## Diffuse", Float) = 1
        _Diff_Tex ("-- Diff Texture", 2D) = "white" {}
        _Diff_Tint ("-- Diff Colour Tint", Color) = (1,1,1,0)
        _Separator_2 ("# (2)", Float) = 1
        _Title_MS ("## MS", Float) = 1
        _NOTE_R_Metal_G_Smoothness ("!NOTE R = Metal, G = Smoothness", Float) = 0
        [NoScaleOffset]_MS_Tex ("-- MS Texture", 2D) = "white" {}
        _Metal_Intensity ("-- Metal Intensity", Range(0, 1)) = 0
        _Smoothness_Intensity ("-- Smoothness Intensity", Range(0, 1)) = 1
        _Separator_3 ("# (3)", Float) = 1
        _Title_Normal ("## Normal", Float) = 1
        [NoScaleOffset][Normal]_Normal_Tex ("-- Norm Texture", 2D) = "bump" {}
        _Separator_4 ("# (4)", Float) = 1
        _Title_Emission ("## Emission", Float) = 1
        [NoScaleOffset]_Emissive_Tex ("-- Emissive Texture", 2D) = "white" {}
        [HDR]_Emissive_Colour ("-- Emissive Colour", Color) = (0,0,0,0)
        _Separator_5 ("# (5)", Float) = 1
        _Title_Fresnel ("## Fresnel", Float) = 1
        [HDR]_Fresnel_Colour ("-- Fresnel Colour", Color) = (1,1,1,0)
        _Fresnel_Amount ("-- Fresnel Amount", Range(0, 10)) = 5
        _Fresnel_Intensity ("-- Fresnel Intensity", Range(1, 10)) = 1
        _Fresnel_Opacity ("-- Fresnel Opacity", Range(0, 1)) = 0
        _Separator_6 ("# (6)", Float) = 1
        _Title_Vertex_Displacement ("## Vertex Displacement", Float) = 1
        [NoScaleOffset]_Vertex_Displacement_Texture ("-- VD Texture", 2D) = "white" {}
        _Vertex_Displacement_UV_Speed_Direction ("-- VD UV Speed/Direction", Range(-1, 1)) = 1
        _Title_Noise ("### Red", Float) = 1
        _Red_Vertex_Displacement_Noise_Amount ("-- Red: VD Noise Amount", Range(-2, 2)) = 1
        _Title_Pulse ("### Green", Float) = 1
        _Green_Vertex_Displacement_Noise_Amount ("-- Green: VD Noise Amount", Range(-4, 4)) = 1
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
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float _Title_Red;
            float _Title_Green;
            float _Title_Vertex_Colour;
            float _Title_Alpha;
            float _Title_Blue_Vertex_Displacement_Mask;
            float _Title_Diffuse;
            float _Separator_1;
            float _Title_MS;
            float4 _MS_Tex_TexelSize;
            float _Separator_2;
            float _NOTE_R_Metal_G_Smoothness;
            float _Fresnel_Opacity;
            float _Fresnel_Intensity;
            float _Title_Normal;
            float4 _Normal_Tex_TexelSize;
            float _Separator_3;
            float _Title_Emission;
            float4 _Diff_Tex_TexelSize;
            float4 _Diff_Tex_ST;
            float4 _Emissive_Colour;
            float _Smoothness_Intensity;
            float _Metal_Intensity;
            float4 _Diff_Tint;
            float _Separator_4;
            float _Title_Fresnel;
            float4 _Fresnel_Colour;
            float _Fresnel_Amount;
            float4 _Emissive_Tex_TexelSize;
            float _Separator_5;
            float _Title_Vertex_Displacement;
            float _Title_Noise;
            float _Separator_0;
            float4 _Vertex_Displacement_Texture_TexelSize;
            float _Title_Pulse;
            float _Red_Vertex_Displacement_Noise_Amount;
            float _Separator_6;
            float _Green_Vertex_Displacement_Noise_Amount;
            float _Vertex_Displacement_UV_Speed_Direction;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;

        TEXTURE2D(_Vertex_Displacement_Texture);
        SAMPLER(sampler_Vertex_Displacement_Texture);
        TEXTURE2D(_Diff_Tex);
        TEXTURE2D(_MS_Tex);
        TEXTURE2D(_Normal_Tex);
        TEXTURE2D(_Emissive_Tex);
        // The graph's inline sampler: trilinear, repeat.
        SAMPLER(SamplerState_Trilinear_Repeat);

        // The displacement texture at UV0 scrolled by game time times the speed (on both axes), red and green
        // weighted by their amounts, pushes the vertex along its object normal, masked by one minus vertex blue.
        float3 SpikesPositionOS(float3 positionOS, float3 normalOS, float4 uv0, float4 color)
        {
            float2 uv = uv0.xy + _HLShaderTimeGame * _Vertex_Displacement_UV_Speed_Direction;
            float2 noise = SAMPLE_TEXTURE2D_LOD(_Vertex_Displacement_Texture, sampler_Vertex_Displacement_Texture, uv, 0).xy;
            float displacement = dot(noise, float2(_Red_Vertex_Displacement_Noise_Amount, _Green_Vertex_Displacement_Noise_Amount));
            return positionOS + (1.0 - color.b) * displacement * normalOS;
        }

        // Every surface texture shares the diffuse texture's tiling and offset.
        float2 SpikesUV(float2 uv)
        {
            return uv * _Diff_Tex_ST.xy + _Diff_Tex_ST.zw;
        }

        // The normal map, AG or RG packed, at full strength.
        float3 SpikesNormalTS(float2 uv)
        {
            return UnpackNormalmapRGorAG(SAMPLE_TEXTURE2D(_Normal_Tex, SamplerState_Trilinear_Repeat, SpikesUV(uv)));
        }

        // The view direction: towards the camera for perspective, the view matrix's z row for orthographic.
        float3 SpikesViewDirection(float3 positionWS)
        {
            return normalize(unity_OrthoParams.w == 0.0 ? normalize(_WorldSpaceCameraPos - positionWS) : UNITY_MATRIX_V[2].xyz);
        }

        struct SpikesSurface
        {
            float3 albedo;
            float3 emission;
            float metallic;
            float smoothness;
        };

        // The albedo: the fresnel colour over the tinted diffuse, by one minus the fresnel term
        // (pow(1 - saturate(n.v), amount) * intensity) times its opacity, saturated. The metal/smoothness texture's
        // red and green times their intensities, saturated; the emissive texture times its colour.
        SpikesSurface SpikesSurfaceInputs(float2 uv, float3 positionWS, float3 normalWS)
        {
            SpikesSurface surface;
            float2 surfaceUV = SpikesUV(uv);
            float3 diffuse = SAMPLE_TEXTURE2D(_Diff_Tex, SamplerState_Trilinear_Repeat, surfaceUV).rgb * _Diff_Tint.rgb;
            float fresnel = pow(1.0 - saturate(dot(normalWS, SpikesViewDirection(positionWS))), _Fresnel_Amount) * _Fresnel_Intensity;
            surface.albedo = lerp(_Fresnel_Colour.rgb, diffuse, saturate(1.0 - fresnel * _Fresnel_Opacity));
            surface.emission = SAMPLE_TEXTURE2D(_Emissive_Tex, SamplerState_Trilinear_Repeat, surfaceUV).rgb * _Emissive_Colour.rgb;
            float2 metalSmoothness = saturate(SAMPLE_TEXTURE2D(_MS_Tex, SamplerState_Trilinear_Repeat, surfaceUV).xy * float2(_Metal_Intensity, _Smoothness_Intensity));
            surface.metallic = metalSmoothness.x;
            surface.smoothness = metalSmoothness.y;
            return surface;
        }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend One Zero
            ZTest LEqual
            ZWrite On
            Cull Back

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
                float4 color : COLOR;
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
                VertexPositionInputs vertexInput = GetVertexPositionInputs(SpikesPositionOS(input.positionOS.xyz, input.normalOS, input.uv0, input.color));
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
                float3 normalTS = SpikesNormalTS(uv);
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

                SpikesSurface surface = SpikesSurfaceInputs(uv, input.positionWS, inputData.normalWS);
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = surface.albedo;
                surfaceData.metallic = surface.metallic;
                surfaceData.smoothness = surface.smoothness;
                surfaceData.emission = surface.emission;
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 uv0 : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
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
                float3 positionWS = TransformObjectToWorld(SpikesPositionOS(input.positionOS.xyz, input.normalOS, input.uv0, input.color));
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 uv0 : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float4 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(SpikesPositionOS(input.positionOS.xyz, input.normalOS, input.uv0, input.color));
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float4 uv0 : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
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
                output.positionCS = TransformObjectToHClip(SpikesPositionOS(input.positionOS.xyz, input.normalOS, input.uv0, input.color));
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
                outNormalWS = float4(NormalizeNormalPerPixel(TransformTangentToWorld(SpikesNormalTS(input.uv.xy), tangentToWorld)), 0.0);
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
                float4 color : COLOR;
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
                float3 positionOS = SpikesPositionOS(input.positionOS.xyz, input.normalOS, input.uv0, input.color);
                output.positionCS = UnityMetaVertexPosition(positionOS, input.uv1.xy, input.uv2.xy);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                output.positionWS = TransformObjectToWorld(positionOS);
                output.normalWS = normalInput.normalWS;
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(positionOS, input.uv0.xy, input.uv1.xy, input.uv2.xy, output.vizUV, output.lightCoord);
                #endif
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, tangentSign * cross(input.normalWS, input.tangentWS.xyz), input.normalWS);
                float3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(SpikesNormalTS(input.uv0.xy), tangentToWorld));
                SpikesSurface surface = SpikesSurfaceInputs(input.uv0.xy, input.positionWS, normalWS);
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
