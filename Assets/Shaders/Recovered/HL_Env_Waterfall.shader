// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Waterfall, a URP 14 Lit Shader Graph (platform 14), transparent
// with alpha blending and no received shadows. A colour-variation noise and the refracted scene colour make the
// water colour, foam is stepped over it, the graph's own fog node fades it toward the fog colour for the albedo
// (the unfogged colour is the emission), a scrolling ripple normal map shades it, and a texture displaces the
// vertices along their normals. No shipped program enables _USE_DETAIL_MASK_MAP, so that keyword keeps the base
// body, and the forward output has no fog mix beyond the graph's own.
Shader "HardLight/Environment/HL_Env_Waterfall"
{
    Properties
    {
        _Documentation_Link_Below ("## Documentation Link Below : HL_Env_Waterfall", Float) = 0
        _Documentation_Link ("[https://hardlight.atlassian.net/wiki/spaces/HL/pages/8326408/SDT+-+HL+Env+Waterfall](https://hardlight.atlassian.net/wiki/spaces/HL/pages/8326408/SDT+-+HL+Env+Waterfall)", Float) = 0
        _Separator_0 ("#", Float) = 0
        _Title_Surface_Input ("## Surface Input", Float) = 0
        _Overall_Alpha ("-- Overall Alpha", Range(0, 1)) = 1
        _Smoothness ("-- Smoothness", Range(0, 1)) = 1
        _Emission_Boost ("-- Emission Boost", Range(0, 2)) = 1
        _Separator_1 ("# (1)", Float) = 0
        _Title_Fog ("### Fog", Float) = 0
        _Scene_Fog_Intensity ("-- Scene Fog Intensity", Range(0, 1)) = 0
        _Title_Colour ("## Colour", Float) = 0
        _Colour_Variation_Noise_UV_Tiling ("-- Colour Variation Noise UV Tiling", Vector) = (0.5,0.5,0,0)
        _Main_Colour ("-- Main Colour", Color) = (0.5628783,0.7980029,0.8584906,0)
        _Color_Var_1 ("-- Color Var 1", Color) = (0,0.947295,1,0)
        _Color_Var_2 ("-- Color Var 2", Color) = (0,0.5223284,1,0)
        _Separator_2 ("# (2)", Float) = 0
        _Title_Vertex_Displacement ("## Vertex Displacement", Float) = 0
        _Vertex_Displacement_Texture ("-- Vertex Displacement Texture", 2D) = "white" {}
        _Vertex_Displacement_Speed_Direction ("-- Vertex Displacement Speed/Direction", Vector) = (0.2,0.2,0,0)
        _Vertex_Displacement_Amount ("-- Vertex Displacement Amount", Float) = 0.05
        _Separator_3 ("# (3)", Float) = 0
        _Title_Ripples ("## Ripples", Float) = 0
        [Normal]_Ripple_Texture_Normal ("-- Ripple Texture (Normal)", 2D) = "bump" {}
        _Normal_Intensity ("-- Normal Intensity", Range(0, 2)) = 1
        _Ripple_Speed_Direction ("-- Ripple Speed/Direction", Vector) = (0.1,0.55,0,0)
        _Refraction_Intensity ("-- Refraction Intensity", Range(0, 0.05)) = 0.01
        _Separator_4 ("# (4)", Float) = 0
        _Title_Foam ("## Foam", Float) = 0
        _Foam_Texture ("-- Foam Texture", 2D) = "white" {}
        _Foam_Speed_Direction ("-- Foam Speed/Direction", Vector) = (0,1,0,0)
        _Foam_Amount ("-- Foam Amount", Float) = 0
        _Foam_Color ("-- Foam Color", Color) = (0,0,0,0)
        _Foam_Opacity ("-- Foam Opacity", Range(0, 1)) = 1
        _Separator_5 ("# (5)", Float) = 0
        _Title_Detail_Mask ("## Detail Mask", Float) = 0
        [Toggle(_USE_DETAIL_MASK_MAP)]_USE_DETAIL_MASK_MAP ("Use Detail Mask Map?", Float) = 0
        _Detail_Mask_Texture ("-- Detail Mask Texture [_USE_DETAIL_MASK_MAP]", 2D) = "white" {}
        _Detail_Mask_Speed_Direction ("-- Detail Mask Speed/Direction [_USE_DETAIL_MASK_MAP]", Vector) = (0,1,0,0)
        _Detail_Mask_Color ("-- Detail Mask Color [_USE_DETAIL_MASK_MAP]", Color) = (0,0,0,0)
        _Detail_Mask_Opacity ("-- Detail Mask Opacity [_USE_DETAIL_MASK_MAP]", Range(0, 1)) = 1
        _Separator_6 ("# (6)", Float) = 0
        [NoScaleOffset][NonModifiableTextureData]_Texture2DAsset_56213f5e12504974b7f5a49f56756455_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
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
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float4 _Texture2DAsset_56213f5e12504974b7f5a49f56756455_Out_0_Texture2D_TexelSize;
            float _Documentation_Link;
            float _Separator_0;
            float _Separator_3;
            float _Separator_4;
            float _Separator_5;
            float _Separator_6;
            float _Title_Fog;
            float _Scene_Fog_Intensity;
            float _Title_Ripples;
            float _Separator_2;
            float _Separator_1;
            float _Title_Foam;
            float _Title_Detail_Mask;
            float _Title_Surface_Input;
            float _Title_Vertex_Displacement;
            float _Title_Colour;
            float2 _Detail_Mask_Speed_Direction;
            float4 _Detail_Mask_Texture_TexelSize;
            float4 _Detail_Mask_Texture_ST;
            float _Vertex_Displacement_Amount;
            float _Foam_Opacity;
            float4 _Detail_Mask_Color;
            float4 _Main_Colour;
            float _Normal_Intensity;
            float _Smoothness;
            float _Refraction_Intensity;
            float2 _Ripple_Speed_Direction;
            float _Overall_Alpha;
            float4 _Ripple_Texture_Normal_TexelSize;
            float4 _Ripple_Texture_Normal_ST;
            float4 _Color_Var_2;
            float4 _Color_Var_1;
            float _Emission_Boost;
            float _Foam_Amount;
            float2 _Foam_Speed_Direction;
            float4 _Foam_Color;
            float2 _Colour_Variation_Noise_UV_Tiling;
            float _Detail_Mask_Opacity;
            float4 _Vertex_Displacement_Texture_TexelSize;
            float4 _Vertex_Displacement_Texture_ST;
            float2 _Vertex_Displacement_Speed_Direction;
            float4 _Foam_Texture_TexelSize;
            float4 _Foam_Texture_ST;
            float _Documentation_Link_Below;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;

        TEXTURE2D(_Vertex_Displacement_Texture);
        SAMPLER(sampler_Vertex_Displacement_Texture);
        TEXTURE2D(_Texture2DAsset_56213f5e12504974b7f5a49f56756455_Out_0_Texture2D);
        TEXTURE2D(_Ripple_Texture_Normal);
        TEXTURE2D(_Foam_Texture);
        // The graph's inline sampler: linear, repeat.
        SAMPLER(SamplerState_Linear_Repeat);

        // The displacement texture (at UV1, scrolled by game time) pushes the vertex along its object normal,
        // held back by the vertex colour's blue.
        float3 DisplacedPositionOS(float3 positionOS, float3 normalOS, float2 uv1, float4 color)
        {
            float2 uv = uv1 * _Vertex_Displacement_Texture_ST.xy + (_HLShaderTimeGame * _Vertex_Displacement_Speed_Direction + _Vertex_Displacement_Texture_ST.zw);
            float3 displacement = SAMPLE_TEXTURE2D_LOD(_Vertex_Displacement_Texture, sampler_Vertex_Displacement_Texture, uv, 0).xyz;
            return positionOS + (1.0 - color.b) * (displacement * _Vertex_Displacement_Amount * normalOS);
        }

        // The ripple normal map (AG or RG packed), scrolled by game time, before the normal strength.
        float3 RippleNormal(float2 uv)
        {
            float2 rippleUV = uv * _Ripple_Texture_Normal_ST.xy + (_HLShaderTimeGame * _Ripple_Speed_Direction + _Ripple_Texture_Normal_ST.zw);
            return UnpackNormalmapRGorAG(SAMPLE_TEXTURE2D(_Ripple_Texture_Normal, SamplerState_Linear_Repeat, rippleUV));
        }

        // Normal Strength: xy scaled, z lerped from 1 by the saturated strength.
        float3 RippleNormalTS(float2 uv)
        {
            float3 normal = RippleNormal(uv);
            return float3(normal.xy * _Normal_Intensity, lerp(1.0, normal.z, saturate(_Normal_Intensity)));
        }

        // The graph's screen position: the pixel over the scaled screen size, flipped as the target requires.
        float2 ScreenUV(float4 positionCS)
        {
            float y = _ProjectionParams.x < 0.0 ? _ScaledScreenParams.y - positionCS.y : positionCS.y;
            float2 screenUV = float2(positionCS.x, y) / _ScaledScreenParams.xy;
            #if UNITY_UV_STARTS_AT_TOP
            screenUV.y = 1.0 - screenUV.y;
            #endif
            return screenUV;
        }

        // The water colour before fog: the colour-variation noise (scrolled 0.5 per second of game time along v, doubled
        // and unclamped) between the two variation colours, times the scene colour refracted by the unscaled ripple
        // normal plus the main colour; then the stepped foam (foam texture red plus vertex red at or past the foam
        // amount, times its opacity) lerps toward the foam colour.
        float3 WaterColour(float2 uv, float4 color, float4 positionCS)
        {
            float2 noiseUV = uv * _Colour_Variation_Noise_UV_Tiling + _HLShaderTimeGame * float2(0.0, 0.5);
            float3 noise = SAMPLE_TEXTURE2D(_Texture2DAsset_56213f5e12504974b7f5a49f56756455_Out_0_Texture2D, SamplerState_Linear_Repeat, noiseUV).rgb;
            float3 variation = lerp(_Color_Var_1.rgb, _Color_Var_2.rgb, noise * 2.0);
            float3 scene = SampleSceneColor(ScreenUV(positionCS) - RippleNormal(uv).xy * _Refraction_Intensity);
            float3 colour = variation * (scene + _Main_Colour.rgb);

            float2 foamUV = uv * _Foam_Texture_ST.xy + (_HLShaderTimeGame * _Foam_Speed_Direction + _Foam_Texture_ST.zw);
            float foam = SAMPLE_TEXTURE2D(_Foam_Texture, SamplerState_Linear_Repeat, foamUV).r + color.r >= _Foam_Amount ? 1.0 : 0.0;
            foam *= _Foam_Opacity;
            return lerp(colour, foam * _Foam_Color.rgb, saturate(foam));
        }

        // The graph's Fog node density at this point (0 without a fog mode).
        float FogDensity(float3 positionWS)
        {
            #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
            float viewZ = -TransformWorldToView(TransformObjectToWorld(TransformWorldToObject(positionWS))).z;
            return 1.0 - ComputeFogIntensity(ComputeFogFactorZ0ToFar(max(viewZ - _ProjectionParams.y, 0.0)));
            #else
            return 0.0;
            #endif
        }

        // The albedo fades the water colour toward the fog colour by the fog density, less the scene fog intensity.
        float3 FoggedAlbedo(float3 colour, float3 positionWS)
        {
            return lerp(unity_FogColor.rgb, colour, saturate(1.0 - FogDensity(positionWS) + _Scene_Fog_Intensity));
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
            // The ShaderGraph Lit target computes fog per fragment from view depth; this graph receives no shadows.
            #define _FOG_FRAGMENT 1
            #define _RECEIVE_SHADOWS_OFF 1
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
            #pragma shader_feature_local _ _USE_DETAIL_MASK_MAP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;     // the displacement UV and the static lightmap UV
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
                VertexPositionInputs vertexInput = GetVertexPositionInputs(DisplacedPositionOS(input.positionOS.xyz, input.normalOS, input.uv1.xy, input.color));
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
                OUTPUT_LIGHTMAP_UV(input.uv1.xy, unity_LightmapST, output.staticLightmapUV);
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
                float3 colour = WaterColour(uv, input.color, input.positionCS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // The raw interpolated basis, as the original transforms the lighting normal before normalising.
                float tangentSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                inputData.tangentToWorld = float3x3(input.tangentWS.xyz, tangentSign * cross(input.normalWS, input.tangentWS.xyz), input.normalWS);
                float3 normalTS = RippleNormalTS(uv);
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

                // Dielectric (metallic 0) with the saturated smoothness. The graph dots two broadcast vectors for
                // the alpha, doubling the product of the overall alpha and one minus the vertex green.
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = FoggedAlbedo(colour, input.positionWS);
                surfaceData.metallic = 0;
                surfaceData.smoothness = saturate(_Smoothness);
                surfaceData.normalTS = normalTS;
                surfaceData.emission = colour * _Emission_Boost;
                surfaceData.occlusion = 1;
                surfaceData.alpha = saturate(2.0 * _Overall_Alpha * (1.0 - input.color.g));
                #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                outColor = UniversalFragmentPBR(inputData, surfaceData);
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
            #pragma shader_feature_local _ _USE_DETAIL_MASK_MAP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 uv1 : TEXCOORD1; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : TEXCOORD0;
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
                float3 positionWS = TransformObjectToWorld(DisplacedPositionOS(input.positionOS.xyz, input.normalOS, input.uv1.xy, input.color));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
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
            #pragma shader_feature_local _ _USE_DETAIL_MASK_MAP

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 uv1 : TEXCOORD1; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(DisplacedPositionOS(input.positionOS.xyz, input.normalOS, input.uv1.xy, input.color));
                output.color = input.color;
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
            #pragma shader_feature_local _ _USE_DETAIL_MASK_MAP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float4 uv0 : TEXCOORD0; float4 uv1 : TEXCOORD1; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
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
                output.positionCS = TransformObjectToHClip(DisplacedPositionOS(input.positionOS.xyz, input.normalOS, input.uv1.xy, input.color));
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
                outNormalWS = float4(NormalizeNormalPerPixel(TransformTangentToWorld(RippleNormalTS(input.uv.xy), tangentToWorld)), 0.0);
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
            #pragma shader_feature_local _ _USE_DETAIL_MASK_MAP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
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
                float3 positionWS : TEXCOORD4;
                #if defined(EDITOR_VISUALIZATION)
                float2 vizUV : TEXCOORD5;
                float4 lightCoord : TEXCOORD6;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionOS = DisplacedPositionOS(input.positionOS.xyz, input.normalOS, input.uv1.xy, input.color);
                output.positionCS = UnityMetaVertexPosition(positionOS, input.uv1.xy, input.uv2.xy);
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                output.color = input.color;
                output.positionWS = TransformObjectToWorld(positionOS);
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(positionOS, input.uv0.xy, input.uv1.xy, input.uv2.xy, output.vizUV, output.lightCoord);
                #endif
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 colour = WaterColour(input.uv0.xy, input.color, input.positionCS);
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = FoggedAlbedo(colour, input.positionWS);
                metaInput.Emission = colour * _Emission_Boost;
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
