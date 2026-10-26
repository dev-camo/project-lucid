// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/UI/HL_UI_Dream_Effect, a hand-written URP UI shader (platform 14). Two scrolling noise
// lookups index a dream gradient that colours the sprite (or replaces its colour), and eat into its alpha.
// This is the Assets/Shader copy, kept as its own asset so its materials keep their original GUID binding.
// It shares HL_UI_Dream_Effect.hlsl with the bundled copy.
Shader "HardLight/UI/HL_UI_Dream_Effect"
{
    Properties
    {
        [HideInInspector]_MainTex ("Sprite", 2D) = "white" {}
        [NoScaleOffset][Header(Dream Effect)][Space(4)]_Dream_Gradient ("Dream Gradient", 2D) = "white" {}
        [HDR]_ColourTint ("Colour Tint", Color) = (1,1,1,1)
        _Dream_Noise ("Dream Noise", 2D) = "white" {}
        [Toggle]_UseScreenSpaceUVs ("Use ScreenSpace UVs?", Float) = 0
        _Dream_Gradient_Offset ("Dream Gradient Offset", Float) = 1
        _Dream_Gradient_Multiplier ("Dream Gradient Multiplier", Range(0, 2)) = 1
        _Dream_Noise_Speed ("Dream Noise Speed", Range(0, 0.25)) = 0.1
        _Alpha_Dream_Noise_Amount ("Alpha Dream Noise Amount", Range(0, 1)) = 1
        [HideInInspector]_Alpha_Dream_Noise_Contrast ("Alpha Contrast", Range(0, 1)) = 0.75
        _Use_Sprite_RGB_Texture ("Sprite RGB = 0, Sprite Alpha = 1", Range(0, 1)) = 1
        [Header(Blending)][Enum(UnityEngine.Rendering.BlendMode)]_BlendSrc ("Blend mode Source", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)]_BlendDst ("Blend mode Destination", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "UI_Dream_Effect"
            Tags { "LightMode"="UniversalForward" }
            Blend [_BlendSrc] [_BlendDst]
            ZTest [unity_GUIZTestMode]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "HL_UI_Dream_Effect.hlsl"
            ENDHLSL
        }
    }
}
