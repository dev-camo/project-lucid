// Reconstructed from this release's decoded Metal programs and serialized pass.
// Original shader: HardLight/Particle/HL_PFX_AlphaBlendAdd, hand-written HLSL (platform 14, blobs 1/3).
// The camera-distance fade always scales alpha once; the toggle applies it again to all four channels.
Shader "HardLight/Particle/HL_PFX_AlphaBlendAdd"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}
        [HDR]_emissive_colour ("Emissive Colour", Color) = (1,1,1,1)
        _Separator_0 ("#", Float) = 0
        _Title_Camera_Distance_Fade ("## Camera Distance Fade", Float) = 0
        [Toggle]_distance_fade_toggle ("Distance Fade Toggle", Float) = 0
        [Toggle]_distance_fade_custom_particle_data_toggle ("Use Custom Particle Data Instead? [_distance_fade_toggle == 1]", Float) = 0
        _note_custom_data_1 ("!NOTE Step 1: Enable 'Renderer>Custom Vertex Streams' [_distance_fade_custom_particle_data_toggle == 1]", Float) = 0
        _note_custom_data_2 ("!NOTE Step 2: Add 'Custom1.xy' to 'Custom Data'. [_distance_fade_custom_particle_data_toggle == 1]", Float) = 0
        _note_custom_data3 ("!NOTE 'Custom1.x' = Near [_distance_fade_custom_particle_data_toggle == 1]", Float) = 0
        _note_custom_data4 ("!NOTE 'Custom1.y' = Far [_distance_fade_custom_particle_data_toggle == 1]", Float) = 0
        _distance_fade_near ("-- Distance Fade Near [_distance_fade_toggle == 1 && _distance_fade_custom_particle_data_toggle == 0]", Float) = 0
        _distance_fade_far ("-- Distance Fade Far [_distance_fade_toggle == 1 && _distance_fade_custom_particle_data_toggle == 0]", Float) = 0
        _Separator_1 ("#", Float) = 0
        _Title_Blending_Options ("## Blending Options", Float) = 0
        [Toggle]_alpha_channel_switch ("Use R channel for Alpha", Float) = 1
        [Toggle]_alpha_blend_add_toggle ("Disable Alpha Blend Add Toggle", Float) = 0
        _note_requires_premultiplied_transparency ("!NOTE AlphaBlendAdd requires 'Premultiplied transparency' to be set", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)]_BlendSrc ("-- Blend mode Source", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_BlendDst ("-- Blend mode Destination", Float) = 10
        _Folder_Blend_Type_Notes ("# Blend Type Helper", Float) = 0
        _Blend_Type_URL ("[Unity Documentation URL Link](https://docs.unity3d.com/Manual/SL-Blend.html)", Float) = 0
        _note_traditional_transparency ("!NOTE Blend SrcAlpha OneMinusSrcAlpha // Traditional transparency", Float) = 0
        _note_premultiplied_transparency ("!NOTE Blend One OneMinusSrcAlpha // Premultiplied transparency", Float) = 0
        _note_additive ("!NOTE Blend One One // Additive", Float) = 0
        _note_soft_additive ("!NOTE Blend OneMinusDstColor One // Soft additive", Float) = 0
        _note_multiplicative ("!NOTE Blend DstColor Zero // Multiplicative", Float) = 0
        _note_2x_multiplicative ("!NOTE Blend DstColor SrcColor // 2x multiplicative", Float) = 0
        _Separator_3 ("#", Float) = 0
        _Title_Other ("## Other", Float) = 0
        [Enum(Off,0,On,1)]_ZWrite ("-- ZWrite (Depth Write)", Float) = 0
    }
    SubShader
    {
        Tags { "IgnoreProjector"="true" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }

        Pass
        {
            Name "HL Particle Main HLSL"
            Blend [_BlendSrc] [_BlendDst]
            ZTest LEqual
            ZWrite [_ZWrite]
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _emissive_colour;
                float _distance_fade_toggle;
                float _distance_fade_near;
                float _distance_fade_far;
                float _alpha_channel_switch;
                float _alpha_blend_add_toggle;
                float _distance_fade_custom_particle_data_toggle;
            CBUFFER_END
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;     // xy: UV, zw: Custom1.xy (near, far) when enabled
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR0;
                float4 uv0 : TEXCOORD0;
                float distanceFade : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz));
                output.color = input.color;
                output.uv0 = input.uv0;
                float2 range = _distance_fade_custom_particle_data_toggle == 1.0
                    ? input.uv0.zw : float2(_distance_fade_near, _distance_fade_far);
                output.distanceFade = saturate(((output.positionCS.w - output.positionCS.z) - range.x) / (range.y - range.x));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // UV0 is sampled directly; _MainTex_ST is declared but unused, as in the original.
                // Core.hlsl supplies _GlobalMipBias.x through SAMPLE_TEXTURE2D, the single bias the original applies.
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv0.xy);
                float3 added = lerp(tex.rgb + input.color.rgb, tex.rgb, 1.0 - tex.r);
                float3 color = lerp(added * input.color.rgb, tex.rgb, _alpha_blend_add_toggle);
                float4 result;
                result.rgb = color * input.color.rgb * _emissive_colour.rgb;
                float2 alphas = tex.ra * input.color.a;
                result.a = (_alpha_channel_switch == 1.0 ? alphas.x : alphas.y) * input.distanceFade;
                return lerp(result, result * input.distanceFade, _distance_fade_toggle);
            }
            ENDHLSL
        }
    }
    CustomEditor "Needle.MarkdownShaderGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
