// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Character/HL_Shockwave_nightmare, a URP 14 Unlit Shader Graph (platform 14),
// transparent. The vertices pushed along their normals; a scrolling diffuse under a fresnel rim whose edge is
// stepped and broken up by a scrolling noise; vertex red times the alpha is the alpha.
Shader "HardLight/Character/HL_Shockwave_nightmare"
{
    Properties
    {
        [HDR]_Diff_Colour_Tint ("-- Diff Colour Tint", Color) = (1,1,1,0)
        _Title_Surface_Inputs ("## Surface Inputs", Float) = 0
        _UV_Scrolling_Vertical ("-- UV Scrolling Vertical", Float) = 0
        _UV_Scrolling_Horizontal ("-- UV Scrolling Horizontal", Float) = 0
        _Diffuse_Texture ("-- Diff Texture", 2D) = "white" {}
        _Vertex_Push_Amount ("-- Vertex Push Amount", Float) = 1
        _Alpha ("-- Alpha", Float) = 1
        _Separator_0 ("#", Float) = 0
        _Separator_1 ("# (1)", Float) = 0
        _Title_Fresnel ("## Fresnel", Float) = 0
        _Fresnel_Amount ("-- Fresnel Amount", Float) = 1
        [HDR]_Fresnel_Color ("-- Fresnel Color", Color) = (0.854902,0,0.1702644,0)
        _Fresnel_Intensity ("-- Fresnel Intensity", Float) = 1
        _Fresnel_Opacity ("-- Fresnel Opacity", Range(0, 1)) = 1
        _Separator_2 ("# (2)", Float) = 0
        [NoScaleOffset][NonModifiableTextureData]_SampleTexture2D_fd8594977b2b47a0a26ae627707dfdfa_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
        [HideInInspector]_QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl ("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "UniversalMaterialType"="Unlit" "Queue"="Transparent" "DisableBatching"="False" "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalUnlitSubTarget" }

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

        // Shipped UnityPerMaterial member order, kept for SRP Batcher layout parity.
        CBUFFER_START(UnityPerMaterial)
            float4 _SampleTexture2D_fd8594977b2b47a0a26ae627707dfdfa_Texture_1_Texture2D_TexelSize;
            float _Title_Surface_Inputs;
            float4 _Diffuse_Texture_TexelSize;
            float4 _Diffuse_Texture_ST;
            float _Separator_0;
            float4 _Diff_Colour_Tint;
            float _Separator_1;
            float _Alpha;
            float4 _Fresnel_Color;
            float _Title_Fresnel;
            float _Fresnel_Intensity;
            float _Fresnel_Opacity;
            float _Vertex_Push_Amount;
            float _Separator_2;
            float _Fresnel_Amount;
            float _UV_Scrolling_Vertical;
            float _UV_Scrolling_Horizontal;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma shader_feature _ _SAMPLE_GI
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // The graph's immutable rim noise.
            TEXTURE2D(_SampleTexture2D_fd8594977b2b47a0a26ae627707dfdfa_Texture_1_Texture2D);
            SAMPLER(sampler_SampleTexture2D_fd8594977b2b47a0a26ae627707dfdfa_Texture_1_Texture2D);
            TEXTURE2D(_Diffuse_Texture);
            SAMPLER(sampler_Diffuse_Texture);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 uv0 : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float4 color : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // The vertex pushed along its object normal by the push amount.
                output.positionWS = TransformObjectToWorld(input.positionOS + input.normalOS * _Vertex_Push_Amount);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv0 = input.uv0;
                output.color = input.color;
                return output;
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // The view direction: towards the camera for perspective, the view matrix's z row for orthographic.
                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = unity_OrthoParams.w == 0.0 ? normalize(_WorldSpaceCameraPos - input.positionWS) : UNITY_MATRIX_V[2].xyz;
                float fresnel = pow(1.0 - saturate(dot(normalWS, normalize(viewWS))), _Fresnel_Amount) * _Fresnel_Intensity;
                // The rim: (1 - n.v) * 4 less the noise's blue (at UV0 * 2, scrolled up by frac(game time * 0.2))
                // times 3, rounded up and scaled by 1 / 3.88, saturated; it masks the fresnel and its colour.
                float noise = SAMPLE_TEXTURE2D(_SampleTexture2D_fd8594977b2b47a0a26ae627707dfdfa_Texture_1_Texture2D,
                    sampler_SampleTexture2D_fd8594977b2b47a0a26ae627707dfdfa_Texture_1_Texture2D, input.uv0.xy * 2.0 + float2(0.0, frac(_HLShaderTimeGame * 0.2))).z;
                float rim = saturate(ceil((1.0 - dot(normalWS, viewWS)) * 4.0 - noise * 3.0) * 0.257731944);
                float3 rimColour = rim * _Fresnel_Color.rgb;
                float weight = saturate(1.0 - rim * fresnel * _Fresnel_Opacity);
                // The diffuse UV: UV0 through the tiling and offset, scrolled by game time (horizontal, vertical),
                // then through the tiling and offset again, as the graph does.
                float2 diffuseUV = input.uv0.xy * _Diffuse_Texture_ST.xy + _Diffuse_Texture_ST.zw
                    + _HLShaderTimeGame * float2(_UV_Scrolling_Horizontal, _UV_Scrolling_Vertical);
                diffuseUV = diffuseUV * _Diffuse_Texture_ST.xy + _Diffuse_Texture_ST.zw;
                float3 diffuse = SAMPLE_TEXTURE2D(_Diffuse_Texture, sampler_Diffuse_Texture, diffuseUV).rgb;
                outColor = float4(lerp(rimColour, diffuse * _Diff_Colour_Tint.rgb, weight), saturate(input.color.r * _Alpha));
                #if defined(_WRITE_RENDERING_LAYERS)
                outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
                #endif
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
