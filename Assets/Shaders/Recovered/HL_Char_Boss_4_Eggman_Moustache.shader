// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Character/HL_Char_Boss_4_Eggman_Moustache, a URP 14 Lit Shader Graph (platform 14),
// transparent and two-sided without depth writes. A distorted, scrolling diffuse screened with a gradient colour by
// vertex alpha (also its emission), a strength-scaled normal map, an opacity texture, and wind on vertex alpha.
Shader "HardLight/Character/HL_Char_Boss_4_Eggman_Moustache"
{
    Properties
    {
        _Seperator ("#", Float) = 0
        _Title_Surface_Inputs ("## Surface Inputs", Float) = 0
        _Alpha ("-- Alpha", Range(0, 1)) = 1
        _Diff_Texture ("-- Diff Texture", 2D) = "white" {}
        _Emission_Strength ("-- Emission Strength", Range(0, 2)) = 1
        _Diff_UV_Speed_X ("-- Diff UV Speed X", Range(0, 1)) = 0
        _Diff_UV_Speed_Y ("-- Diff UV Speed Y", Range(0, 1)) = 0
        _Smoothness ("-- Smoothness", Range(0, 1)) = 0.5
        _Metalness ("-- Metalness", Range(0, 1)) = 0.5
        _Seperator_1 ("# (1)", Float) = 0
        _Title_Distortion ("## Distortion", Float) = 0
        _Distortion_Intensity ("-- Distortion Intensity", Range(0, 0.2)) = 0
        _Distortion_Texture ("-- Distortion Texture", 2D) = "white" {}
        _Distortion_Speed_X ("-- Distortion Speed X", Range(0, 1)) = 0
        _Distortion_Speed_Y ("-- Distortion Speed Y", Range(0, 1)) = 0
        _Seperator_3 ("# (3)", Float) = 0
        [NoScaleOffset][Normal]_Stache_Normal ("-- Stache Normal", 2D) = "bump" {}
        _Normal_Strength ("-- Normal Strength", Range(0, 5)) = 1
        [NoScaleOffset]_Stache_Opacity ("-- Stache Opacity", 2D) = "white" {}
        _Vertex_Alpha_Opacity_Strength ("-- Vertex Alpha Opacity Strength", Range(0, 1)) = 1
        _Moustache_Gradient_Colour ("-- Moustache Gradient Colour", Color) = (1,0,0.953536,0)
        [ToggleUI]_Invert_Gradient ("-- Invert Gradient?", Float) = 1
        _Moustache_Gradient_Strength ("-- Moustache Gradient Strength", Range(0, 1)) = 1
        _Moustache_Gradient_Mask_Power ("-- Moustache Gradient Mask Power", Range(0, 5)) = 1
        _Seperator_4 ("# (4)", Float) = 0
        _Title_Distortion_1 ("## Wind", Float) = 0
        _Wind_Strength ("-- Wind Strength", Range(0, 1)) = 0
        _WindSpeed ("-- Wind Speed", Range(0, 100)) = 50
        _WindScale ("-- Wind Scale", Range(0, 999)) = 1
        [NoScaleOffset][NonModifiableTextureData]_SampleTexture2DLOD_4ece03de67e24273978bbf408352a95d_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
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
            float4 _SampleTexture2DLOD_4ece03de67e24273978bbf408352a95d_Texture_1_Texture2D_TexelSize;
            float4 _Diff_Texture_TexelSize;
            float4 _Diff_Texture_ST;
            float _Distortion_Intensity;
            float _Seperator_1;
            float _Diff_UV_Speed_X;
            float _Diff_UV_Speed_Y;
            float _Distortion_Speed_X;
            float _Metalness;
            float _Title_Distortion_1;
            float _Distortion_Speed_Y;
            float4 _Distortion_Texture_TexelSize;
            float4 _Distortion_Texture_ST;
            float _Alpha;
            float _Seperator_3;
            float _Title_Surface_Inputs;
            float _Title_Distortion;
            float _Seperator;
            float _Seperator_4;
            float4 _Stache_Opacity_TexelSize;
            float4 _Stache_Normal_TexelSize;
            float _Normal_Strength;
            float _Emission_Strength;
            float _WindScale;
            float _WindSpeed;
            float _Wind_Strength;
            float4 _Moustache_Gradient_Colour;
            float _Moustache_Gradient_Strength;
            float _Moustache_Gradient_Mask_Power;
            float _Invert_Gradient;
            float _Vertex_Alpha_Opacity_Strength;
            float _Smoothness;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;

        TEXTURE2D(_Diff_Texture);
        TEXTURE2D(_Distortion_Texture);
        TEXTURE2D(_Stache_Normal);
        SAMPLER(sampler_Stache_Normal);
        TEXTURE2D(_Stache_Opacity);
        SAMPLER(sampler_Stache_Opacity);
        // The graph's immutable wind noise texture.
        TEXTURE2D(_SampleTexture2DLOD_4ece03de67e24273978bbf408352a95d_Texture_1_Texture2D);
        SAMPLER(sampler_SampleTexture2DLOD_4ece03de67e24273978bbf408352a95d_Texture_1_Texture2D);
        // The graph's inline sampler: linear, repeat.
        SAMPLER(SamplerState_Linear_Repeat);

        // Wind: the noise's red (LOD 0) at the vertex's world XY times the wind scale / 1000 over the object's X and
        // Y scale, plus game time times the speed / 1000, remapped by 1.5 - 0.5 and times the strength, lifts the world
        // Y by that times the object's X scale, faded in by vertex alpha; the graph returns it to object space.
        float3 MoustachePositionOS(float3 positionOS, float4 color)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float2 scale = float2(length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x)),
                                  length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y)));
            float2 uv = positionWS.xy * (_WindScale * 0.001 / scale) + _HLShaderTimeGame * _WindSpeed * 0.001;
            float wind = (SAMPLE_TEXTURE2D_LOD(_SampleTexture2DLOD_4ece03de67e24273978bbf408352a95d_Texture_1_Texture2D, sampler_SampleTexture2DLOD_4ece03de67e24273978bbf408352a95d_Texture_1_Texture2D, uv, 0).x * 1.5 - 0.5) * _Wind_Strength;
            float3 windWS = float3(positionWS.x, positionWS.y + wind * scale.x, positionWS.z);
            return TransformWorldToObject(lerp(positionWS, windWS, color.a));
        }

        // The tangent-space normal: the moustache normal map (red times alpha, green) with its XY times the normal
        // strength and its Z eased from 1 by the saturated strength (Shader Graph's Normal Strength).
        float3 MoustacheNormalTS(float2 uv)
        {
            float3 normal = UnpackNormalmapRGorAG(SAMPLE_TEXTURE2D(_Stache_Normal, sampler_Stache_Normal, uv));
            return float3(normal.xy * _Normal_Strength, lerp(1.0, normal.z, saturate(_Normal_Strength)));
        }

        // The diffuse at UV0 offset by the distortion texture's red and green (tiled, offset and scrolled) times the
        // intensity, scrolled by game time times its speed, through its tiling and offset. The gradient colour times
        // its strength is screened over it, shown by vertex alpha (or one minus it when inverted) times the mask power.
        float4 MoustacheColour(float2 uv, float4 color)
        {
            float2 distortionUV = uv * _Distortion_Texture_ST.xy + (_HLShaderTimeGame * float2(_Distortion_Speed_X, _Distortion_Speed_Y) + _Distortion_Texture_ST.zw);
            float2 distorted = SAMPLE_TEXTURE2D(_Distortion_Texture, SamplerState_Linear_Repeat, distortionUV).xy * _Distortion_Intensity + uv;
            float2 diffuseUV = (distorted + _HLShaderTimeGame * float2(_Diff_UV_Speed_X, _Diff_UV_Speed_Y)) * _Diff_Texture_ST.xy + _Diff_Texture_ST.zw;
            float4 diffuse = SAMPLE_TEXTURE2D(_Diff_Texture, SamplerState_Linear_Repeat, diffuseUV);
            float3 screen = 1.0 - (1.0 - _Moustache_Gradient_Colour.rgb * _Moustache_Gradient_Strength) * (1.0 - diffuse.rgb);
            float mask = (_Invert_Gradient != 0.0 ? 1.0 - color.a : color.a) * _Moustache_Gradient_Mask_Power;
            return float4(lerp(diffuse.rgb, screen, mask), diffuse.a);
        }

        // The alpha: the diffuse alpha times vertex red plus the alpha, capped by the opacity texture's blue, times
        // (1 - vertex alpha) - the vertex alpha opacity strength + 1, saturated.
        float MoustacheAlpha(float2 uv, float4 color, float diffuseAlpha)
        {
            float alpha = min(diffuseAlpha * color.r + _Alpha, SAMPLE_TEXTURE2D(_Stache_Opacity, sampler_Stache_Opacity, uv).b);
            return saturate(alpha * ((1.0 - color.a) - _Vertex_Alpha_Opacity_Strength + 1.0));
        }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off
            Cull Off

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
                float4 color : TEXCOORD4;
                float4 fogFactorAndVertexLight : TEXCOORD5;
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
                VertexPositionInputs vertexInput = GetVertexPositionInputs(MoustachePositionOS(input.positionOS.xyz, input.color));
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv = input.uv0;
                output.color = input.color;
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
                float3 normalTS = MoustacheNormalTS(uv);
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

                float4 colour = MoustacheColour(uv, input.color);
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = colour.rgb;
                surfaceData.metallic = saturate(_Metalness);
                surfaceData.smoothness = saturate(_Smoothness);
                surfaceData.emission = colour.rgb * _Emission_Strength;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1;
                surfaceData.alpha = MoustacheAlpha(uv, input.color, colour.a);
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
                float4 color : TEXCOORD2;
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
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = TransformObjectToHClip(MoustachePositionOS(input.positionOS.xyz, input.color));
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv = input.uv0;
                output.color = input.color;
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
                outNormalWS = float4(NormalizeNormalPerPixel(TransformTangentToWorld(MoustacheNormalTS(input.uv.xy), tangentToWorld)), 0.0);
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
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 color : TEXCOORD3;
                #if defined(EDITOR_VISUALIZATION)
                float2 vizUV : TEXCOORD5;
                float4 lightCoord : TEXCOORD6;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = UnityMetaVertexPosition(MoustachePositionOS(input.positionOS.xyz, input.color), input.uv1.xy, input.uv2.xy);
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                output.color = input.color;
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(input.positionOS.xyz, input.uv0.xy, input.uv1.xy, input.uv2.xy, output.vizUV, output.lightCoord);
                #endif
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 colour = MoustacheColour(input.uv0.xy, input.color).rgb;
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = colour;
                metaInput.Emission = colour * _Emission_Strength;
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
