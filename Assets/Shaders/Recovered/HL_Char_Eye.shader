// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Character/HL_Char_Eye, a URP 14 Lit Shader Graph (platform 14), alpha clipped
// with alpha to coverage. The graph's surface inputs are rebuilt from the Metal programs; lighting, GI,
// shadows, fog and depth/meta output use URP 14.0.11's own library functions, as the graph's generated
// passes do.
Shader "HardLight/Character/HL_Char_Eye"
{
    Properties
    {
        _Documentation_Link_Below ("## Documentation Link Below : HL_Char_Eye", Float) = 0
        _Heading_This_shader_is_linked_to_animations ("### ***This shader is linked to animations***", Float) = 0
        _Documentation_Link ("[https://hardlight.atlassian.net/wiki/spaces/HL/pages/8326158/SDT+-+HL+Char+Eye](https://hardlight.atlassian.net/wiki/spaces/HL/pages/8326158/SDT+-+HL+Char+Eye)", Float) = 0
        _Separator_0 ("#", Float) = 0
        [NoScaleOffset]_EyeTextureAlbedoAlpha ("Eye Texture Mask", 2D) = "white" {}
        [NoScaleOffset][Normal]_EyeTextureNormal ("Eye Texture Normal", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 1
        [HDR]_EyeColor ("Eye Color", Color) = (0.1573993,1,0,0)
        [HDR]_GlintColour ("Glint Colour", Color) = (1.498039,1.498039,1.498039,1)
        _EmissionStrength ("Emission Strength", Range(0, 1)) = 0.1
        _Vertex_Colour ("Vertex Colour", Range(0, 1)) = 0
        _Global_UV_Scale ("Global UV Scale", Float) = 1
        _Global_UV_Horizontal ("Global UV Horizontal", Float) = 1
        _Global_UV_Vertical ("Global UV Vertical", Float) = 1
        _Left_Eye_UV_Scale ("Left Eye UV Scale", Float) = 1
        _LeftGlintHorizontal ("Left Glint Horizontal", Float) = 0
        _LeftGlintVertical ("Left Glint Vertical", Float) = 0
        _LeftEyeHorizontal ("Left Eye Horizontal", Float) = 0
        _LeftEyeVertical ("Left Eye Vertical", Float) = 0
        _Right_Eye_UV_Scale ("Right Eye UV Scale", Float) = 1
        _RightGlintHorizontal ("Right Glint Horizontal", Float) = 0
        _RightGlintVertical ("Right Glint Vertical", Float) = 0
        _RightEyeHorizontal ("Right Eye Horizontal", Float) = 0
        _RightEyeVertical ("Right Eye Vertical", Float) = 0
        _Separator_1 ("# (1)", Float) = 0
        _Erosion_Progress ("-- Erosion Progress", Range(0, 1)) = 0
        [HDR]_ErosionColor ("-- ErosionColor", Color) = (1,1,1,0)
        _ErosionTiling ("-- ErosionTiling", Float) = 1
        [NoScaleOffset][NonModifiableTextureData]_SampleTexture2D_b6ebbf4e151d47d491f3933b8ce35177_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
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
            float4 _SampleTexture2D_b6ebbf4e151d47d491f3933b8ce35177_Texture_1_Texture2D_TexelSize;
            float _Heading_This_shader_is_linked_to_animations;
            float _Global_UV_Scale;
            float _Separator_0;
            float _Separator_1;
            float _RightEyeHorizontal;
            float _RightEyeVertical;
            float4 _EyeTextureNormal_TexelSize;
            float _LeftEyeHorizontal;
            float _LeftEyeVertical;
            float4 _EyeTextureAlbedoAlpha_TexelSize;
            float _Smoothness;
            float _LeftGlintHorizontal;
            float _LeftGlintVertical;
            float _NormalStrength;
            float4 _GlintColour;
            float _EmissionStrength;
            float _RightGlintHorizontal;
            float _RightGlintVertical;
            float4 _EyeColor;
            float _Global_UV_Horizontal;
            float _Global_UV_Vertical;
            float _Vertex_Colour;
            float _Left_Eye_UV_Scale;
            float _Right_Eye_UV_Scale;
            float _Documentation_Link;
            float _Documentation_Link_Below;
            float4 _ErosionColor;
            float _Erosion_Progress;
            float _ErosionTiling;
        CBUFFER_END

        TEXTURE2D(_EyeTextureAlbedoAlpha);
        SAMPLER(sampler_EyeTextureAlbedoAlpha);
        TEXTURE2D(_EyeTextureNormal);
        SAMPLER(sampler_EyeTextureNormal);
        TEXTURE2D(_SampleTexture2D_b6ebbf4e151d47d491f3933b8ce35177_Texture_1_Texture2D);
        SAMPLER(sampler_SampleTexture2D_b6ebbf4e151d47d491f3933b8ce35177_Texture_1_Texture2D);

        // Each eye's mask UV: moved by the eye's offset (plus the glint's, for a glint lookup) and scaled about
        // the texture centre. Core.hlsl's SAMPLE_TEXTURE2D adds _GlobalMipBias.x once, as the original does.
        float2 EyeUV(float2 uv, float2 offset, float scale)
        {
            return (uv - offset) * (_Global_UV_Scale * float2(_Global_UV_Horizontal, _Global_UV_Vertical) * scale) + 0.5;
        }

        float2 LeftEyeUV(float2 uv) { return EyeUV(uv, float2(_LeftEyeHorizontal, _LeftEyeVertical), _Left_Eye_UV_Scale); }
        float2 RightEyeUV(float2 uv) { return EyeUV(uv, float2(_RightEyeHorizontal, _RightEyeVertical), _Right_Eye_UV_Scale); }

        // The eye colour before the vertex tint: iris (mask red, the two eyes multiplied) in the eye colour,
        // lerped (unclamped) to the glint colour by the two glints (mask blue, added), then to the bare iris by
        // the inverted background (mask green, the two eyes added).
        float3 EyeColour(float2 uv)
        {
            float2 left = SAMPLE_TEXTURE2D(_EyeTextureAlbedoAlpha, sampler_EyeTextureAlbedoAlpha, LeftEyeUV(uv)).xy;
            float2 right = SAMPLE_TEXTURE2D(_EyeTextureAlbedoAlpha, sampler_EyeTextureAlbedoAlpha, RightEyeUV(uv)).xy;
            float glint = SAMPLE_TEXTURE2D(_EyeTextureAlbedoAlpha, sampler_EyeTextureAlbedoAlpha,
                              EyeUV(uv, float2(_LeftEyeHorizontal, _LeftEyeVertical) + float2(_LeftGlintHorizontal, _LeftGlintVertical), _Left_Eye_UV_Scale)).z +
                          SAMPLE_TEXTURE2D(_EyeTextureAlbedoAlpha, sampler_EyeTextureAlbedoAlpha,
                              EyeUV(uv, float2(_RightEyeHorizontal, _RightEyeVertical) + float2(_RightGlintHorizontal, _RightGlintVertical), _Right_Eye_UV_Scale)).z;
            float iris = left.x * right.x;
            float3 colour = lerp(iris * _EyeColor.rgb, _GlintColour.rgb, glint);
            return lerp(colour, iris.xxx, 1.0 - (left.y + right.y));
        }

        float3 EyeAlbedo(float3 colour, float4 vertexColor)
        {
            return lerp(1.0, vertexColor.rgb, _Vertex_Colour) * colour;
        }

        // The erosion mask (blue of the graph's texture), pushed by the progress.
        float Erosion(float2 uv)
        {
            float mask = SAMPLE_TEXTURE2D(_SampleTexture2D_b6ebbf4e151d47d491f3933b8ce35177_Texture_1_Texture2D,
                sampler_SampleTexture2D_b6ebbf4e151d47d491f3933b8ce35177_Texture_1_Texture2D, uv * _ErosionTiling).z;
            return saturate((_Erosion_Progress - 0.5) * 2.0 + mask);
        }

        // The untinted eye colour at the emission strength, plus the erosion edge glow.
        float3 EyeEmission(float3 colour, float erosion)
        {
            return _EmissionStrength * colour + saturate((erosion - 0.35) * 3.33333325) * _ErosionColor.rgb;
        }

        // Both eyes' normals added, then the graph's Normal Strength (xy scaled, z lerped from 1).
        float3 EyeNormalTS(float2 uv)
        {
            float3 normal = UnpackNormal(SAMPLE_TEXTURE2D(_EyeTextureNormal, sampler_EyeTextureNormal, LeftEyeUV(uv))) +
                            UnpackNormal(SAMPLE_TEXTURE2D(_EyeTextureNormal, sampler_EyeTextureNormal, RightEyeUV(uv)));
            return float3(normal.xy * _NormalStrength, lerp(1.0, normal.z, saturate(_NormalStrength)));
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
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
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
                float3 colour = EyeColour(uv);
                float erosion = Erosion(uv);

                // The alpha (one minus the erosion) clips at 0.5, sharpened for alpha to coverage when it is
                // available, which then also becomes the output alpha (1 otherwise).
                float alpha = 1.0 - erosion;
                float clipValue = alpha - 0.5;
                float width = abs(ddx(alpha)) + abs(ddy(alpha));
                float sharpened = saturate((clipValue - width * 0.5) / max(width, 9.99999975e-05) + 1.0);
                if (_AlphaToMaskAvailable != 0.0)
                    clipValue = sharpened - 9.99999975e-05;
                clip(clipValue);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // The raw interpolated basis, as the original transforms the lighting normal before normalising.
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                inputData.tangentToWorld = float3x3(input.tangentWS.xyz, tangentSign * cross(input.normalWS, input.tangentWS.xyz), input.normalWS);
                float3 normalTS = EyeNormalTS(uv);
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

                // Dielectric (metallic 0) with the graph's smoothness, saturated.
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = EyeAlbedo(colour, input.color);
                surfaceData.metallic = 0;
                surfaceData.smoothness = saturate(_Smoothness);
                surfaceData.normalTS = normalTS;
                surfaceData.emission = EyeEmission(colour, erosion);
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

            // The depth passes clip the bare alpha at 0.5.
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(0.5 - Erosion(input.uv.xy));
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
                clip(0.5 - Erosion(input.uv.xy));
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, tangentSign * cross(input.normalWS, input.tangentWS.xyz), input.normalWS);
                outNormalWS = float4(NormalizeNormalPerPixel(TransformTangentToWorld(EyeNormalTS(input.uv.xy), tangentToWorld)), 0.0);
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
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                #if defined(EDITOR_VISUALIZATION)
                float2 vizUV : TEXCOORD1;
                float4 lightCoord : TEXCOORD2;
                #endif
                float4 color : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
                output.uv = input.uv0;
                output.color = input.color;
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(input.positionOS.xyz, input.uv0.xy, input.uv1, input.uv2, output.vizUV, output.lightCoord);
                #endif
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float erosion = Erosion(input.uv.xy);
                clip(0.5 - erosion);
                float3 colour = EyeColour(input.uv.xy);
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = EyeAlbedo(colour, input.color);
                metaInput.Emission = EyeEmission(colour, erosion);
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
