// Reconstructed from this release's decoded Metal programs and serialized passes.
// Original shader: HardLight/Hub/HL_Hub_Matcap_Unlit, a URP 14 Unlit Shader Graph (platform 14), transparent with
// depth writes. A view-space matcap under a dream gradient (two scrolling noise octaves pick the gradient UV and
// its opacity); vertex red times the opacity is the alpha, and a scrolling noise displaces the vertices along
// their normals by vertex green.
Shader "HardLight/Hub/HL_Hub_Matcap_Unlit"
{
    Properties
    {
        _Separator ("#", Float) = 0
        _Title_Surface_Inputs ("## Surface Inputs", Float) = 0
        _Opacity ("-- Opacity", Range(0, 1)) = 1
        _Separator_1 ("# (1)", Float) = 0
        _Title_Matcap ("## Matcap", Float) = 0
        [NoScaleOffset]_Matcap_Texture ("-- Matcap Texture", 2D) = "white" {}
        _Matcap_Push_Border ("-- Matcap Push Border", Float) = 0
        _Separator_2 ("# (2)", Float) = 0
        _Title_Dream_Noise ("## Dream Noise", Float) = 0
        _Dream_UV_Speed_1 ("-- Dream UV Tiling", Float) = 1
        _Dream_UV_Speed ("-- Dream UV Speed", Vector) = (0,0,0,0)
        _Dream_Gradient_Offset ("-- Dream Gradient Offset", Float) = 1
        _Dream_Scrolling_Amount ("-- Dream Opacity", Range(0, 1)) = 0
        _Separator_3 ("# (3)", Float) = 0
        _Title_Vertex_Displacement ("## Vertex Displacement", Float) = 0
        _Vertex_Displacement_UV_Scale ("-- Vertex Displacement UV Scale", Float) = 0.1
        _Vertex_Displacement_UV_Speed ("-- Vertex Displacement UV Speed", Float) = 0.05
        _Vertex_Displacement_Intensity ("-- Vertex Displacement Intensity", Range(0, 1)) = 0
        _Separator_4 ("# (4)", Float) = 0
        [NoScaleOffset][NonModifiableTextureData]_HLSubSgVertexDisplacementTexture_649c8a79f0c04a6785cb1ef073b96b53_Texture_4082694086_Texture2D ("Texture2D", 2D) = "white" {}
        [NoScaleOffset][NonModifiableTextureData]_Texture2DAsset_d39bff2dfe9a47cba322697e35ffd3f9_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
        [NoScaleOffset][NonModifiableTextureData]_SampleTexture2D_3b9f3ee02411490484da157e5c0b5f78_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
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
            float4 _HLSubSgVertexDisplacementTexture_649c8a79f0c04a6785cb1ef073b96b53_Texture_4082694086_Texture2D_TexelSize;
            float4 _SampleTexture2D_3b9f3ee02411490484da157e5c0b5f78_Texture_1_Texture2D_TexelSize;
            float4 _Texture2DAsset_d39bff2dfe9a47cba322697e35ffd3f9_Out_0_Texture2D_TexelSize;
            float _Separator_2;
            float _Title_Surface_Inputs;
            float _Matcap_Push_Border;
            float4 _Matcap_Texture_TexelSize;
            float _Dream_Scrolling_Amount;
            float _Separator_1;
            float _Title_Dream_Noise;
            float _Separator;
            float _Dream_UV_Speed_1;
            float _Separator_3;
            float _Vertex_Displacement_UV_Scale;
            float _Vertex_Displacement_UV_Speed;
            float _Title_Vertex_Displacement;
            float _Vertex_Displacement_Intensity;
            float _Separator_4;
            float _Title_Matcap;
            float4 _Dream_UV_Speed;
            float _Dream_Gradient_Offset;
            float _Opacity;
        CBUFFER_END

        // Game-scaled shader time published by the time manager.
        float _HLShaderTimeGame;

        // The graph's immutable textures: the vertex displacement noise, the dream noise and the dream gradient.
        TEXTURE2D(_HLSubSgVertexDisplacementTexture_649c8a79f0c04a6785cb1ef073b96b53_Texture_4082694086_Texture2D);
        TEXTURE2D(_Texture2DAsset_d39bff2dfe9a47cba322697e35ffd3f9_Out_0_Texture2D);
        TEXTURE2D(_SampleTexture2D_3b9f3ee02411490484da157e5c0b5f78_Texture_1_Texture2D);
        SAMPLER(sampler_SampleTexture2D_3b9f3ee02411490484da157e5c0b5f78_Texture_1_Texture2D);
        TEXTURE2D(_Matcap_Texture);
        SAMPLER(sampler_Matcap_Texture);
        // The graph's inline sampler: linear, repeat.
        SAMPLER(SamplerState_Linear_Repeat);

        // The displacement noise's red at UV0 times the scale, plus game time times the speed (on both axes), read
        // at level 0, times the intensity, pushes the vertex along its object normal by vertex green.
        float3 MatcapPositionOS(float3 positionOS, float3 normalOS, float4 uv0, float4 color)
        {
            float2 uv = uv0.xy * _Vertex_Displacement_UV_Scale + _HLShaderTimeGame * _Vertex_Displacement_UV_Speed;
            float noise = SAMPLE_TEXTURE2D_LOD(_HLSubSgVertexDisplacementTexture_649c8a79f0c04a6785cb1ef073b96b53_Texture_4082694086_Texture2D,
                SamplerState_Linear_Repeat, uv, 0).x;
            return positionOS + color.g * (noise * _Vertex_Displacement_Intensity) * normalOS;
        }
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
                output.positionWS = TransformObjectToWorld(MatcapPositionOS(input.positionOS, input.normalOS, input.uv0, input.color));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv0 = input.uv0;
                output.color = input.color;
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
                // The matcap UV: r = cross(view, normal) in view space, (r.y, -r.x) * push border + 0.5. As in the
                // original programs, the normal is the row vector times the inverse view (the inverse view's
                // transpose); that equals the view rotation only for a rigid camera.
                float3 viewWS = unity_OrthoParams.w == 0.0 ? normalize(_WorldSpaceCameraPos - input.positionWS) : UNITY_MATRIX_V[2].xyz;
                float3 viewVS = normalize(mul((float3x3)UNITY_MATRIX_V, viewWS));
                float3 normalVS = mul(normalize(input.normalWS), (float3x3)UNITY_MATRIX_I_V);
                float3 r = cross(viewVS, normalVS);
                float3 matcap = SAMPLE_TEXTURE2D(_Matcap_Texture, sampler_Matcap_Texture, float2(r.y, -r.x) * _Matcap_Push_Border + 0.5).rgb;
                // The dream noise: two octaves at UV0 times the tiling (the second offset by (0.3, 0.2)), scrolled
                // by game time (speed xy and zw), summed; times the gradient offset it is the gradient UV, and its red
                // times the dream opacity weights the gradient over the matcap.
                float4 uv = float4(input.uv0.xy * _Dream_UV_Speed_1, input.uv0.xy * _Dream_UV_Speed_1 + float2(0.3, 0.2)) + _HLShaderTimeGame * _Dream_UV_Speed;
                float2 noise = SAMPLE_TEXTURE2D(_Texture2DAsset_d39bff2dfe9a47cba322697e35ffd3f9_Out_0_Texture2D, SamplerState_Linear_Repeat, uv.xy).xy
                    + SAMPLE_TEXTURE2D(_Texture2DAsset_d39bff2dfe9a47cba322697e35ffd3f9_Out_0_Texture2D, SamplerState_Linear_Repeat, uv.zw).xy;
                float3 gradient = SAMPLE_TEXTURE2D(_SampleTexture2D_3b9f3ee02411490484da157e5c0b5f78_Texture_1_Texture2D,
                    sampler_SampleTexture2D_3b9f3ee02411490484da157e5c0b5f78_Texture_1_Texture2D, noise * _Dream_Gradient_Offset).rgb;
                outColor = float4(lerp(matcap, gradient, noise.x * _Dream_Scrolling_Amount), saturate(input.color.r * _Opacity));
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
                float4 uv0 : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(MatcapPositionOS(input.positionOS, input.normalOS, input.uv0, input.color));
                output.color = input.color;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return input.positionCS.zzzz;
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
