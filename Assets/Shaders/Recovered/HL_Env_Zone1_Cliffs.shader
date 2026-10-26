// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Zone1_Cliffs, a URP 14 Lit Shader Graph (platform 14), opaque.
// A triplanar main layer, an edge layer masked from UV0 (with a shadow under it) and a planar top layer; lighting,
// GI, shadows, fog and depth/meta output use URP 14.0.11's own library functions, as the graph's generated passes do.
Shader "HardLight/Environment/HL_Env_Zone1_Cliffs"
{
    Properties
    {
        _Documentation_Link_Below ("## Documentation Link Below : HL_Env_Zone1_Cliffs", Float) = 0
        _Documentation_Link ("[https://hardlight.atlassian.net/wiki/spaces/HL/pages/8335199/SDT+-+HL+Env+Zone1+Cliffs](https://hardlight.atlassian.net/wiki/spaces/HL/pages/8335199/SDT+-+HL+Env+Zone1+Cliffs)", Float) = 0
        _Separator_0 ("#", Float) = 0
        _Title_Top ("## Top", Float) = 0
        [NoScaleOffset]_Top_DiffA_Tex ("-- Top DiffA Texture", 2D) = "white" {}
        _Top_UV_Tiling ("-- Top UV Tiling", Float) = 1
        _Top_Smoothness_Intensity ("-- Top Smoothness Intensity", Range(0, 2)) = 1
        _NOTE_Top_Normal ("!NOTE Top Normal", Float) = 0
        [NoScaleOffset][Normal]_Top_Normal_Tex ("-- Top Normal Texture", 2D) = "bump" {}
        _Separator_1 ("# (1)", Float) = 0
        _Title_Main ("## Main", Float) = 0
        _NOTE_Surface_Inputs ("!NOTE Surface Inputs", Float) = 0
        _Main_Tri_Blend_Amount ("-- Main Tri Blend Amount", Range(0, 10)) = 1
        _Main_Diff_Colour_Tint ("-- Main Colour Tint", Color) = (1,1,1,0)
        _Main_Smoothness_Intensity ("-- Main Smoothness Intensity", Range(0, 2)) = 1
        _NOTE_Main_Diffuse ("!NOTE Main Diffuse", Float) = 0
        [NoScaleOffset]_Main_DiffA_Tex ("--Main DiffA Texture", 2D) = "white" {}
        _Main_UV_Tiling ("-- Main Diffuse UV Tiling", Float) = 1
        _NOTE_Main_Normal ("!NOTE Main Normal", Float) = 0
        [NoScaleOffset][Normal]_Main_Normal_Tex ("-- Main Normal Texture", 2D) = "bump" {}
        _Main_Normal_UV_Tiling ("-- Main Normal UV Tiling", Float) = 0.1
        _Separator_2 ("# (2)", Float) = 0
        _Title_Edge ("## Edge", Float) = 0
        _NOTE_RGB_Diff_A_Mask ("!NOTE RGB = Diff, A = Mask", Float) = 0
        [NoScaleOffset]_Edge_DiffA_Tex ("-- Edge DiffA Texture", 2D) = "white" {}
        _Top_Edge_Blend_Amount ("-- Top+Edge Blend Amount", Float) = 0
        _Edge_DiffA_Alpha_Step_Amount ("-- Edge Alpha Step Amount", Range(0, 1)) = 0.5
        _Edge_Smoothness_Intensity ("-- Edge Smoothness Intensity", Range(0, 1)) = 0.5
        _NOTE_Edge_Normal ("!NOTE Edge Normal", Float) = 0
        [NoScaleOffset][Normal]_Edge_Normal_Tex ("-- Edge Normal Texture", 2D) = "bump" {}
        _NOTE_Edge_Shadow ("!NOTE Edge Shadow", Float) = 0
        _Edge_Shadow_Opacity ("-- Edge Shadow Opacity", Range(0, 1)) = 0
        _Separator_3 ("# (3)", Float) = 0
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
            float _Title_Top;
            float _Separator_1;
            float _Main_Smoothness_Intensity;
            float4 _Edge_Normal_Tex_TexelSize;
            float4 _Edge_DiffA_Tex_TexelSize;
            float4 _Top_Normal_Tex_TexelSize;
            float _NOTE_Top_Normal;
            float4 _Top_DiffA_Tex_TexelSize;
            float _NOTE_Surface_Inputs;
            float _NOTE_Main_Diffuse;
            float _Title_Main;
            float4 _Main_Normal_Tex_TexelSize;
            float4 _Main_DiffA_Tex_TexelSize;
            float4 _Main_Diff_Colour_Tint;
            float _Top_UV_Tiling;
            float _Main_UV_Tiling;
            float _NOTE_Main_Normal;
            float _Main_Normal_UV_Tiling;
            float _Separator_2;
            float _Title_Edge;
            float _Main_Tri_Blend_Amount;
            float _Top_Smoothness_Intensity;
            float _Edge_DiffA_Alpha_Step_Amount;
            float _NOTE_Edge_Normal;
            float _Edge_Shadow_Opacity;
            float _Edge_Smoothness_Intensity;
            float _NOTE_Edge_Shadow;
            float _Separator_3;
            float _Top_Edge_Blend_Amount;
            float _Documentation_Link_Below;
            float _NOTE_RGB_Diff_A_Mask;
        CBUFFER_END

        TEXTURE2D(_Top_DiffA_Tex);
        TEXTURE2D(_Top_Normal_Tex);
        TEXTURE2D(_Main_DiffA_Tex);
        TEXTURE2D(_Main_Normal_Tex);
        TEXTURE2D(_Edge_DiffA_Tex);
        TEXTURE2D(_Edge_Normal_Tex);
        // The graph's inline sampler: trilinear, repeat.
        SAMPLER(SamplerState_Trilinear_Repeat);
        #define CLIFF_SAMPLE(tex, uv) SAMPLE_TEXTURE2D(tex, SamplerState_Trilinear_Repeat, uv)

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

        #define TRI_PLANAR(tex, c) (c.weights.x * CLIFF_SAMPLE(tex, c.uv.zy) + c.weights.y * CLIFF_SAMPLE(tex, c.uv.xz) + c.weights.z * CLIFF_SAMPLE(tex, c.uv.xy))

        // The layer masks, both from UV0: the edge where the edge texture's alpha reaches the step amount or the
        // UV's top 4% (v 0.96 to 0.9675), and the top where v plus the blend amount passes 0.1 (over a tenth).
        float CliffEdgeMask(float edgeAlpha, float2 uv)
        {
            return min((edgeAlpha >= _Edge_DiffA_Alpha_Step_Amount ? 1.0 : 0.0) + saturate((uv.y - 0.96) * 133.6), 1.0);
        }

        float CliffTopBlend(float2 uv)
        {
            return saturate((uv.y + _Top_Edge_Blend_Amount) * 10.0);
        }

        float2 CliffTopUV(float3 positionWS)
        {
            return positionWS.xz * (_Top_UV_Tiling * 0.1);
        }

        // Albedo: the tinted triplanar main diffuse darkened by the edge shadow (1 - edge alpha, at least the
        // shadow opacity), replaced by the edge diffuse under the edge mask, then the planar (XZ) top diffuse
        // under the top blend.
        float3 CliffAlbedo(float3 positionWS, float3 normalWS, float2 uv)
        {
            TriPlanar coords = TriPlanarCoords(positionWS, normalWS, _Main_UV_Tiling, _Main_Tri_Blend_Amount);
            float3 main = TRI_PLANAR(_Main_DiffA_Tex, coords).rgb * _Main_Diff_Colour_Tint.rgb;
            float4 edge = CLIFF_SAMPLE(_Edge_DiffA_Tex, uv);
            float shadow = max(1.0 - edge.a, _Edge_Shadow_Opacity);
            float3 sides = lerp(main * shadow, edge.rgb, CliffEdgeMask(edge.a, uv));
            float3 top = CLIFF_SAMPLE(_Top_DiffA_Tex, CliffTopUV(positionWS)).rgb;
            return lerp(top, sides, 1.0 - CliffTopBlend(uv));
        }

        // Smoothness: the main diffuse alpha times its intensity, and under the edge mask the edge intensity
        // pulled to the top diffuse alpha times its intensity by the top blend.
        float CliffSmoothness(float3 positionWS, float3 normalWS, float2 uv)
        {
            TriPlanar coords = TriPlanarCoords(positionWS, normalWS, _Main_UV_Tiling, _Main_Tri_Blend_Amount);
            float main = TRI_PLANAR(_Main_DiffA_Tex, coords).a * _Main_Smoothness_Intensity;
            float top = CLIFF_SAMPLE(_Top_DiffA_Tex, CliffTopUV(positionWS)).a * _Top_Smoothness_Intensity;
            float edgeAlpha = CLIFF_SAMPLE(_Edge_DiffA_Tex, uv).a;
            return saturate(lerp(main, lerp(top, _Edge_Smoothness_Intensity, 1.0 - CliffTopBlend(uv)), CliffEdgeMask(edgeAlpha, uv)));
        }

        // Normal: the graph's Normal-type Triplanar node (each projection's unpacked xy added to the matching
        // world-normal components, swizzled back to world axes, blended, brought into tangent space and
        // normalised), pulled to the edge normal map by the edge mask, then to the planar top normal map by the
        // top blend.
        // As in the original Forward and DepthNormals fragments: the bitangent sign is the strict sign of the
        // interpolated tangent w (w > 0 ? 1 : -1, so 0 is negative) times the world transform's sign, applied
        // separately so that w 0 under a negative scale is positive.
        float3 CliffBitangent(float3 normalWS, float4 tangentWS)
        {
            float bitangentSign = (tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
            return bitangentSign * cross(normalWS, tangentWS.xyz);
        }

        float3 CliffNormalTS(float3 positionWS, float3 normalWS, float3x3 tangentToWorld, float2 uv)
        {
            TriPlanar coords = TriPlanarCoords(positionWS, normalWS, _Main_Normal_UV_Tiling, _Main_Tri_Blend_Amount);
            float2 x = UnpackNormalmapRGorAG(CLIFF_SAMPLE(_Main_Normal_Tex, coords.uv.zy)).xy;
            float2 y = UnpackNormalmapRGorAG(CLIFF_SAMPLE(_Main_Normal_Tex, coords.uv.xz)).xy;
            float2 z = UnpackNormalmapRGorAG(CLIFF_SAMPLE(_Main_Normal_Tex, coords.uv.xy)).xy;
            float3 blended = coords.weights.x * float3(normalWS.x, x.y + normalWS.y, x.x + normalWS.z)
                           + coords.weights.y * float3(y.x + normalWS.x, normalWS.y, y.y + normalWS.z)
                           + coords.weights.z * float3(z.x + normalWS.x, z.y + normalWS.y, normalWS.z);
            float3 normalTS = SafeNormalize(TransformWorldToTangent(blended, tangentToWorld));
            float edgeAlpha = CLIFF_SAMPLE(_Edge_DiffA_Tex, uv).a;
            normalTS = lerp(normalTS, UnpackNormalmapRGorAG(CLIFF_SAMPLE(_Edge_Normal_Tex, uv)), CliffEdgeMask(edgeAlpha, uv));
            return lerp(normalTS, UnpackNormalmapRGorAG(CLIFF_SAMPLE(_Top_Normal_Tex, CliffTopUV(positionWS))), CliffTopBlend(uv));
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
                float2 staticLightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                float4 uv0 : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float4 uv0 : TEXCOORD3;
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
                // The raw tangent w is interpolated; the fragment takes its strict sign (see CliffBitangent).
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv0 = input.uv0;
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
                float3 bitangent = CliffBitangent(input.normalWS, input.tangentWS);
                inputData.tangentToWorld = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 normalTS = CliffNormalTS(input.positionWS, normalWS, inputData.tangentToWorld, input.uv0.xy);
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
                surfaceData.albedo = CliffAlbedo(input.positionWS, normalWS, input.uv0.xy);
                surfaceData.metallic = 0;
                surfaceData.smoothness = CliffSmoothness(input.positionWS, normalWS, input.uv0.xy);
                surfaceData.normalTS = normalTS;
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
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float4 uv0 : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.uv0 = input.uv0;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                // The raw tangent w is interpolated; the fragment takes its strict sign (see CliffBitangent).
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
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
                float3 bitangent = CliffBitangent(input.normalWS, input.tangentWS);
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 normalTS = CliffNormalTS(input.positionWS, normalize(input.normalWS), tangentToWorld, input.uv0.xy);
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv0 : TEXCOORD0;
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
                output.uv0 = input.uv0;
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(input.positionOS.xyz, input.uv0, input.uv1, input.uv2, output.vizUV, output.lightCoord);
                #endif
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = CliffAlbedo(input.positionWS, normalWS, input.uv0);
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
