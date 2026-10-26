// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_FoliageWind, a URP 14 Lit Shader Graph (platform 14), opaque and
// two-sided with alpha clipping (alpha to coverage in Forward). A wind texture, read at the vertex's world XY
// and scrolled by game time, sways the vertices along world X by the vertex colour's red; the surface is the
// diffuse texture (alpha clipped), a normal map and the metal/smoothness texture's red as smoothness
// (dielectric; no emission).
Shader "HardLight/Environment/HL_Env_FoliageWind"
{
    Properties
    {
        __Documentation_Link_Below_HL_Env_FoliageWind ("## Documentation Link Below : HL_Env_FoliageWind", Float) = 0
        __https_hardlight_atlassian_net_wiki_spaces_HL_pages_8326453_SDT_HL_Env_FoliageWind_https_hardlight_atlassian_net_wiki_spaces_HL_pages_8326453_SDT_HL_Env_FoliageWind ("[https://hardlight.atlassian.net/wiki/spaces/HL/pages/8326453/SDT+-+HL+Env+FoliageWind](https://hardlight.atlassian.net/wiki/spaces/HL/pages/8326453/SDT+-+HL+Env+FoliageWind)", Float) = 0
        __2 ("# (2)", Float) = 0
        __Surface_Inputs ("## Surface Inputs", Float) = 0
        [NoScaleOffset]_Diffuse ("-- Diff Tex", 2D) = "white" {}
        _AlphaClipping ("-- AlphaClipping", Range(0, 1)) = 0
        __NOTE_R_Metal_G_Smoothness ("!NOTE R= Metal, G= Smoothness", Float) = 0
        [NoScaleOffset]_MS_Tex ("-- MS Tex", 2D) = "white" {}
        _Roughness_Strength ("-- Smoothness Intensity", Range(0, 1)) = 0
        __NOTE_Normal ("!NOTE Normal", Float) = 0
        [NoScaleOffset][Normal]_Normal_Map ("-- Norm Tex", 2D) = "bump" {}
        __1 ("# (1)", Float) = 0
        __Wind ("## Wind", Float) = 0
        _WindScale ("-- WindScale", Float) = 0.1
        _WindSpeed ("-- WindSpeed", Float) = 0.05
        _WindStrength ("-- WindStrength", Range(0, 1)) = 0.3
        _ ("#", Float) = 0
        [NoScaleOffset][NonModifiableTextureData]_SampleTexture2DLOD_47aee9514d6f42a8ab4f6851147fbbe5_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
        [HideInInspector]_QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl ("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" "Queue"="AlphaTest" "DisableBatching"="False" "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalLitSubTarget" }

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float4 _SampleTexture2DLOD_47aee9514d6f42a8ab4f6851147fbbe5_Texture_1_Texture2D_TexelSize;
            float __https_hardlight_atlassian_net_wiki_spaces_HL_pages_8326453_SDT_HL_Env_FoliageWind_https_hardlight_atlassian_net_wiki_spaces_HL_pages_8326453_SDT_HL_Env_FoliageWind;
            float __2;
            float __Surface_Inputs;
            float __Documentation_Link_Below_HL_Env_FoliageWind;
            float4 _Diffuse_TexelSize;
            float _WindSpeed;
            float __NOTE_R_Metal_G_Smoothness;
            float _AlphaClipping;
            float _WindScale;
            float _WindStrength;
            float __1;
            float4 _MS_Tex_TexelSize;
            float _;
            float _Roughness_Strength;
            float4 _Normal_Map_TexelSize;
            float __Wind;
            float __NOTE_Normal;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;

        TEXTURE2D(_SampleTexture2DLOD_47aee9514d6f42a8ab4f6851147fbbe5_Texture_1_Texture2D);
        SAMPLER(sampler_SampleTexture2DLOD_47aee9514d6f42a8ab4f6851147fbbe5_Texture_1_Texture2D);
        TEXTURE2D(_Diffuse);
        SAMPLER(sampler_Diffuse);
        TEXTURE2D(_MS_Tex);
        SAMPLER(sampler_MS_Tex);
        TEXTURE2D(_Normal_Map);
        SAMPLER(sampler_Normal_Map);

        // The wind texture's red at the vertex's world XY (scaled, plus game time times the speed), times the
        // strength, pushes the world position along X by the vertex colour's red; the graph returns it to object
        // space for the rest of the vertex stage.
        float3 WindPositionOS(float3 positionOS, float4 color)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float2 uv = positionWS.xy * _WindScale + _HLShaderTimeGame * _WindSpeed;
            float wind = SAMPLE_TEXTURE2D_LOD(_SampleTexture2DLOD_47aee9514d6f42a8ab4f6851147fbbe5_Texture_1_Texture2D,
                sampler_SampleTexture2DLOD_47aee9514d6f42a8ab4f6851147fbbe5_Texture_1_Texture2D, uv, 0).x * _WindStrength;
            return TransformWorldToObject(positionWS + color.rgb * float3(wind, 0.0, 0.0));
        }

        float4 Diffuse(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_Diffuse, sampler_Diffuse, uv);
        }

        // The normal map, AG or RG packed, at full strength.
        float3 FoliageNormalTS(float2 uv)
        {
            return UnpackNormalmapRGorAG(SAMPLE_TEXTURE2D(_Normal_Map, sampler_Normal_Map, uv));
        }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend One Zero
            ZTest LEqual
            ZWrite On
            Cull Off
            AlphaToMask On

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
                VertexPositionInputs vertexInput = GetVertexPositionInputs(WindPositionOS(input.positionOS.xyz, input.color));
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
                float4 diffuse = Diffuse(uv);

                // The diffuse alpha clips at the threshold, sharpened for alpha to coverage when it is available
                // (and the threshold is above 0), which then also becomes the output alpha (1 otherwise).
                float width = abs(ddx(diffuse.a)) + abs(ddy(diffuse.a));
                float sharpened = _AlphaClipping <= 0.0 ? 1.0
                    : saturate((diffuse.a - _AlphaClipping - width * 0.5) / max(width, 9.99999975e-05) + 1.0);
                bool sharpen = _AlphaToMaskAvailable != 0.0 && _AlphaClipping > 0.0;
                clip(sharpen ? sharpened - 9.99999975e-05 : diffuse.a - _AlphaClipping);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // The raw interpolated basis, as the original transforms the lighting normal before normalising.
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                inputData.tangentToWorld = float3x3(input.tangentWS.xyz, tangentSign * cross(input.normalWS, input.tangentWS.xyz), input.normalWS);
                float3 normalTS = FoliageNormalTS(uv);
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

                // Dielectric (metallic 0); the metal/smoothness texture's red times the intensity is the smoothness.
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = diffuse.rgb;
                surfaceData.metallic = 0;
                surfaceData.smoothness = saturate(SAMPLE_TEXTURE2D(_MS_Tex, sampler_MS_Tex, uv).r * _Roughness_Strength);
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1;
                surfaceData.alpha = 1;
                #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = _AlphaToMaskAvailable != 0.0 ? sharpened : 1.0;
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
            Cull Off

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
                float3 positionWS = TransformObjectToWorld(WindPositionOS(input.positionOS.xyz, input.color));
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
                clip(Diffuse(input.uv.xy).a - _AlphaClipping);
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON

            struct Attributes { float4 positionOS : POSITION; float4 uv0 : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float4 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(WindPositionOS(input.positionOS.xyz, input.color));
                output.uv = input.uv0;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(Diffuse(input.uv.xy).a - _AlphaClipping);
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
            Cull Off

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
                output.positionCS = TransformObjectToHClip(WindPositionOS(input.positionOS.xyz, input.color));
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
                clip(Diffuse(input.uv.xy).a - _AlphaClipping);
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, tangentSign * cross(input.normalWS, input.tangentWS.xyz), input.normalWS);
                outNormalWS = float4(NormalizeNormalPerPixel(TransformTangentToWorld(FoliageNormalTS(input.uv.xy), tangentToWorld)), 0.0);
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
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                #if defined(EDITOR_VISUALIZATION)
                float2 vizUV : TEXCOORD3;
                float4 lightCoord : TEXCOORD4;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionOS = WindPositionOS(input.positionOS.xyz, input.color);
                output.positionCS = UnityMetaVertexPosition(positionOS, input.uv1.xy, input.uv2.xy);
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(positionOS, input.uv0.xy, input.uv1.xy, input.uv2.xy, output.vizUV, output.lightCoord);
                #endif
                return output;
            }

            // The diffuse colour is the albedo, clipped by its alpha; there is no emission.
            float4 Frag(Varyings input) : SV_Target
            {
                float4 diffuse = Diffuse(input.uv0.xy);
                clip(diffuse.a - _AlphaClipping);
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = diffuse.rgb;
                metaInput.Emission = 0;
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
