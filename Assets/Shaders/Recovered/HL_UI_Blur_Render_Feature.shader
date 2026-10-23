// Reconstructed from this release's decoded Metal programs and serialized pass.
// Original shader: HardLight/RenderFeature/HL_UI_Blur_Render_Feature (platform 14).
// A single full-screen pass: a vertex-ID triangle, and a five-tap blur of _BlitTexture.
Shader "HardLight/RenderFeature/HL_UI_Blur_Render_Feature"
{
    Properties
    {
        [HideInInspector]_BlitTexture ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Blend One Zero
            ZTest LEqual
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Shipped UnityPerMaterial layout: the texel size is declared as a float2, and _offset
            // is set by the render feature rather than exposed as a property.
            CBUFFER_START(UnityPerMaterial)
                float2 _BlitTexture_TexelSize;
                float4 _BlitTexture_ST;
                float _offset;
            CBUFFER_END
            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // The full-screen triangle from the vertex id, with v flipped; no blit scale and bias.
            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                float2 corner = float2((vertexID << 1) & 2, vertexID & 2);
                output.positionCS = float4(corner * 2.0 - 1.0, 1.0, 1.0);
                output.uv = float2(corner.x, 1.0 - corner.y);
                return output;
            }

            // The centre and the four diagonal taps at _offset texels, averaged. Core.hlsl's
            // SAMPLE_TEXTURE2D adds the single _GlobalMipBias.x the original applies.
            half4 Frag(Varyings input) : SV_Target
            {
                float2 step = _offset * _BlitTexture_TexelSize;
                float4 sum = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.uv + step);
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.uv);
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.uv + step * float2(1, -1));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.uv + step * float2(-1, 1));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.uv - step);
                return sum * 0.2;
            }
            ENDHLSL
        }
    }
}
