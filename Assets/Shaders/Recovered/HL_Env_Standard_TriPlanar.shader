// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Standard_TriPlanar, a URP 14 Lit Shader Graph
// (platform 14), opaque. The graph's triplanar surface inputs are rebuilt from the Metal programs;
// lighting, GI, shadows, fog and depth/meta output use URP 14.0.11's own library functions, as the
// graph's generated passes do.
Shader "HardLight/Environment/HL_Env_Standard_TriPlanar"
{
    Properties
    {
        _Documentation_Link_Below ("## Documentation Link Below : HL_Env_Standard_TriPlanar", Float) = 0
        _Documentation_Link ("[https://hardlight.atlassian.net/wiki/spaces/HL/pages/8335977/SDT+-+HL+Env+Standard+TriPlanar](https://hardlight.atlassian.net/wiki/spaces/HL/pages/8335977/SDT+-+HL+Env+Standard+TriPlanar)", Float) = 0
        _Separator_0 ("#", Float) = 0
        _Title_Tri_Planar ("## Tri-Planar", Float) = 0
        _Tri_Planar_Blend ("-- Tri Planar Blend", Float) = 2
        _Tri_Planar_UV_Tiling ("-- Tri Planar UV Tiling", Float) = 1
        _Separator_1 ("# (1)", Float) = 0
        _Title_Diffuse ("## Diffuse", Float) = 0
        [NoScaleOffset][MainTexture]_Diff_Tex ("-- Diff Texture", 2D) = "white" {}
        _Diff_Colour_Tint ("-- Diffuse Colour Tint", Color) = (1,1,1,0)
        [ToggleUI]_USE_VERTEX_COLOUR_TINT ("-- Use Vertex Colour Tint?", Float) = 0
        _Separator_2 ("# (2)", Float) = 0
        _Title_MSE ("## MS", Float) = 0
        _NOTE_R_Metal_G_Smoothness_B_Emission_Optional ("!NOTE R = Metal, G = Smoothness", Float) = 0
        [NoScaleOffset]_MSEA_Tex ("-- MS Texture", 2D) = "white" {}
        _Metal_Intensity ("-- Metal Intensity", Range(0, 2)) = 0
        _Smoothness_Intensity ("-- Smoothness Intensity", Range(0, 2)) = 0.5
        _Separator_3 ("# (3)", Float) = 0
        _Title_Normal ("## Normal", Float) = 0
        [NoScaleOffset][Normal]_Normal_Tex ("-- Norm Texture", 2D) = "bump" {}
        _Separator_4 ("# (4)", Float) = 0
        _Title_Emission ("## Emission", Float) = 0
        _REF_EMISSION_TYPE ("!REF _USE_EMISSION", Float) = 0
        [Toggle(_USE_EMISSION)]_USE_EMISSION ("Use Emission?", Float) = 0
        [NoScaleOffset]_Emissive_Tex ("-- Emission Tex [_USE_EMISSION]", 2D) = "white" {}
        [HDR]_Emissive_Colour ("-- Emissive Colour [_USE_EMISSION]", Color) = (0,0,0,0)
        _Separator_5 ("# (5)", Float) = 0
        _Title_Detail_Maps ("## Detail Maps", Float) = 0
        _REF_USE_TRIPLANAR_DETAIL ("!REF _USE_TRIPLANAR_DETAIL", Float) = 0
        [Toggle(_USE_TRIPLANAR_DETAIL)]_USE_TRIPLANAR_DETAIL ("Use Triplanar Detail?", Float) = 0
        [NoScaleOffset]_Detail_Diff_Tex ("-- Detail Diffuse Tex [_USE_TRIPLANAR_DETAIL]", 2D) = "white" {}
        _Detail_Diff_Tri_Planar_UV_Tiling ("-- Detail Tri Planar UV Tiling [_USE_TRIPLANAR_DETAIL]", Float) = 1
        _Detail_Diff_Tri_Planar_Blend ("-- Detail Tri Planar Blend [_USE_TRIPLANAR_DETAIL]", Float) = 100
        _Detail_Diff_Colour_Tint ("-- Detail Diff Colour Tint[_USE_TRIPLANAR_DETAIL]", Color) = (1,1,1,0)
        _Detail_Diff_Opacity ("-- Detail Opacity [_USE_TRIPLANAR_DETAIL]", Range(0, 1)) = 1
        _Separator_6 ("# (6)", Float) = 0
        _Title_Fresnel ("## Fresnel", Float) = 0
        _REF_USE_FRESNEL ("!REF _USE_FRESNEL", Float) = 0
        [Toggle(_USE_FRESNEL)]_USE_FRESNEL ("Use Fresnel?", Float) = 0
        _Fresnel_Colour ("-- Fresnel Colour [_USE_FRESNEL]", Color) = (1,1,1,0)
        _Fresnel_Amount ("-- Fresnel Amount [_USE_FRESNEL]", Range(0, 20)) = 5
        _Fresnel_Intensity ("-- Fresnel Intensity [_USE_FRESNEL]", Range(1, 20)) = 1
        _Fresnel_Opacity ("-- Fresnel Opacity [_USE_FRESNEL]", Range(0, 1)) = 1
        _Separator_7 ("# (7)", Float) = 0
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
            float _Documentation_Link;
            float _Separator_0;
            float _Documentation_Link_Below;
            float _Title_Tri_Planar;
            float _Tri_Planar_Blend;
            float4 _MSEA_Tex_TexelSize;
            float _Title_Diffuse;
            float _Separator_1;
            float _Detail_Diff_Tri_Planar_Blend;
            float _Fresnel_Opacity;
            float _Detail_Diff_Tri_Planar_UV_Tiling;
            float _Detail_Diff_Opacity;
            float _NOTE_R_Metal_G_Smoothness_B_Emission_Optional;
            float _Title_MSE;
            float _Separator_2;
            float _Fresnel_Intensity;
            float4 _Normal_Tex_TexelSize;
            float4 _Diff_Tex_TexelSize;
            float _Title_Normal;
            float _Separator_3;
            float _Smoothness_Intensity;
            float _Metal_Intensity;
            float _Title_Emission;
            float _REF_EMISSION_TYPE;
            float _Separator_4;
            float4 _Diff_Colour_Tint;
            float4 _Fresnel_Colour;
            float _Fresnel_Amount;
            float _Title_Detail_Maps;
            float _REF_USE_TRIPLANAR_DETAIL;
            float _Separator_5;
            float4 _Detail_Diff_Tex_TexelSize;
            float4 _Detail_Diff_Colour_Tint;
            float _Tri_Planar_UV_Tiling;
            float4 _Emissive_Colour;
            float4 _Emissive_Tex_TexelSize;
            float _Title_Fresnel;
            float _REF_USE_FRESNEL;
            float _Separator_6;
            float _Separator_7;
            float _USE_VERTEX_COLOUR_TINT;
        CBUFFER_END

        TEXTURE2D(_Diff_Tex);
        TEXTURE2D(_MSEA_Tex);
        TEXTURE2D(_Normal_Tex);
        TEXTURE2D(_Emissive_Tex);
        TEXTURE2D(_Detail_Diff_Tex);
        // The graph's inline sampler: linear, repeat.
        SAMPLER(SamplerState_Linear_Repeat);
        #define TRI_SAMPLE(tex, uv) SAMPLE_TEXTURE2D(tex, SamplerState_Linear_Repeat, uv)

        // The graph's Triplanar node: world position times tiling / 10, projected along each axis
        // (X: zy, Y: xz, Z: xy), weighted by |normal|^blend normalised to sum to 1.
        struct TriPlanar
        {
            float3 uv;
            float3 weights;
        };

        TriPlanar TriPlanarCoords(float3 positionWS, float3 normalWS, float tiling, float blend)
        {
            TriPlanar coords;
            coords.uv = positionWS * (tiling * 0.1);
            float3 weights = pow(abs(normalWS), blend);
            coords.weights = weights / dot(weights, 1.0);
            return coords;
        }

        #define TRI_PLANAR(tex, c) (c.weights.x * TRI_SAMPLE(tex, c.uv.zy) + c.weights.y * TRI_SAMPLE(tex, c.uv.xz) + c.weights.z * TRI_SAMPLE(tex, c.uv.xy))

        // Albedo: the triplanar diffuse, pulled to the detail tint by the triplanar detail texture (with its
        // own tiling and blend) times vertex alpha and opacity, then tinted (and vertex-colour tinted when enabled), then pulled toward
        // the fresnel colour at grazing angles when fresnel is enabled.
        float3 TriAlbedo(float3 positionWS, float3 normalWS, float3 viewDirWS, float4 vertexColor)
        {
            TriPlanar coords = TriPlanarCoords(positionWS, normalWS, _Tri_Planar_UV_Tiling, _Tri_Planar_Blend);
            float3 albedo = TRI_PLANAR(_Diff_Tex, coords).rgb;
            #if defined(_USE_TRIPLANAR_DETAIL)
            TriPlanar detail = TriPlanarCoords(positionWS, normalWS, _Detail_Diff_Tri_Planar_UV_Tiling, _Detail_Diff_Tri_Planar_Blend);
            float3 detailMask = TRI_PLANAR(_Detail_Diff_Tex, detail).rgb * vertexColor.a * _Detail_Diff_Opacity;
            albedo = lerp(albedo, _Detail_Diff_Colour_Tint.rgb, detailMask);
            #endif
            albedo = _USE_VERTEX_COLOUR_TINT != 0.0 ? albedo * vertexColor.rgb * _Diff_Colour_Tint.rgb : albedo * _Diff_Colour_Tint.rgb;
            #if defined(_USE_FRESNEL)
            float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _Fresnel_Amount) * _Fresnel_Intensity;
            albedo = lerp(_Fresnel_Colour.rgb, albedo, saturate(1.0 - fresnel * _Fresnel_Opacity));
            #endif
            return albedo;
        }

        float3 TriEmission(float3 positionWS, float3 normalWS)
        {
            #if defined(_USE_EMISSION)
            TriPlanar coords = TriPlanarCoords(positionWS, normalWS, _Tri_Planar_UV_Tiling, _Tri_Planar_Blend);
            return TRI_PLANAR(_Emissive_Tex, coords).rgb * _Emissive_Colour.rgb;
            #else
            return 0;
            #endif
        }

        // Metal and smoothness: the triplanar MS texture's red and green times their intensities.
        float2 TriMetalSmooth(float3 positionWS, float3 normalWS)
        {
            TriPlanar coords = TriPlanarCoords(positionWS, normalWS, _Tri_Planar_UV_Tiling, _Tri_Planar_Blend);
            return saturate(TRI_PLANAR(_MSEA_Tex, coords).rg * float2(_Metal_Intensity, _Smoothness_Intensity));
        }

        // The graph's Normal-type Triplanar node: each projection's unpacked xy added to the matching
        // world-normal components, swizzled back to world axes, blended, then brought into tangent space.
        float3 TriNormalTS(float3 positionWS, float3 normalWS, float3x3 tangentToWorld)
        {
            TriPlanar coords = TriPlanarCoords(positionWS, normalWS, _Tri_Planar_UV_Tiling, _Tri_Planar_Blend);
            float2 x = UnpackNormal(TRI_SAMPLE(_Normal_Tex, coords.uv.zy)).xy;
            float2 y = UnpackNormal(TRI_SAMPLE(_Normal_Tex, coords.uv.xz)).xy;
            float2 z = UnpackNormal(TRI_SAMPLE(_Normal_Tex, coords.uv.xy)).xy;
            float3 blended = coords.weights.x * float3(normalWS.x, x.y + normalWS.y, x.x + normalWS.z)
                           + coords.weights.y * float3(y.x + normalWS.x, normalWS.y, y.y + normalWS.z)
                           + coords.weights.z * float3(z.x + normalWS.x, z.y + normalWS.y, normalWS.z);
            return TransformWorldToTangent(blended, tangentToWorld);
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
            #pragma shader_feature_local _ _USE_FRESNEL
            #pragma shader_feature_local _ _USE_TRIPLANAR_DETAIL
            #pragma shader_feature_local _ _USE_EMISSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
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
                float4 color : TEXCOORD3;
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
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
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

                // The graph works with the interpolated normal, tangent and bitangent all scaled by
                // 1 / |normal|, which leaves the tangent-space result unchanged.
                float3 normalWS = normalize(input.normalWS);
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                inputData.tangentToWorld = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 normalTS = TriNormalTS(input.positionWS, normalWS, inputData.tangentToWorld);
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

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = TriAlbedo(input.positionWS, normalWS, inputData.viewDirectionWS, input.color);
                float2 metalSmooth = TriMetalSmooth(input.positionWS, normalWS);
                surfaceData.metallic = metalSmooth.x;
                surfaceData.smoothness = metalSmooth.y;
                surfaceData.normalTS = normalTS;
                surfaceData.emission = TriEmission(input.positionWS, normalWS);
                surfaceData.occlusion = 1;
                surfaceData.alpha = 1;
                #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = OutputAlpha(color.a, false);
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
            #pragma shader_feature_local _ _USE_FRESNEL
            #pragma shader_feature_local _ _USE_TRIPLANAR_DETAIL
            #pragma shader_feature_local _ _USE_EMISSION
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
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
                return positionCS;
            }
            float4 Frag() : SV_Target { return 0; }
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
            #pragma shader_feature_local _ _USE_FRESNEL
            #pragma shader_feature_local _ _USE_TRIPLANAR_DETAIL
            #pragma shader_feature_local _ _USE_EMISSION
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }
            float4 Frag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.zzzz; }
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
            #pragma shader_feature_local _ _USE_FRESNEL
            #pragma shader_feature_local _ _USE_TRIPLANAR_DETAIL
            #pragma shader_feature_local _ _USE_EMISSION
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
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
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
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
                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 normalTS = TriNormalTS(input.positionWS, normalize(input.normalWS), tangentToWorld);
                outNormalWS = float4(NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld)), 0.0);
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
            #pragma shader_feature_local _ _USE_FRESNEL
            #pragma shader_feature_local _ _USE_TRIPLANAR_DETAIL
            #pragma shader_feature_local _ _USE_EMISSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                #if defined(EDITOR_VISUALIZATION)
                float2 vizUV : TEXCOORD3;
                float4 lightCoord : TEXCOORD4;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(input.positionOS.xyz, input.uv0, input.uv1, input.uv2, output.vizUV, output.lightCoord);
                #endif
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = TriAlbedo(input.positionWS, normalWS, GetWorldSpaceNormalizeViewDir(input.positionWS), input.color);
                metaInput.Emission = TriEmission(input.positionWS, normalWS);
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
