// Shared source for both shipped copies of Hidden/HardLight/RenderFeature/HL_FullScreen_Effects
// (the bundled asset and the Resources asset), reconstructed from the decoded Metal programs.
#ifndef HL_FULLSCREEN_EFFECTS_INCLUDED
#define HL_FULLSCREEN_EFFECTS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Globals published by the fullscreen effect feature and the time manager.
float _FlipY;
float _Edge_Distortion_Amount;
float _Vignette_Amount;
float _Vignette_Flow_Map_Speed;
float _Vignette_Opacity;
float _Vignette_Flow_Map_Strength;
float4 _Fade_To_Color;
float _Fade_To_Colour_Amount;
float _Vignette_Colour_Boost;
float _Speed_Lines_Scale;
float _HLShaderTimePlayerAnim;
float4 _Vignette_Tint;
float _Speed_Lines_Opacity;
float _Speed_Lines_Amount;
float _Speed_Lines_Speed;
float4 _Speed_Lines_Colour_Tint;
float _Vignette_UV_Scale;

TEXTURE2D(_Vignette_Flow_Map_Texture);
TEXTURE2D(_Vignette_Texture);
TEXTURE2D(_Speed_Lines_Packed_Noise_Texture);
TEXTURE2D_X(_BlitTexture);
// The graph's inline sampler states.
SAMPLER(SamplerState_Linear_Clamp);
SAMPLER(SamplerState_Linear_Repeat);

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float4 uv : TEXCOORD0;
};

// Both templates give the screen UV, flipped vertically unless _FlipY is set.
Varyings FullscreenVaryings(float2 positionNDC)
{
    Varyings output;
    output.positionCS = float4(positionNDC, 1.0, 1.0);
    float2 uv = positionNDC * 0.5 + 0.5;
    output.uv = float4(uv.x, _FlipY < 0.5 ? 1.0 - uv.y : uv.y, 1.0, 1.0);
    return output;
}

// The decoded fragment: edge distortion of the scene, the flow-mapped dream vignette,
// polar speed lines, then the fade to colour.
half4 FullscreenEffects(Varyings input) : SV_Target
{
    float2 uv = input.uv.xy;
    float2 centred = uv - 0.5;

    // Speed lines: polar coordinates (atan2(x, y), radial scale and length scale) scroll two
    // taps of the packed noise, sampled with the screen UV's own gradients. The platform macro keeps
    // them raw: URP's SAMPLE_TEXTURE2D_GRAD would scale them by _GlobalMipBias.y.
    float radius = length(centred) * _Speed_Lines_Scale;
    float2 polar = float2(radius * 2.0, atan2(centred.x, centred.y) * _Speed_Lines_Amount * 0.159235656);
    float time = _HLShaderTimePlayerAnim * _Speed_Lines_Speed;
    float2 dx = ddx(uv), dy = ddy(uv);
    float noiseA = PLATFORM_SAMPLE_TEXTURE2D_GRAD(_Speed_Lines_Packed_Noise_Texture, SamplerState_Linear_Repeat, (polar + float2(time, -0.2 * time)) * 0.2, dx, dy).r;
    float noiseB = PLATFORM_SAMPLE_TEXTURE2D_GRAD(_Speed_Lines_Packed_Noise_Texture, SamplerState_Linear_Repeat, polar + float2(-time, 0.0), dx, dy).r;
    float noise = pow(saturate(noiseA * noiseB * 1.2), 0.1);
    float ramp = radius * 6.0 - 0.1;
    float lines = saturate(noise - (1.0 - radius * 2.0));
    lines *= saturate(ramp * 10.0) * noise + ramp * 1.7;
    lines = saturate(lines * 5.0) * _Speed_Lines_Opacity;

    // Edge distortion pushes the scene lookup outward by the fourth power of the distance.
    float distanceSq = dot(centred, centred);
    uint2 pixel = uint2((centred * (distanceSq * distanceSq * _Edge_Distortion_Amount) + uv) * _ScreenSize.xy);
    float3 scene = LOAD_TEXTURE2D_X(_BlitTexture, pixel).rgb;

    // Dream vignette: two flow-map phases, half a cycle apart, crossfaded. Core.hlsl's SAMPLE_TEXTURE2D
    // adds the single _GlobalMipBias.x the original applies.
    float2 flow = (SAMPLE_TEXTURE2D(_Vignette_Flow_Map_Texture, SamplerState_Linear_Repeat, uv).xy - 0.5) * -2.0 * _Vignette_Flow_Map_Strength;
    float phaseA = frac(_HLShaderTimePlayerAnim * _Vignette_Flow_Map_Speed + 0.5);
    float phaseB = frac(_Vignette_Flow_Map_Speed * _HLShaderTimePlayerAnim);
    float4 vignetteA = SAMPLE_TEXTURE2D(_Vignette_Texture, SamplerState_Linear_Clamp, _Vignette_UV_Scale * (uv + flow * phaseA - 0.5) + 0.5);
    float4 vignetteB = SAMPLE_TEXTURE2D(_Vignette_Texture, SamplerState_Linear_Clamp, _Vignette_UV_Scale * (uv + flow * phaseB - 0.5) + 0.5);
    float4 vignette = lerp(vignetteB, vignetteA, abs(phaseB * 2.0 - 1.0));
    float edge = saturate(vignette.a - (1.0 - _Vignette_Amount));
    edge = min(edge * edge * (3.0 - 2.0 * edge), 1.0) * _Vignette_Opacity;
    float3 colour = lerp(scene, vignette.rgb * _Vignette_Colour_Boost * _Vignette_Tint.rgb, edge);

    colour += _Speed_Lines_Colour_Tint.rgb * lines;
    return half4(lerp(colour, _Fade_To_Color.rgb, _Fade_To_Colour_Amount), 1.0);
}

// The full-screen triangle from the vertex id.
Varyings VertDrawProcedural(uint vertexID : SV_VertexID)
{
    return FullscreenVaryings(float2((vertexID << 1) & 2, vertexID & 2) * 2.0 - 1.0);
}

// The blit quad from the vertex id: corners (0,1), (0,0), (1,0), (1,1).
Varyings VertBlit(uint vertexID : SV_VertexID)
{
    uint x = vertexID >> 1;
    uint y = (uint)(1 - (int)(vertexID & 1) - (int)x) & 1;
    return FullscreenVaryings(float2(x, y) * 2.0 - 1.0);
}

#endif
