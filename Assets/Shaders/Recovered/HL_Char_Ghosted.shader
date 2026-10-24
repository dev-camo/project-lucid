// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Character/HL_Char_Ghosted, hand-written HLSL (platform 14, blobs 1/3/5/7).
// The first pass has no LightMode, so URP draws it as SRPDefaultUnlit: a depth-only prepass that
// lets the transparent ForwardLit pass hide the character's own back layers.
Shader "HardLight/Character/HL_Char_Ghosted"
{
    Properties
    {
        _Seperator ("#", Float) = 0
        _Title_Surface_Inputs ("## Surface Inputs", Float) = 0
        [NoScaleOffset][MainTexture]_Diffuse_Texture ("-- Dffuse Texture", 2D) = "white" {}
        _Diffuse_Colour_Tint ("-- Diffuse Colour Tint", Color) = (1,1,1,1)
        _Alpha ("Alpha", Range(0, 1)) = 0.5
        _Seperator_1 ("# (1)", Float) = 0
        _Title_Fresnel ("## Fresnel", Float) = 0
        [HDR]_Fresnel_Colour ("-- Fresnel Colour", Color) = (1,1,1,1)
        _Fresnel_Amount ("-- Fresnel Amount", Float) = 1
        _Fresnel_Intensity ("-- Fresnel Intensity", Float) = 1
        _Seperator_2 ("# (2)", Float) = 0
        _Title_Dream_Noise ("## Dream Noise", Float) = 0
        _Dream_Noise_Opacity ("-- Dream Noise Opacity", Range(0, 1)) = 1
        _Dream_Noise_Speed ("-- Dream Noise Speed", Range(0, 2)) = 1
        [NoScaleOffset]_Dream_Noise_Texture ("-- Dream Noise Texture", 2D) = "white" {}
        [HDR]_Dream_Colour_Tint ("-- Dream Colour Tint", Color) = (1,1,1,1)
        _Dream_Noise_Fresnel_Amount ("-- Dream Noise Fresnel Amount", Float) = 0
        _Dream_Noise_UV_Scale ("-- Dream Noise UV Scale", Float) = 1
        _Dream_Noise_Fresnel_Intensity ("-- Dream Noise Fresnel Intensity", Float) = 0
        _Seperator_3 ("# (3)", Float) = 0
        _Title_Fade_To_Colour ("## Fade To Colour", Float) = 0
        _Fade_To_Colour ("-- Fade To Colour", Range(0, 1)) = 0
        [HDR]_Fade_Colour ("-- Fade Colour", Color) = (1,1,1,1)
        [NoScaleOffset][Space()]_Perlin_Noise_Texture ("Dream Noise Texture&", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "UniversalMaterialType"="Unlit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float _Seperator;
            float _Title_Surface_Inputs;
            float4 _Diffuse_Texture_ST;
            float _Alpha;
            float4 _Diffuse_Colour_Tint;
            float _Seperator_1;
            float _Title_Fresnel;
            float4 _Fresnel_Colour;
            float _Fresnel_Amount;
            float _Fresnel_Intensity;
            float _Seperator_2;
            float _Title_Dream_Noise;
            float _Dream_Noise_Opacity;
            float _Dream_Noise_Speed;
            float4 _Dream_Noise_Texture_ST;
            float4 _Dream_Colour_Tint;
            float _Dream_Noise_Fresnel_Amount;
            float _Dream_Noise_UV_Scale;
            float _Dream_Noise_Fresnel_Intensity;
            float4 _Perlin_Noise_Texture_ST;
            float _Seperator_3;
            float _Title_Fade_To_Colour;
            float _Fade_To_Colour;
            float4 _Fade_Colour;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "DepthOnly"
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR0;
            };

            // Blobs 1/3: the clamped vertex colour reaches the target, which ColorMask 0 then hides.
            Varyings Vert(float3 positionOS : POSITION, float4 color : COLOR)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS);
                output.color = saturate(color);
                return output;
            }
            float4 Frag(Varyings input) : SV_Target { return input.color; }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Game-scaled shader time published by the time manager.
            float _HLShaderTimeGame;

            TEXTURE2D(_Diffuse_Texture);
            SAMPLER(sampler_Diffuse_Texture);
            TEXTURE2D(_Dream_Noise_Texture);
            SAMPLER(sampler_Dream_Noise_Texture);
            TEXTURE2D(_Perlin_Noise_Texture);
            SAMPLER(sampler_Perlin_Noise_Texture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv * _Diffuse_Texture_ST.xy + _Diffuse_Texture_ST.zw;
                // The shipped vertex normalizes both the perspective view delta and the orthographic view row.
                output.viewDirWS = normalize(GetWorldSpaceViewDir(positionWS));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float rim = 1.0 - saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                float fresnel = saturate(pow(rim, _Fresnel_Amount) * _Fresnel_Intensity);
                float dreamFresnel = saturate(pow(rim, _Dream_Noise_Fresnel_Amount) * _Dream_Noise_Fresnel_Intensity);

                // Core.hlsl supplies _GlobalMipBias.x through SAMPLE_TEXTURE2D, the single bias the original applies.
                // Two scrolling taps of the noise texture index the dream texture. _Dream_Noise_Opacity
                // and the _ST vectors other than the diffuse one are unused, as in the original.
                float4 scroll = _Dream_Noise_Speed * float4(-0.2, -0.15, 0.1, 0.1) * _HLShaderTimeGame;
                float4 noiseUV = (input.uv.xyxy + float4(0.0, 0.0, 0.3, 0.2)) * _Dream_Noise_UV_Scale + scroll;
                float2 noise = saturate(SAMPLE_TEXTURE2D(_Perlin_Noise_Texture, sampler_Perlin_Noise_Texture, noiseUV.xy).xy +
                                        SAMPLE_TEXTURE2D(_Perlin_Noise_Texture, sampler_Perlin_Noise_Texture, noiseUV.zw).xy);
                float4 dream = SAMPLE_TEXTURE2D(_Dream_Noise_Texture, sampler_Dream_Noise_Texture, noise);
                float4 diffuse = SAMPLE_TEXTURE2D(_Diffuse_Texture, sampler_Diffuse_Texture, input.uv) * _Diffuse_Colour_Tint;

                float4 color = lerp(diffuse, dream * _Dream_Colour_Tint, dreamFresnel);
                color = lerp(color, fresnel * _Fresnel_Colour, fresnel * _Fresnel_Colour.w);
                color = lerp(color, _Fade_Colour, _Fade_To_Colour);
                return float4(color.rgb, color.a * _Alpha);
            }
            ENDHLSL
        }
    }
    CustomEditor "Needle.MarkdownShaderGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
