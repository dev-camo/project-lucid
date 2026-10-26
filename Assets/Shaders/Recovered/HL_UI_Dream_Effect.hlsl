// Shared program for both shipped copies of HardLight/UI/HL_UI_Dream_Effect (the bundled shader and the
// Assets/Shader copy), reconstructed from the decoded Metal programs.
#ifndef HL_UI_DREAM_EFFECT_INCLUDED
#define HL_UI_DREAM_EFFECT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
CBUFFER_START(UnityPerMaterial)
    float4 _Dream_Noise_ST;
    float _Dream_Noise_Speed;
    float _Dream_Gradient_Offset;
    float _Dream_Gradient_Multiplier;
    float _Alpha_Dream_Noise_Amount;
    float _Alpha_Dream_Noise_Contrast;
    float _Use_Sprite_RGB_Texture;
    float _UseScreenSpaceUVs;
    float4 _ColourTint;
CBUFFER_END

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Dream_Gradient);
SAMPLER(sampler_Dream_Gradient);
TEXTURE2D(_Dream_Noise);
SAMPLER(sampler_Dream_Noise);

struct Attributes
{
    float3 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float2 noiseUV : TEXCOORD1;
    float4 color : COLOR;
};

// The noise UV is the sprite UV or, with _UseScreenSpaceUVs, the undivided screen position, tiled and offset.
Varyings Vert(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    float2 screenUV = ComputeScreenPos(output.positionCS).xy;
    output.noiseUV = TRANSFORM_TEX(lerp(input.uv, screenUV, _UseScreenSpaceUVs), _Dream_Noise);
    output.uv = input.uv;
    output.color = input.color;
    return output;
}

// Linear mid grey (0.5 ^ 2.2), the pivot of the alpha noise contrast.
static const float MidGrey = 0.217637643;

float4 Frag(Varyings input) : SV_Target
{
    // Two noise lookups scrolling against each other; their sum indexes the dream gradient.
    float2 scroll = _Dream_Noise_Speed * _Time.y;
    float2 noise = SAMPLE_TEXTURE2D(_Dream_Noise, sampler_Dream_Noise, input.noiseUV + scroll).xy
                 + SAMPLE_TEXTURE2D(_Dream_Noise, sampler_Dream_Noise, scroll - input.noiseUV).xy;
    float3 gradient = SAMPLE_TEXTURE2D(_Dream_Gradient, sampler_Dream_Gradient, noise).rgb * _Dream_Gradient_Multiplier;
    float alphaNoise = saturate(((noise.x - MidGrey) * _Alpha_Dream_Noise_Contrast + MidGrey) * _Alpha_Dream_Noise_Amount);

    // _Use_Sprite_RGB_Texture 0 shows the gradient; 1 shows the per-channel maximum of the gradient and the sprite
    // colour (values between lerp the sprite towards that maximum, then towards the gradient).
    float4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
    float3 color = lerp(sprite.rgb, max(gradient, sprite.rgb), _Use_Sprite_RGB_Texture);
    color = lerp(color, gradient, 1.0 - _Use_Sprite_RGB_Texture);

    // The sprite alpha, eaten into by the noise, times the vertex alpha, times the clamped sprite alpha.
    float alpha = saturate(sprite.a) * ((sprite.a - alphaNoise) * input.color.a);
    return float4(color * _ColourTint.rgb, alpha);
}

#endif
