// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Character/HL_Char_Ghosted_Eye, hand-written HLSL (platform 14, blobs 1/3).
// One transparent pass: two eyes and their glints sampled from one mask texture, each eye at its own
// offset and scale, tinted by the vertex colour and faded towards a colour.
Shader "HardLight/Character/HL_Char_Ghosted_Eye"
{
    Properties
    {
        _Documentation_Link_Below ("## Documentation Link Below : HL_Char_Ghosted_Eye", Float) = 0
        _Heading_This_shader_is_linked_to_animations ("### ***This shader is linked to animations***", Float) = 0
        _Documentation_Link ("[https://hardlight.atlassian.net/wiki/spaces/HL/pages/8326158/SDT+-+HL+Char+Eye](https://hardlight.atlassian.net/wiki/spaces/HL/pages/8326158/SDT+-+HL+Char+Eye)", Float) = 0
        _Separator_0 ("#", Float) = 0
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Fade_To_Colour ("Fade To Colour", Range(0, 1)) = 0
        [HDR]_Fade_Colour ("Fade Colour", Color) = (1,1,1,1)
        [NoScaleOffset]_EyeTextureAlbedoAlpha ("Eye Texture Mask", 2D) = "white" {}
        [HDR]_EyeColor ("Eye Color", Color) = (0.1573993,1,0,1)
        [HDR]_GlintColour ("Glint Colour", Color) = (1.498039,1.498039,1.498039,1)
        _Vertex_Colour ("Vertex Colour", Range(0, 1)) = 0
        [HDR]_Eye_Background_Colour ("Fade Colour", Color) = (1,1,1,1)
        _Global_UV_Scale ("Global UV Scale", Float) = 1
        _Global_UV_Horizontal ("Global UV Horizontal", Float) = 1
        _Global_UV_Vertical ("Global UV Vertical", Float) = 1
        _Left_Eye_UV_Scale ("Left Eye UV Scale", Float) = 1
        _LeftGlintHorizontal ("Left Glint Horizontal", Float) = 0
        _LeftGlintVertical ("Left Glint Vertical", Float) = 0
        _LeftEyeHorizontal ("Left Eye Horizontal", Float) = 0
        _LeftEyeVertical ("Left Eye Vertical", Float) = 0
        _Right_Eye_UV_Scale ("Right Eye UV Scale", Float) = 1
        _RightGlintHorizontal ("Right Glint Horizontal", Float) = 0
        _RightGlintVertical ("Right Glint Vertical", Float) = 0
        _RightEyeHorizontal ("Right Eye Horizontal", Float) = 0
        _RightEyeVertical ("Right Eye Vertical", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Unlit" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
            CBUFFER_START(UnityPerMaterial)
                float _Documentation_Link_Below;
                float _Heading_This_shader_is_linked_to_animations;
                float _Documentation_Link;
                float _Alpha;
                float _Fade_To_Colour;
                float4 _Fade_Colour;
                float4 _EyeTextureAlbedoAlpha_ST;
                float4 _EyeColor;
                float4 _GlintColour;
                float _Vertex_Colour;
                float _Global_UV_Scale;
                float _Global_UV_Horizontal;
                float _Global_UV_Vertical;
                float _Left_Eye_UV_Scale;
                float _LeftGlintHorizontal;
                float _LeftGlintVertical;
                float _LeftEyeHorizontal;
                float _LeftEyeVertical;
                float _Right_Eye_UV_Scale;
                float _RightGlintHorizontal;
                float _RightGlintVertical;
                float _RightEyeHorizontal;
                float _RightEyeVertical;
                float4 _Eye_Background_Colour;
            CBUFFER_END

            TEXTURE2D(_EyeTextureAlbedoAlpha);
            SAMPLER(sampler_EyeTextureAlbedoAlpha);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _EyeTextureAlbedoAlpha_ST.xy + _EyeTextureAlbedoAlpha_ST.zw;
                output.color = input.color;
                return output;
            }

            // One eye-mask lookup: the UV moved by the offset and scaled about the texture centre.
            float4 SampleEye(float2 uv, float2 offset, float2 scale)
            {
                // Core.hlsl supplies _GlobalMipBias.x through SAMPLE_TEXTURE2D, the single bias the original applies.
                return SAMPLE_TEXTURE2D(_EyeTextureAlbedoAlpha, sampler_EyeTextureAlbedoAlpha, (uv - offset) * scale + 0.5);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 globalScale = _Global_UV_Scale * float2(_Global_UV_Horizontal, _Global_UV_Vertical);
                float2 rightScale = globalScale * _Right_Eye_UV_Scale;
                float2 leftScale = globalScale * _Left_Eye_UV_Scale;
                float2 rightEyeOffset = float2(_RightEyeHorizontal, _RightEyeVertical);
                float2 leftEyeOffset = float2(_LeftEyeHorizontal, _LeftEyeVertical);

                // The mask holds the iris in red, the eye background in green and the glint in blue.
                float2 rightEye = SampleEye(input.uv, rightEyeOffset, rightScale).xy;
                float2 leftEye = SampleEye(input.uv, leftEyeOffset, leftScale).xy;
                float glint = SampleEye(input.uv, rightEyeOffset + float2(_RightGlintHorizontal, _RightGlintVertical), rightScale).z +
                              SampleEye(input.uv, leftEyeOffset + float2(_LeftGlintHorizontal, _LeftGlintVertical), leftScale).z;
                float iris = rightEye.x * leftEye.x;
                float background = 1.0 - (rightEye.y + leftEye.y);

                // The lerps are unclamped, as in the original. _Vertex_Colour is unused: the vertex colour always tints.
                float3 color = lerp(iris * _EyeColor.rgb, _GlintColour.rgb, glint);
                color = lerp(color, iris * _Eye_Background_Colour.rgb, background);
                color = lerp(color * input.color.rgb, _Fade_Colour.rgb, _Fade_To_Colour);
                return float4(color, _Alpha);
            }
            ENDHLSL
        }
    }
    CustomEditor "Needle.MarkdownShaderGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
