// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Gameplay/HL_Gameplay_Dream_Orb, a URP 14 Unlit Shader Graph (platform 14),
// transparent. A matcap (or diffuse) base, overlaid by a scrolling dream-noise colour ramp where the matcap
// is clear, then by a scrolling detail mask on the second UV; the alpha is the matcap alpha.
Shader "HardLight/Gameplay/HL_Gameplay_Dream_Orb"
{
    Properties
    {
        _NOTE_Swap_between_matcap_and_diffuse_texture_input ("!NOTE Swap between matcap and diffuse texture input", Float) = 0
        _REF_Use_Matcap_Bool ("!REF _Use_Matcap_Bool", Float) = 0
        _Title_Matcap ("## Matcap [_Use_Matcap_Bool]", Range(0, 2)) = 1
        [ToggleUI]_Use_Matcap_Bool ("Use Matcap?", Float) = 0
        _Alpha_Amount ("-- Alpha Amount", Range(0, 1)) = 1
        [NoScaleOffset]_Matcap_Tex ("-- Matcap Texture [_Use_Matcap_Bool]", 2D) = "white" {}
        _Matcap_Border_Push_Amount ("-- Matcap Border Push Amount [_Use_Matcap_Bool]", Float) = 0.5
        _Title_Diffuse ("## Diffuse", Range(0, 2)) = 1
        _Diff_Texture ("-- Diff Texture", 2D) = "black" {}
        _Separator ("#", Float) = 0
        _Title_Dream_Noise ("## Dream Noise", Range(0, 2)) = 1
        [NoScaleOffset]_Dream_Noise_Colour_Ramp ("-- Dream Noise Colour Ramp", 2D) = "white" {}
        _Dream_Noise_UV_Tiling_X ("-- Dream Noise UV Tiling X", Float) = 1
        _Dream_Noise_UV_Tiling_Y ("-- Dream Noise UV Tiling Y", Float) = 1
        _Dream_Noise_UV_Speed_X ("-- Dream Noise UV Speed X", Range(0, 1)) = 0
        _Dream_Noise_UV_Speed_Y ("-- Dream Noise UV Speed Y", Range(0, 1)) = 0
        _Dream_Noise_Opacity ("-- Dream Noise Opacity", Range(0, 1)) = 1
        _Separator_1 ("# (1)", Float) = 0
        _Title_Detail_Mask ("## Detail Mask", Range(0, 2)) = 1
        _NOTE_Swap_between_matcap_and_diffuse_texture_input_1 ("!NOTE Uses Alpha channel of Diff Texture", Float) = 0
        _Detail_Mask_UV_Speed_X ("-- Detail Mask UV Speed X", Range(0, 1)) = 0
        _Detail_Mask_UV_Speed_Y ("-- Detail Mask UV Speed Y", Range(0, 1)) = 0
        [HDR]_Detail_Mask_Colour ("-- Detail Mask Colour", Color) = (1,1,1,0)
        _Separator_2 ("# (2)", Float) = 0
        [NoScaleOffset][NonModifiableTextureData]_SampleTexture2D_cb50bfe028ef4036ac3206ebb85e8866_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
        [NoScaleOffset][NonModifiableTextureData]_SampleTexture2D_a7a00c3b42cd409295935933b2dd6395_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
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
            float4 _SampleTexture2D_a7a00c3b42cd409295935933b2dd6395_Texture_1_Texture2D_TexelSize;
            float4 _SampleTexture2D_cb50bfe028ef4036ac3206ebb85e8866_Texture_1_Texture2D_TexelSize;
            float _Title_Matcap;
            float4 _Matcap_Tex_TexelSize;
            float _Title_Diffuse;
            float _Title_Dream_Noise;
            float _Matcap_Border_Push_Amount;
            float _Dream_Noise_UV_Tiling_X;
            float _Dream_Noise_UV_Tiling_Y;
            float _Dream_Noise_UV_Speed_Y;
            float _Separator_2;
            float _Dream_Noise_UV_Speed_X;
            float _Title_Detail_Mask;
            float _Detail_Mask_UV_Speed_X;
            float _Detail_Mask_UV_Speed_Y;
            float _Dream_Noise_Opacity;
            float4 _Detail_Mask_Colour;
            float4 _Diff_Texture_TexelSize;
            float4 _Diff_Texture_ST;
            float _NOTE_Swap_between_matcap_and_diffuse_texture_input_1;
            float _Use_Matcap_Bool;
            float _REF_Use_Matcap_Bool;
            float _Separator_1;
            float _Separator;
            float _NOTE_Swap_between_matcap_and_diffuse_texture_input;
            float _Alpha_Amount;
            float4 _Dream_Noise_Colour_Ramp_TexelSize;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest LEqual
            ZWrite On
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

            TEXTURE2D(_Matcap_Tex);
            SAMPLER(sampler_Matcap_Tex);
            TEXTURE2D(_Diff_Texture);
            SAMPLER(sampler_Diff_Texture);
            TEXTURE2D(_Dream_Noise_Colour_Ramp);
            SAMPLER(sampler_Dream_Noise_Colour_Ramp);
            TEXTURE2D(_SampleTexture2D_a7a00c3b42cd409295935933b2dd6395_Texture_1_Texture2D);
            SAMPLER(sampler_SampleTexture2D_a7a00c3b42cd409295935933b2dd6395_Texture_1_Texture2D);
            TEXTURE2D(_SampleTexture2D_cb50bfe028ef4036ac3206ebb85e8866_Texture_1_Texture2D);
            SAMPLER(sampler_SampleTexture2D_cb50bfe028ef4036ac3206ebb85e8866_Texture_1_Texture2D);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
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
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            void Frag(Varyings input, out float4 outColor : SV_Target0
                #if defined(_WRITE_RENDERING_LAYERS)
                , out float4 outRenderingLayers : SV_Target1
                #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = input.uv0.xy;

                // Matcap: a view-space cross of the view direction and the normal, pushed out from the centre.
                float3 viewVS = normalize(mul((float3x3)UNITY_MATRIX_V, GetWorldSpaceNormalizeViewDir(input.positionWS)));
                float3 normalVS = mul(normalize(input.normalWS), (float3x3)UNITY_MATRIX_I_V);
                float2 matcapUV = float2(viewVS.z * normalVS.x - viewVS.x * normalVS.z, viewVS.z * normalVS.y - viewVS.y * normalVS.z) *
                                  _Matcap_Border_Push_Amount + 0.5;
                float4 matcap = SAMPLE_TEXTURE2D(_Matcap_Tex, sampler_Matcap_Tex, matcapUV);
                float3 diffuse = SAMPLE_TEXTURE2D(_Diff_Texture, sampler_Diff_Texture, uv * _Diff_Texture_ST.xy + _Diff_Texture_ST.zw).rgb;
                float3 colour = lerp(diffuse, matcap.rgb, _Use_Matcap_Bool);

                // Dream noise: a scrolling, tiled noise (red and green) looks up the colour ramp, weighted by the
                // noise red where the matcap alpha is clear.
                float2 noiseUV = uv * float2(_Dream_Noise_UV_Tiling_X, _Dream_Noise_UV_Tiling_Y) +
                                 _HLShaderTimeGame * float2(_Dream_Noise_UV_Speed_X, _Dream_Noise_UV_Speed_Y);
                float2 noise = SAMPLE_TEXTURE2D(_SampleTexture2D_a7a00c3b42cd409295935933b2dd6395_Texture_1_Texture2D,
                    sampler_SampleTexture2D_a7a00c3b42cd409295935933b2dd6395_Texture_1_Texture2D, noiseUV).xy;
                float3 ramp = SAMPLE_TEXTURE2D(_Dream_Noise_Colour_Ramp, sampler_Dream_Noise_Colour_Ramp, noise).rgb;
                float noiseWeight = saturate(noise.x * (1.0 - matcap.a) * 5.0 * _Dream_Noise_Opacity);
                colour = lerp(colour, ramp, noiseWeight);

                // Detail mask (alpha), scrolling on the second UV: the colour less the detail colour, kept where the
                // mask is clear, plus the detail colour where it is set.
                float detail = SAMPLE_TEXTURE2D(_SampleTexture2D_cb50bfe028ef4036ac3206ebb85e8866_Texture_1_Texture2D,
                    sampler_SampleTexture2D_cb50bfe028ef4036ac3206ebb85e8866_Texture_1_Texture2D,
                    input.uv1.xy + _HLShaderTimeGame * float2(_Detail_Mask_UV_Speed_X, _Detail_Mask_UV_Speed_Y)).a;
                outColor.rgb = saturate(1.0 - detail) * (colour - _Detail_Mask_Colour.rgb * detail) + detail * _Detail_Mask_Colour.rgb;
                outColor.a = matcap.a * _Alpha_Amount;
                #if defined(_WRITE_RENDERING_LAYERS)
                outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZTest LEqual
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            float4 Frag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.zzzz; }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZTest LEqual
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5 DOTS_INSTANCING_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                return output;
            }
            float4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
