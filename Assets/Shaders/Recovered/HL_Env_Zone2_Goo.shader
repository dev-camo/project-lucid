// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Environment/HL_Env_Zone2_Goo, a URP 14 Lit Shader Graph (platform 14),
// transparent with alpha clipping always on. The graph's surface and vertex displacement are rebuilt
// from the Metal programs; lighting, GI, shadows, fog and depth/meta output use URP 14.0.11's own
// library functions, as the graph's generated passes do.
Shader "HardLight/Environment/HL_Env_Zone2_Goo"
{
    Properties
    {
        _Separator_0 ("#", Float) = 0
        _Title_Vertex_Colour ("## Vertex Colour", Float) = 1
        _Title_Red ("### Red = (Optional:SineWave)", Float) = 1
        _Title_Green ("### Green = (Optional:SineWave)", Float) = 1
        _Title_Blue ("### Blue = (Optional:SineWave)", Float) = 1
        _Title_Alpha_Colour_Blend ("### Alpha = Colour Blend", Float) = 1
        _Separator_1 ("# (1)", Float) = 0
        _Title_Surface_Input ("## Surface Input", Float) = 0
        _Smoothness_Override ("-- Smoothness Override", Range(-1, 1)) = 0
        _Smoothness_Intensity ("-- Smoothness Intensity", Range(0, 1)) = 0.5
        _Title_Alpha ("### Alpha", Float) = 0
        [ToggleUI]_Use_Custom_Alpha_Value ("-- Use Custom Alpha Value?", Float) = 0
        _NOTE_Alpha ("!NOTE Alpha derives from Erosion/DepthFade/Global Alpha. [_Use_Custom_Alpha_Value]", Float) = 0
        _Alpha_Clip ("-- Alpha Clip [_Use_Custom_Alpha_Value]", Range(0, 1)) = 0.5
        _Global_Alpha ("-- Global Alpha [_Use_Custom_Alpha_Value]", Range(0, 1)) = 1
        _Separator_2 ("# (2)", Float) = 0
        _NOTE_Mask_Vertex_Alpha_Channel ("!NOTE Mask: Vertex Alpha Channel", Float) = 0
        _Color_A ("-- Color A", Color) = (0.9333334,0.3137255,0.05882353,1)
        _Color_B ("-- Color B", Color) = (1,0.6352941,0,1)
        _Color_C ("-- Color C", Color) = (0.7254902,0.1490196,0.8,1)
        _Color_D ("-- Color D", Color) = (0.4156863,0,1,1)
        [ToggleUI]_Use_Fresnel_Instead_of_Vertex_Mask ("-- Use Fresnel Instead of Vertex Mask?", Float) = 0
        _Fresnel_Amount ("-- Fresnel Amount [_Use_Fresnel_Instead_of_Vertex_Mask]", Float) = 1
        _Fresnel_Intensity ("-- Fresnel Intensity [_Use_Fresnel_Instead_of_Vertex_Mask]", Float) = 1
        _Separator_3 ("# (3)", Float) = 0
        _NOTE_Global_Scrolling_UVs ("!NOTE Global Scrolling UVs", Float) = 0
        _UV_Scrolling_Direction_X ("-- UV Scrolling/Direction X", Range(-1, 1)) = 0
        _UV_Scrolling_Direction_Y ("-- UV Scrolling/Direction Y", Range(-1, 1)) = 0
        _Separator_4 ("# (4)", Float) = 0
        _Title_Noise ("## Noise", Float) = 0
        _Goo_Noise_Tex ("-- Goo Noise Texture", 2D) = "white" {}
        _Goo_Noise_Contrast ("-- Goo Noise Contrast", Float) = 2
        _Separator_5 ("# (5)", Float) = 0
        _Title_FlowMap ("## FlowMap", Float) = 0
        _Flow_Map_Strength ("-- Flow Map Strength", Float) = 1
        [NoScaleOffset]_Flow_Map_Tex ("-- Flow Map Texture", 2D) = "linearGrey" {}
        _Flow_Map_Speed ("-- Flow Map Speed", Float) = 0.2
        _Separator_6 ("# (6)", Float) = 0
        _Title_Normal ("## Normal", Float) = 0
        [NoScaleOffset][Normal]_Normal_Tex ("-- Normal Texture", 2D) = "bump" {}
        _Normal_Intensity ("-- Normal Intensity", Range(0, 3)) = 1
        _Separator_7 ("# (7)", Float) = 0
        _Title_Depth_Fade ("## Depth Fade", Float) = 0
        _Depth_Fade_Distance ("-- Depth Fade Distance", Range(0, 100)) = 2
        _Depth_Fade_Contrast ("-- Depth Fade Contrast", Float) = 1
        _Title_Depth_Fade_1 ("### Depth Fade Colour", Float) = 0
        _Depth_Fade_Color_A ("-- Depth Fade Color A", Color) = (1,0,0,1)
        _Depth_Fade_Color_B ("-- Depth Fade Color B", Color) = (0.8784709,0,1,1)
        _Depth_Fade_Colour_Contrast ("-- Depth Fade Colour Contrast", Float) = 0.2
        _Separator_8 ("# (8)", Float) = 0
        _Title_Vertex_Displacement ("## Vertex Displacement", Float) = 0
        _Vertex_Displacement_Noise_Tex ("-- Vertex Displacement Noise Texture", 2D) = "white" {}
        _UV_Vertex_Displacement_Speed_Direction_X ("-- UV Speed/Direction X", Range(-1, 1)) = 0
        _UV_Vertex_Displacement_Speed_Direction_Y ("-- UV Speed/Direction Y", Range(-1, 1)) = 0
        _Vertex_Displacement_Amount ("-- Vertex Displacement Amount", Float) = 0
        _Title_Sine_Wave ("### Sine Wave", Float) = 0
        [ToggleUI]_Use_SineWave ("-- Use SineWave?", Float) = 0
        _SineWave_Speed ("-- SineWave Speed", Float) = 1
        _SineWave_Mask_Tiling ("-- SineWave Mask Tiling", Float) = 1
        _SineWave_Mask_Min ("-- SineWave Mask Min", Float) = 0.8
        _SineWave_Mask_Max ("-- SineWave Mask Max", Float) = 1
        _SineWave_Displacement_Amount ("-- SineWave Displacement Amount", Float) = 1
        _Separator_9 ("# (9)", Float) = 1
        _Title_Vertex_Displacement_1 ("## Erosion", Float) = 0
        [HDR]_Erosion_Color ("-- Erosion Color", Color) = (1,1,1,0)
        _Erosion_Tiling ("-- Erosion Tiling", Float) = 1
        _Erosion_Progress ("-- Erosion Progress", Range(0, 1)) = 0
        _Separator_10 ("# (10)", Float) = 1
        [NoScaleOffset][NonModifiableTextureData]_SampleTexture2D_7c1737227cde428799e62df11901fdc3_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
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
        // The graph is transparent with alpha clipping on; neither is a material keyword.
        #define _ALPHATEST_ON 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float4 _SampleTexture2D_7c1737227cde428799e62df11901fdc3_Texture_1_Texture2D_TexelSize;
            float _Title_Red;
            float _Title_Green;
            float _Title_Vertex_Colour;
            float _Title_Alpha_Colour_Blend;
            float _Title_Blue;
            float _Separator_9;
            float _Separator_4;
            float _Title_Surface_Input;
            float _Separator_5;
            float _Title_Depth_Fade;
            float _Title_Alpha;
            float _Separator_0;
            float _Separator_1;
            float _NOTE_Alpha;
            float _Separator_3;
            float4 _Color_C;
            float4 _Color_D;
            float _Title_Normal;
            float _NOTE_Global_Scrolling_UVs;
            float4 _Goo_Noise_Tex_TexelSize;
            float4 _Goo_Noise_Tex_ST;
            float _Separator_2;
            float _Title_FlowMap;
            float4 _Color_B;
            float4 _Color_A;
            float _Depth_Fade_Distance;
            float _Smoothness_Intensity;
            float _Flow_Map_Strength;
            float _Flow_Map_Speed;
            float _Depth_Fade_Colour_Contrast;
            float4 _Normal_Tex_TexelSize;
            float _Normal_Intensity;
            float _Goo_Noise_Contrast;
            float4 _Flow_Map_Tex_TexelSize;
            float4 _Depth_Fade_Color_A;
            float _Title_Vertex_Displacement;
            float _Title_Noise;
            float4 _Vertex_Displacement_Noise_Tex_TexelSize;
            float4 _Vertex_Displacement_Noise_Tex_ST;
            float _UV_Vertex_Displacement_Speed_Direction_Y;
            float4 _Depth_Fade_Color_B;
            float _UV_Vertex_Displacement_Speed_Direction_X;
            float _Separator_6;
            float _Depth_Fade_Contrast;
            float _Title_Depth_Fade_1;
            float _Separator_7;
            float _Vertex_Displacement_Amount;
            float _Smoothness_Override;
            float _UV_Scrolling_Direction_Y;
            float _Separator_8;
            float _UV_Scrolling_Direction_X;
            float _NOTE_Mask_Vertex_Alpha_Channel;
            float _Title_Vertex_Displacement_1;
            float4 _Erosion_Color;
            float _Erosion_Progress;
            float _Title_Sine_Wave;
            float _Erosion_Tiling;
            float _Separator_10;
            float _SineWave_Mask_Min;
            float _SineWave_Mask_Max;
            float _SineWave_Displacement_Amount;
            float _Use_Fresnel_Instead_of_Vertex_Mask;
            float _Fresnel_Intensity;
            float _Fresnel_Amount;
            float _Alpha_Clip;
            float _Use_Custom_Alpha_Value;
            float _Global_Alpha;
            float _SineWave_Speed;
            float _SineWave_Mask_Tiling;
            float _Use_SineWave;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;

        TEXTURE2D(_Goo_Noise_Tex);
        TEXTURE2D(_Flow_Map_Tex);
        TEXTURE2D(_Normal_Tex);
        TEXTURE2D(_Vertex_Displacement_Noise_Tex);
        TEXTURE2D(_SampleTexture2D_7c1737227cde428799e62df11901fdc3_Texture_1_Texture2D);
        SAMPLER(sampler_SampleTexture2D_7c1737227cde428799e62df11901fdc3_Texture_1_Texture2D);
        // The graph's inline sampler: linear, repeat.
        SAMPLER(SamplerState_Linear_Repeat);
        #define GOO_SAMPLE(tex, uv) SAMPLE_TEXTURE2D(tex, SamplerState_Linear_Repeat, uv)

        // Vertex displacement along the object normal: an optional sine wave masked per vertex-colour
        // channel, then the scrolling displacement noise.
        float3 GooDisplace(float3 positionOS, float3 normalOS, float2 uv0, float4 color)
        {
            float3 wave = sin(color.rgb * _SineWave_Mask_Tiling + (_HLShaderTimeGame * _SineWave_Speed * 0.5 + 0.5));
            wave = smoothstep(0.0, 1.0, saturate((wave - _SineWave_Mask_Min) / (_SineWave_Mask_Max - _SineWave_Mask_Min)));
            float3 displaced = _Use_SineWave != 0.0 ? positionOS + normalOS * _SineWave_Displacement_Amount * wave : positionOS;
            float2 noiseUV = uv0 * _Vertex_Displacement_Noise_Tex_ST.xy +
                (_HLShaderTimeGame * float2(_UV_Vertex_Displacement_Speed_Direction_X, _UV_Vertex_Displacement_Speed_Direction_Y) + _Vertex_Displacement_Noise_Tex_ST.zw);
            float noise = SAMPLE_TEXTURE2D_LOD(_Vertex_Displacement_Noise_Tex, SamplerState_Linear_Repeat, noiseUV, 0).r;
            return displaced + noise * _Vertex_Displacement_Amount * normalOS;
        }

        // Saturated (scene eye depth - fragment clip w) / distance * contrast, from the camera depth
        // texture: _ZBufferParams for perspective, inverse view-projection for orthographic.
        float GooDepthFade(float3 positionWS, float4 positionCS)
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
            return saturate((sceneDepth - clipW) / _Depth_Fade_Distance * _Depth_Fade_Contrast);
        }

        // Flow map: two phases float a cycle apart over the scrolled, tiled noise UV, crossfaded by
        // |2 * phase - 1|. uvA uses frac(t * speed), uvB frac(t * speed + 0.5).
        struct GooFlow
        {
            float2 uvA;
            float2 uvB;
            float blend;
        };

        GooFlow GooFlowUV(float2 uv0)
        {
            float2 uv = (uv0 + _HLShaderTimeGame * float2(_UV_Scrolling_Direction_X, _UV_Scrolling_Direction_Y)) * _Goo_Noise_Tex_ST.xy + _Goo_Noise_Tex_ST.zw;
            float2 flow = (GOO_SAMPLE(_Flow_Map_Tex, uv).xy - 0.5) * -2.0 * _Flow_Map_Strength;
            float phase = frac(_HLShaderTimeGame * _Flow_Map_Speed);
            GooFlow result;
            result.uvA = uv + flow * phase;
            result.uvB = uv + flow * frac(_HLShaderTimeGame * _Flow_Map_Speed + 0.5);
            result.blend = abs(phase * 2.0 - 1.0);
            return result;
        }

        // The goo noise: both flow phases sampled with the noise tiling applied again, then contrasted
        // about 0.1.
        float3 GooNoise(GooFlow flow)
        {
            float3 a = GOO_SAMPLE(_Goo_Noise_Tex, flow.uvA * _Goo_Noise_Tex_ST.xy + _Goo_Noise_Tex_ST.zw).rgb;
            float3 b = GOO_SAMPLE(_Goo_Noise_Tex, flow.uvB * _Goo_Noise_Tex_ST.xy + _Goo_Noise_Tex_ST.zw).rgb;
            return saturate((lerp(a, b, flow.blend) - 0.1) * _Goo_Noise_Contrast + 0.1);
        }

        // Erosion value: the erosion texture's blue channel shifted by the progress.
        float GooErosion(float2 uv0)
        {
            float erosion = SAMPLE_TEXTURE2D(_SampleTexture2D_7c1737227cde428799e62df11901fdc3_Texture_1_Texture2D,
                sampler_SampleTexture2D_7c1737227cde428799e62df11901fdc3_Texture_1_Texture2D, uv0 * _Erosion_Tiling).b;
            return saturate(erosion + (_Erosion_Progress - 0.5) * 2.0);
        }

        // Graph alpha: 1 unless the custom alpha is on, then the eroded edge times the depth fade and
        // the global alpha.
        float GooAlpha(float erosion, float depthFade)
        {
            float edge = min(smoothstep(0.0, 1.0, saturate((0.5 - erosion) * 4.0)), 1.0);
            return _Use_Custom_Alpha_Value != 0.0 ? edge * depthFade * _Global_Alpha : 1.0;
        }

        // Tangent-space normal: both flow phases, crossfaded, then the graph's Normal Strength node.
        float3 GooNormalTS(GooFlow flow)
        {
            float3 a = UnpackNormal(GOO_SAMPLE(_Normal_Tex, flow.uvA));
            float3 b = UnpackNormal(GOO_SAMPLE(_Normal_Tex, flow.uvB));
            float3 normal = lerp(a, b, flow.blend);
            return float3(normal.xy * _Normal_Intensity, lerp(1.0, normal.z, saturate(_Normal_Intensity)));
        }

        // Base colour (also the emission): the noise between colour pairs C/D and A/B by the vertex
        // alpha or fresnel mask, recoloured towards the depth-fade colours where the goo is shallow,
        // plus the erosion rim colour.
        float3 GooColor(float3 noise, float mask, float depthFade, float erosion)
        {
            float shallow = 1.0 - depthFade;
            float3 goo = lerp(lerp(_Color_C.rgb, _Color_D.rgb, noise), lerp(_Color_A.rgb, _Color_B.rgb, noise), mask);
            float3 fadeColor = lerp(_Depth_Fade_Color_A.rgb, _Depth_Fade_Color_B.rgb, (1.0 - mask) * 2.0 * shallow);
            float3 color = lerp(fadeColor, goo, depthFade);
            color = saturate(lerp(color, goo * _Depth_Fade_Colour_Contrast, shallow));
            return color + saturate((erosion - 0.35) * 3.33333325) * _Erosion_Color.rgb;
        }

        // The colour mask: saturated vertex alpha, or a fresnel of the interpolated vertex normal.
        float GooMask(float4 color, float3 normalWS, float3 viewDirWS)
        {
            float fresnel = saturate(pow(1.0 - saturate(dot(normalize(normalWS), viewDirWS)), _Fresnel_Amount) * _Fresnel_Intensity);
            return _Use_Fresnel_Instead_of_Vertex_Mask != 0.0 ? fresnel : saturate(color.a);
        }

        // Depth, shadow and normal passes clip the graph alpha against _Alpha_Clip directly.
        void GooClip(float2 uv0, float3 positionWS, float4 positionCS)
        {
            clip(GooAlpha(GooErosion(uv0), GooDepthFade(positionWS, positionCS)) - _Alpha_Clip);
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
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
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
                float3 positionOS = GooDisplace(input.positionOS.xyz, input.normalOS, input.uv0.xy, input.color);
                VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv0 = input.uv0;
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

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float depthFade = GooDepthFade(input.positionWS, input.positionCS);
                float mask = GooMask(input.color, input.normalWS, viewDirWS);
                GooFlow flow = GooFlowUV(input.uv0.xy);
                float3 noise = GooNoise(flow);
                float erosion = GooErosion(input.uv0.xy);
                float3 color = GooColor(noise, mask, depthFade, erosion);

                // As the generated pass: alpha clipping (with alpha-to-coverage sharpening when available),
                // then the saturated alpha of a transparent surface.
                float alpha = AlphaDiscard(GooAlpha(erosion, depthFade), _Alpha_Clip);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.viewDirectionWS = viewDirWS;
                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                inputData.tangentToWorld = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 normalTS = GooNormalTS(flow);
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
                surfaceData.albedo = color;
                surfaceData.metallic = 0;
                surfaceData.smoothness = saturate(_Smoothness_Intensity * noise.r + _Smoothness_Override);
                surfaceData.normalTS = normalTS;
                surfaceData.emission = color;
                surfaceData.occlusion = 1;
                surfaceData.alpha = saturate(alpha);
                #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                float4 result = UniversalFragmentPBR(inputData, surfaceData);
                result.rgb = MixFog(result.rgb, inputData.fogCoord);
                result.a = OutputAlpha(result.a, true);
                outColor = result;
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
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv0 : TEXCOORD0; float3 positionWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(GooDisplace(input.positionOS.xyz, input.normalOS, input.uv0.xy, input.color));
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
                output.uv0 = input.uv0.xy;
                output.positionWS = positionWS;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                GooClip(input.uv0, input.positionWS, input.positionCS);
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
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv0 : TEXCOORD0; float3 positionWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(GooDisplace(input.positionOS.xyz, input.normalOS, input.uv0.xy, input.color));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv0 = input.uv0.xy;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                GooClip(input.uv0, input.positionWS, input.positionCS);
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
                float2 uv0 : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(GooDisplace(input.positionOS.xyz, input.normalOS, input.uv0.xy, input.color));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv0 = input.uv0.xy;
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
                GooClip(input.uv0, input.positionWS, input.positionCS);
                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                float3 normalWS = TransformTangentToWorld(GooNormalTS(GooFlowUV(input.uv0)), float3x3(input.tangentWS.xyz, bitangent, input.normalWS));
                outNormalWS = float4(NormalizeNormalPerPixel(normalWS), 0.0);
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
                float4 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float4 color : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                #if defined(EDITOR_VISUALIZATION)
                float2 vizUV : TEXCOORD4;
                float4 lightCoord : TEXCOORD5;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionOS = GooDisplace(input.positionOS.xyz, input.normalOS, input.uv0.xy, input.color);
                output.positionCS = UnityMetaVertexPosition(positionOS, input.uv1, input.uv2);
                output.positionWS = TransformObjectToWorld(positionOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv0 = input.uv0;
                output.color = input.color;
                #if defined(EDITOR_VISUALIZATION)
                UnityEditorVizData(positionOS, input.uv0.xy, input.uv1, input.uv2, output.vizUV, output.lightCoord);
                #endif
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float depthFade = GooDepthFade(input.positionWS, input.positionCS);
                float erosion = GooErosion(input.uv0.xy);
                clip(GooAlpha(erosion, depthFade) - _Alpha_Clip);
                float mask = GooMask(input.color, input.normalWS, GetWorldSpaceNormalizeViewDir(input.positionWS));
                float3 color = GooColor(GooNoise(GooFlowUV(input.uv0.xy)), mask, depthFade, erosion);
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = color;
                metaInput.Emission = color;
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
