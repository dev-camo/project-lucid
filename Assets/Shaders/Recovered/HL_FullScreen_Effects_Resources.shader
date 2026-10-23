// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: Hidden/HardLight/RenderFeature/HL_FullScreen_Effects, a URP 14 Fullscreen Shader Graph
// (platform 14). Its inputs other than the flow map are globals set by the fullscreen effect feature.
// This is the Resources copy (Assets/Shader in the export), kept as its own asset so its material keeps
// its original GUID binding. It shares HL_FullScreen_Effects.hlsl with the bundled copy.
Shader "Hidden/HardLight/RenderFeature/HL_FullScreen_Effects"
{
    Properties
    {
        _Separator ("#", Float) = 0
        _Title_Fade_To_Colour ("## Fade To Colour", Float) = 0
        _Separator_1 ("# (1)", Float) = 0
        _Title_Dream_Vignette ("## Dream Vignette", Float) = 0
        [NoScaleOffset]_Vignette_Flow_Map_Texture ("-- Vignette Flow Map Texture", 2D) = "linearGrey" {}
        _Separator_2 ("# (2)", Float) = 0
        _Title_Edge_Distortion ("## Edge Distortion", Float) = 0
        _Separator_3 ("# (3)", Float) = 0
        _Title_Speed_Lines ("## Speed Lines", Float) = 0
        _Separator_4 ("# (4)", Float) = 0
        [HideInInspector][NoScaleOffset]unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalFullscreenSubTarget" }

        HLSLINCLUDE
        #pragma target 3.5
        #include "HL_FullScreen_Effects.hlsl"
        ENDHLSL

        Pass
        {
            Name "DrawProcedural"
            Blend One Zero
            ZTest Off
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex VertDrawProcedural
            #pragma fragment FullscreenEffects
            ENDHLSL
        }

        Pass
        {
            Name "Blit"
            Blend One Zero
            ZTest Off
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex VertBlit
            #pragma fragment FullscreenEffects
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.Rendering.Fullscreen.ShaderGraph.FullscreenShaderGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
