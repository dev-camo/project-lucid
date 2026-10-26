// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Sky/HL_Sky_Trippy, a URP 14 Unlit Shader Graph (platform 14), opaque. Two
// scrolling noise lookups pick colours from a top and a bottom gradient ramp, blended by object-space height,
// then faded to a bottom and a top fog colour; the optional cubemap comp is composited over by its alpha.
// No shipped program enables _USE_PATTERN_MASK, so that keyword keeps the base body.
Shader "HardLight/Sky/HL_Sky_Trippy"
{
    Properties
    {
        _Documentation_Link_Below ("## Documentation Link Below : HL_Env_Trippy_Sky", Float) = 0
        _Documentation_Link ("[https://hardlight.atlassian.net/wiki/spaces/HL/pages/8334074/SDT+-+HL+Env+Trippy+Sky](https://hardlight.atlassian.net/wiki/spaces/HL/pages/8334074/SDT+-+HL+Env+Trippy+Sky)", Float) = 0
        _Separator_0 ("#", Float) = 0
        _Title_Noise ("## Noise", Float) = 0
        _Noise_Texture ("-- Noise Texture", 2D) = "white" {}
        _Noise_UV_Speed ("-- Noise UV Speed", Range(0, 1)) = 0.5
        _Separator_1 ("# (1)", Float) = 0
        _Title_Gradient_Ramp_Blend ("## Gradient Ramp Blend", Float) = 0
        _Gradient_Ramp_Blend_Amount ("-- Gradient Ramp Blend Alount", Float) = 1
        _Gradient_Ramp_Blend_Position ("-- Gradient Blend Position", Float) = 0
        _Gradient_Ramp_Blend_Opacity ("-- Gradient Blend Opacity", Range(0, 1)) = 1
        _Separator_2 ("# (2)", Float) = 0
        _Title_Top ("## Top", Float) = 0
        _Top_Gradient_Ramp_Offset ("-- Top Gradient Ramp Offset", Float) = 3
        [NoScaleOffset]_Top_Gradient_Ramp_Tex ("-- Top Gradient Ramp Texture", 2D) = "white" {}
        _Top_Fog_Blend_Amount ("-- Top Fog Blend", Float) = 1
        _Top_Fog_Colour ("-- Top Fog Colour", Color) = (1,0,0,0)
        _Top_Fog_Position ("-- Top Fog Position", Float) = 0
        _Top_Fog_Opacity ("-- Top Fog Opacity", Range(0, 1)) = 1
        _Separator_3 ("# (3)", Float) = 0
        _Title_Bottom ("## Bottom", Float) = 0
        _Bottom_Gradient_Ramp_Offset ("-- Bottom Gradient Ramp Offset", Float) = 3
        [NoScaleOffset]_Bottom_Gradient_Ramp_Tex ("-- Bottom Gradient Ramp Texture", 2D) = "white" {}
        _Bottom_Fog_Blend ("-- Bottom Fog Blend", Float) = 1
        _Bottom_Fog_Position ("-- Bottom Fog Position", Float) = 0
        _Bottom_Fog_Opacity ("-- Bottom Fog Opacity", Range(0, 1)) = 1
        _Bottom_Fog_Colour ("-- Bottom Fog Colour", Color) = (0,1,0.5764706,0)
        _Separator_4 ("# (4)", Float) = 0
        _Title_Cubemap_Comp ("## Cubemap Comp", Float) = 0
        _REF_USE_CUBEMAP_COMP ("!REF _USE_CUBEMAP_COMP", Float) = 0
        [Toggle(_USE_CUBEMAP_COMP)]_USE_CUBEMAP_COMP ("Use Cubemap comp?", Float) = 0
        [NoScaleOffset]_Cubemap_Texture ("-- Cubemap Texture [_USE_CUBEMAP_COMP]", Cube) = "" {}
        _Cubemap_Rotation_Amount ("-- Rotation Amount [_USE_CUBEMAP_COMP]", Range(0, 360)) = 0
        [ToggleUI]_Cubemap_Flip_Y_Axis ("-- Flip Y Axis? [_USE_CUBEMAP_COMP]", Float) = 0
        _Cubemap_Blur_Amount ("-- Blur Amount [_USE_CUBEMAP_COMP]", Range(0, 10)) = 0
        _Separator_5 ("# (5)", Float) = 0
        _Title_Texture_Mask ("## Texture Mask", Float) = 0
        _REF_USE_PATTERN_MASK ("!REF _USE_PATTERN_MASK", Float) = 0
        _NOTE_Vertex_Colour_Alpha_can_be_used_as_masking ("!NOTE Vertex Colour Alpha can be used as masking. [_USE_PATTERN_MASK]", Float) = 0
        [Toggle(_USE_PATTERN_MASK)]_USE_PATTERN_MASK ("Use Pattern Mask?", Float) = 0
        _Texture_Mask ("-- Texture Mask [_USE_PATTERN_MASK]", 2D) = "black" {}
        _NOTE_Offset_XY_controls_scrolling ("!NOTE Offset XY controls scrolling. [_USE_PATTERN_MASK]", Float) = 0
        _Texture_Mask_Channel_Selection_Red_Green_Blue_Alpha ("-- Texture Mask Channel Selection (Red, Green, Blue, Alpha) & [_USE_PATTERN_MASK]", Vector) = (1,0,0,0)
        _Title_Colour ("### Colour [_USE_PATTERN_MASK]", Float) = 0
        [ToggleUI]_Use_Background_as_Colour ("-- Use Background as Colour? [_USE_PATTERN_MASK]", Float) = 0
        _Title_Gradient_Colour ("### Gradient Colour [_USE_PATTERN_MASK]", Float) = 0
        _Gradient_Top ("-- Gradient Top [_USE_PATTERN_MASK]", Color) = (1,1,1,0)
        _Gradient_Bottom ("-- Gradient Bottom [_USE_PATTERN_MASK]", Color) = (0,0,0,0)
        _Gradient_Position ("-- Gradient Position [_USE_PATTERN_MASK]", Float) = 1
        _Gradient_Blend ("-- Gradient Blend [_USE_PATTERN_MASK]", Float) = 0.5
        _Separator_6 ("# (6)", Float) = 0
        [HideInInspector]_QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl ("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Unlit" "Queue"="Geometry" "DisableBatching"="False" "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalUnlitSubTarget" }

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float4 _Bottom_Gradient_Ramp_Tex_TexelSize;
            float _Bottom_Fog_Opacity;
            float _Title_Noise;
            float _Bottom_Gradient_Ramp_Offset;
            float _Noise_UV_Speed;
            float _Separator_1;
            float _Gradient_Ramp_Blend_Opacity;
            float _Gradient_Ramp_Blend_Amount;
            float _Gradient_Ramp_Blend_Position;
            float _Title_Gradient_Ramp_Blend;
            float4 _Noise_Texture_TexelSize;
            float4 _Noise_Texture_ST;
            float _Title_Top;
            float _Top_Gradient_Ramp_Offset;
            float _Bottom_Fog_Blend;
            float4 _Top_Gradient_Ramp_Tex_TexelSize;
            float _Title_Bottom;
            float _Separator_2;
            float _Top_Fog_Opacity;
            float _Separator_3;
            float _Top_Fog_Blend_Amount;
            float _Separator_4;
            float _Top_Fog_Position;
            float4 _Top_Fog_Colour;
            float _Bottom_Fog_Position;
            float4 _Bottom_Fog_Colour;
            float _Documentation_Link;
            float _Documentation_Link_Below;
            float _Title_Cubemap_Comp;
            float _REF_USE_CUBEMAP_COMP;
            float _Separator_5;
            float _Separator_0;
            float _Cubemap_Flip_Y_Axis;
            float _Separator_6;
            float _Title_Gradient_Colour;
            float _REF_USE_PATTERN_MASK;
            float _Cubemap_Blur_Amount;
            float _NOTE_Vertex_Colour_Alpha_can_be_used_as_masking;
            float _Cubemap_Rotation_Amount;
            float4 _Gradient_Bottom;
            float _Title_Colour;
            float _Title_Texture_Mask;
            float4 _Gradient_Top;
            float _Gradient_Position;
            float _Gradient_Blend;
            float4 _Texture_Mask_TexelSize;
            float4 _Texture_Mask_ST;
            float _NOTE_Offset_XY_controls_scrolling;
            float _Use_Background_as_Colour;
            float4 _Texture_Mask_Channel_Selection_Red_Green_Blue_Alpha;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Blend One Zero
            ZTest LEqual
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma shader_feature _ _SAMPLE_GI
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #pragma shader_feature_local _ _USE_CUBEMAP_COMP
            #pragma shader_feature_local _ _USE_PATTERN_MASK
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            TEXTURE2D(_Noise_Texture);
            TEXTURE2D(_Top_Gradient_Ramp_Tex);
            TEXTURE2D(_Bottom_Gradient_Ramp_Tex);
            TEXTURECUBE(_Cubemap_Texture);
            SAMPLER(sampler_Cubemap_Texture);
            // The graph's inline sampler: linear, repeat.
            SAMPLER(SamplerState_Linear_Repeat);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 uv0 : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float4 color : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
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
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv0 = input.uv0;
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Two lookups of the tiled noise, one scrolled (+s, +s) and one offset by (0.3, 0.2) and scrolled
                // (-s, +s), with s = 0.1 * (1 - speed) * game time; their red and green are added.
                float scroll = (0.1 - 0.1 * _Noise_UV_Speed) * _HLShaderTimeGame;
                float2 noiseUV = input.uv0.xy * _Noise_Texture_ST.xy + _Noise_Texture_ST.zw;
                float2 noise = SAMPLE_TEXTURE2D(_Noise_Texture, SamplerState_Linear_Repeat, noiseUV + scroll).xy +
                               SAMPLE_TEXTURE2D(_Noise_Texture, SamplerState_Linear_Repeat, noiseUV + float2(0.3, 0.2) + float2(-scroll, scroll)).xy;
                float3 bottom = SAMPLE_TEXTURE2D(_Bottom_Gradient_Ramp_Tex, SamplerState_Linear_Repeat, noise * _Bottom_Gradient_Ramp_Offset).rgb;
                float3 top = SAMPLE_TEXTURE2D(_Top_Gradient_Ramp_Tex, SamplerState_Linear_Repeat, noise * _Top_Gradient_Ramp_Offset).rgb;

                // Object-space height drives the ramp blend and both fogs; each opacity lifts its weight toward 1.
                float height = TransformWorldToObject(input.positionWS).y;
                float rampWeight = saturate(saturate(height * _Gradient_Ramp_Blend_Amount + _Gradient_Ramp_Blend_Position) + 1.0 - _Gradient_Ramp_Blend_Opacity);
                float3 colour = lerp(bottom, top, rampWeight);
                float bottomFog = saturate(saturate(height * _Bottom_Fog_Blend + _Bottom_Fog_Position) + 1.0 - _Bottom_Fog_Opacity);
                colour = lerp(_Bottom_Fog_Colour.rgb, colour, bottomFog);
                float topFog = saturate(saturate((1.0 - height) * _Top_Fog_Blend_Amount + _Top_Fog_Position) + 1.0 - _Top_Fog_Opacity);
                colour = lerp(_Top_Fog_Colour.rgb, colour, topFog);

                #if defined(_USE_CUBEMAP_COMP)
                // The cubemap reads the direction toward the camera, turned about Y and optionally flipped in Y, at
                // the blur amount's level, and composites over by its alpha.
                float angle = _Cubemap_Rotation_Amount * 0.0174532924;
                float s = sin(angle), c = cos(angle);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 direction = float3(c * view.x + s * view.z, view.y - 2.0 * _Cubemap_Flip_Y_Axis * view.y, c * view.z - s * view.x);
                float4 cubemap = SAMPLE_TEXTURECUBE_LOD(_Cubemap_Texture, sampler_Cubemap_Texture, direction, _Cubemap_Blur_Amount);
                colour = lerp(colour, cubemap.rgb, cubemap.a);
                #endif

                outColor = float4(colour, 1.0);
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
            #pragma shader_feature_local _ _USE_CUBEMAP_COMP
            #pragma shader_feature_local _ _USE_PATTERN_MASK

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                return output;
            }
            float4 Frag(Varyings input) : SV_Target { return input.positionCS.zzzz; }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
