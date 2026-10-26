// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: Hidden/HardLight/Particle/HL_Particle_Dream_Ripple, a URP 14 Sprite Unlit Shader Graph
// (platform 14), transparent. A ring about the UV centre (a smoothstep band at the size, its width twice the
// thickness) magnifies the opaque scene colour behind it, faded in by a smoothstep of the ring and tinted by the vertex
// and renderer colour. The 2D and forward passes are identical.
Shader "Hidden/HardLight/Particle/HL_Particle_Dream_Ripple"
{
    Properties
    {
        _Size ("Size", Range(0, 0.3)) = 0.3
        _Thickness ("Thickness", Float) = 0.1
        _Magnification ("Magnification", Float) = -0.1
        [ToggleUI]_Debug_View ("Debug View?", Float) = 0
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
            float _Size;
            float _Thickness;
            float _Magnification;
            float _Debug_View;
        CBUFFER_END

        // The sprite renderer's colour, published by URP's 2D renderer (white elsewhere).
        float4 _RendererColor;

        TEXTURE2D(_CameraOpaqueTexture);
        SAMPLER(sampler_CameraOpaqueTexture);

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

        // The fragment's screen UV, as the decoded program derives it from the pixel position.
        float2 ScreenUV(float4 positionCS)
        {
            float y = _ProjectionParams.x < 0.0 ? _ScaledScreenParams.y - positionCS.y : positionCS.y;
            float2 screenUV = float2(positionCS.x, y) / _ScaledScreenParams.xy;
            #if UNITY_UV_STARTS_AT_TOP
            screenUV.y = 1.0 - screenUV.y;
            #endif
            return screenUV;
        }

        float4 Frag(Varyings input) : SV_Target
        {
            // The ring: a smoothstep from (frac(size) - thickness) to (frac(size) + thickness) of the distance from
            // the UV centre, s, shaped as s * (1 - s); its alpha is a smoothstep of the ring from 0 to 0.26.
            float size = frac(_Size);
            float2 offset = input.uv0.xy - 0.5;
            float ring = smoothstep(size - _Thickness, size + _Thickness, length(offset));
            ring = ring * (1.0 - ring);
            float alpha = smoothstep(0.0, 0.26, ring);
            // The Sprite target discards fully transparent fragments.
            if (alpha == 0.0) discard;
            // The opaque scene colour at the screen UV, both axes moved by the ring times the offset direction's X
            // times the magnification (as the graph wires it); the debug view shows the ring instead.
            float shift = ring * normalize(offset).x * _Magnification;
            float3 scene = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, ScreenUV(input.positionCS) + shift).rgb;
            float3 colour = _Debug_View != 0.0 ? ring.xxx : scene;
            return float4(colour, alpha) * input.color;
        }
        ENDHLSL

        Pass
        {
            Name "Sprite Unlit"
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            ENDHLSL
        }

        Pass
        {
            Name "Sprite Unlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
