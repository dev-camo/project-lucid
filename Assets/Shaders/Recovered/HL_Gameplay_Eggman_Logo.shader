// Original compiled shader: HardLight/Gameplay/HL_Gameplay_Eggman_Logo.
// Unity 2022.3.54f1, Metal platform 14, gpuProgramID 61652; source blobs 1/4/5.
// Derived from the supplied release's Metal programs and serialized render states.
Shader "HardLight/Gameplay/HL_Gameplay_Eggman_Logo"
{
    Properties
    {
        _Colour_A ("Colour A", Color) = (1, 0, 0, 1)
        _Colour_B ("Colour B", Color) = (0, 1, 0, 1)
        _Colour_C ("Colour C", Color) = (0, 0, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        [NoScaleOffset] _Mask ("Mask", 2D) = "white" {}
        [HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl ("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags
        {
            "DisableBatching"="False" "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent"
            "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalUnlitSubTarget"
            "UniversalMaterialType"="Unlit"
        }
        Pass
        {
            Name "Universal Forward"
            // Serialized rtSeparateBlend is false: this blend applies to every target.
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            BlendOp Add, Add
            ZClip On
            ZTest LEqual
            ZWrite Off
            Cull Back
            ColorMask RGBA
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            // URP's own pragmas raise the rendering-layer variant to target 4.5.
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Retain the original material-buffer order, including its texel-size field.
            CBUFFER_START(UnityPerMaterial)
                float4 _Colour_A;
                float4 _Colour_C;
                float4 _Colour_B;
                float _Alpha;
                float4 _Mask_TexelSize;
            CBUFFER_END
            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                // Blob 1 emits these varyings even though the surviving fragments
                // only consume UV0. Preserve its inverse-transpose normal transform.
                float3 normal = mul(input.normalOS, (float3x3)GetWorldToObjectMatrix());
                output.normalWS = normal * rsqrt(max(dot(normal, normal), 1.17549435e-38));
                return output;
            }

            void Frag(Varyings input, out float4 color : SV_Target0
                      #if defined(_WRITE_RENDERING_LAYERS)
                      , out float4 renderingLayers : SV_Target1
                      #endif
                      )
            {
                // Core.hlsl supplies _GlobalMipBias.x through SAMPLE_TEXTURE2D.
                // UV0 is used directly; the original has no _Mask_ST transform.
                float4 mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, input.uv.xy);
                float4 mixed = mask.r * _Colour_A;
                // The green channel occurs twice in blob 4/5. This is the original
                // nested interpolation, including its green-squared contribution.
                mixed = mad(mask.g, mad(_Colour_B, mask.g, -mixed), mixed);
                mixed = mad(mask.b, _Colour_C - mixed, mixed);
                color.rgb = mixed.rgb;
                color.a = mask.a * saturate(mixed.a + mixed.a) * _Alpha;
                #if defined(_WRITE_RENDERING_LAYERS)
                renderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
                #endif
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    Fallback "Hidden/Shader Graph/FallbackError"
}
