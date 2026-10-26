// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: Hidden/HardLight/Particle/HL_Particle_Dream, a URP 14 Sprite Unlit Shader Graph
// (platform 14), additive. The diffuse UV is pushed by Shader Graph's gradient noise, scrolled by time or
// sine time; the result is tinted by the vertex and renderer colour. The 2D and forward passes are identical.
Shader "Hidden/HardLight/Particle/HL_Particle_Dream"
{
    Properties
    {
        _Alpha ("Alpha", Range(0, 2)) = 1
        [NoScaleOffset]_DiffTex ("Diff Texture", 2D) = "white" {}
        [KeywordEnum(Sine Time, Time)]_TIME_TYPE ("Time Type", Float) = 1
        _Noise_Speed ("Noise Speed", Range(0, 2)) = 1
        _Noise_Scale ("Noise Scale", Float) = 2
        _Noise_Intensity ("Noise Intensity", Float) = 0
        [HideInInspector][NoScaleOffset]unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "UniversalMaterialType"="Unlit" "Queue"="Transparent" "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalSpriteUnlitSubTarget" }

        HLSLINCLUDE
        #pragma target 2.0
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float _Noise_Speed;
            float _Noise_Intensity;
            float _Noise_Scale;
            float4 _DiffTex_TexelSize;
            float _Alpha;
        CBUFFER_END

        // The sprite renderer's colour, published by URP's 2D renderer (white elsewhere).
        float4 _RendererColor;

        TEXTURE2D(_DiffTex);
        SAMPLER(sampler_DiffTex);

        struct Attributes
        {
            float3 positionOS : POSITION;
            float4 uv0 : TEXCOORD0;
            float4 color : COLOR;
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float4 uv0 : TEXCOORD0;
            float4 color : TEXCOORD1;
            float3 positionWS : TEXCOORD2;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            output.positionWS = TransformObjectToWorld(input.positionOS);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv0 = input.uv0;
            output.color = input.color * _RendererColor;
            return output;
        }

        // Shader Graph's Gradient Noise node (legacy mod-289 hash).
        float2 GradientNoiseDir(float2 p)
        {
            p = p % 289;
            float x = (34 * p.x + 1) * p.x % 289 + p.y;
            x = (34 * x + 1) * x % 289;
            x = frac(x / 41) * 2 - 1;
            return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
        }

        float GradientNoise(float2 uv, float scale)
        {
            float2 p = uv * scale;
            float2 ip = floor(p);
            float2 fp = frac(p);
            float d00 = dot(GradientNoiseDir(ip), fp);
            float d01 = dot(GradientNoiseDir(ip + float2(0, 1)), fp - float2(0, 1));
            float d10 = dot(GradientNoiseDir(ip + float2(1, 0)), fp - float2(1, 0));
            float d11 = dot(GradientNoiseDir(ip + float2(1, 1)), fp - float2(1, 1));
            fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
            return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
        }

        float4 Frag(Varyings input) : SV_Target
        {
            #if defined(_TIME_TYPE_SINE_TIME)
            float time = _TimeParameters.y;
            #else
            float time = _TimeParameters.x;
            #endif
            float noise = GradientNoise(input.uv0.xy + time * _Noise_Speed, _Noise_Scale);
            float4 colour = SAMPLE_TEXTURE2D(_DiffTex, sampler_DiffTex, input.uv0.xy + noise * _Noise_Intensity);
            colour.a *= _Alpha;
            // The Sprite target discards fully transparent fragments.
            if (colour.a == 0.0) discard;
            return colour * input.color;
        }
        ENDHLSL

        Pass
        {
            Name "Sprite Unlit"
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha One, One One
            ZTest LEqual
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma shader_feature_local_fragment _TIME_TYPE_SINE_TIME _TIME_TYPE_TIME
            ENDHLSL
        }

        Pass
        {
            Name "Sprite Unlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One, One One
            ZTest LEqual
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma shader_feature_local_fragment _TIME_TYPE_SINE_TIME _TIME_TYPE_TIME
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
