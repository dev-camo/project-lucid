// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/UI/HL_UI_Worldspace_AlwaysOnTop, a hand-written UI shader in the style of Unity's
// UI/Default, drawn without depth testing so world-space UI stays on top. The sprite (plus the texture sample add)
// times the vertex colour and tint, its alpha masked by the clip rectangle in object space, optionally alpha-clipped.
Shader "HardLight/UI/HL_UI_Worldspace_AlwaysOnTop"
{
    Properties
    {
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Toggle(UNITY_UI_ALPHACLIP)]_UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Pass
        {
            Name "Default"
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Off
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            // The stereo keywords the shipped (CG) program was built with.
            #pragma multi_compile _ STEREO_INSTANCING_ON UNITY_SINGLE_PASS_STEREO STEREO_MULTIVIEW_ON STEREO_CUBEMAP_RENDER_ON
            #pragma multi_compile _ UNITY_UI_ALPHACLIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Not in a UnityPerMaterial buffer: the shipped programs read these as plain uniforms.
            float4 _Color;
            float4 _TextureSampleAdd;
            float4 _ClipRect;
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv0 : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 positionOS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = input.uv0;
                output.positionOS = input.positionOS;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                // The original fragments sample _MainTex with no bias input. Core.hlsl's SAMPLE_TEXTURE2D would add
                // _GlobalMipBias.x, so the platform sample is used directly.
                float4 color = (PLATFORM_SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) + _TextureSampleAdd) * input.color;
                // UnityGet2DClipping: 1 inside the clip rectangle (inclusive), 0 outside.
                float2 inside = step(_ClipRect.xy, input.positionOS.xy) * step(input.positionOS.xy, _ClipRect.zw);
                color.a *= inside.x * inside.y;
                #if defined(UNITY_UI_ALPHACLIP)
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDHLSL
        }
    }
}
