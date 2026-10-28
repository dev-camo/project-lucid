// Reconstructed from this release's decoded Metal programs and serialized pass.
// Original shader: HardLight/Sky/HL_Sky_Cubemap, platform 14, gpuProgramID 51434.
// Derived from the supplied release's Metal programs and serialized render states.
Shader "HardLight/Sky/HL_Sky_Cubemap"
{
    Properties
    {
        _Separator ("#", Float) = 0
        _Title_Base_Cubemap ("## Base Cubemap", Float) = 0
        [NoScaleOffset] _Cubemap_Texture ("Cubemap Texture", Cube) = "" {}
        _Cubemap_LOD ("LOD", Range(0, 10)) = 0
        _NOTE_LOD_Acts_like_a_blur ("!NOTE LOD: Acts like a blur", Float) = 0
        _Cubemap_Rotation ("Rotation", Range(0, 360)) = 0
        [ToggleUI] _Flip_Y_Axis ("Flip Y Axis?", Float) = 0
        _Separator_1 ("# (1)", Float) = 0
        [HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl ("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        // The original is an opaque mesh pass with depth writes disabled.
        // Its direction comes from world normals, rather than camera position.
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Unlit" "DisableBatching"="False" "ShaderGraphShader"="true" "ShaderGraphTargetId"="UniversalUnlitSubTarget" }
        Pass
        {
            Name "Universal Forward"
            Blend One Zero
            ZTest LEqual
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Title_Base_Cubemap;
                float _Cubemap_LOD;
                float _Cubemap_Rotation;
                float _NOTE_LOD_Acts_like_a_blur;
                float _Separator_1;
                float _Separator;
                float _Flip_Y_Axis;
            CBUFFER_END
            TEXTURECUBE(_Cubemap_Texture);
            SAMPLER(sampler_Cubemap_Texture);

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
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normal = input.normalWS / sqrt(dot(input.normalWS, input.normalWS));
                float sine, cosine;
                sincos(_Cubemap_Rotation * 0.0174532924, sine, cosine);
                float3 direction = float3(cosine * normal.x + sine * normal.z,
                                          normal.y * (1.0 - 2.0 * _Flip_Y_Axis),
                                          -sine * normal.x + cosine * normal.z);
                color = float4(SAMPLE_TEXTURECUBE_LOD(_Cubemap_Texture, sampler_Cubemap_Texture,
                                                     direction, _Cubemap_LOD).rgb, 1.0);
                #if defined(_WRITE_RENDERING_LAYERS)
                renderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
                #endif
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}
