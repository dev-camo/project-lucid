Shader "Hidden/ProjectLucid/FullscreenVertexContract"
{
    Properties
    {
        _ProbeUseBlit ("Use blit entry", Float) = 0
        _ProbeVertexId ("Original vertex id", Float) = 0
        _ProbeOutput ("Output", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend One Zero
            ZTest Always
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ProbeVert
            #pragma fragment ProbeFrag
            #include "Assets/Shaders/Recovered/HL_FullScreen_Effects.hlsl"

            float _ProbeUseBlit;
            float _ProbeVertexId;
            float _ProbeOutput;

            Varyings ProbeVert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                if (_ProbeUseBlit == 1.0) output = VertBlit((uint)_ProbeVertexId);
                else output = VertDrawProcedural((uint)_ProbeVertexId);
                // Keep the actual entry's payload; only raster coverage belongs to this diagnostic.
                float2 corner = float2((vertexID << 1) & 2, vertexID & 2);
                output.positionCS = float4(corner * 2.0 - 1.0, 0.5, 1.0);
                return output;
            }

            float4 ProbeFrag(Varyings input) : SV_Target
            {
                if (_ProbeOutput == 1.0) return input.uv;
                // These admission modes expose bindings, not reconstructed game computations.
                if (_ProbeOutput == 2.0) return float4(_WorldSpaceCameraPos, 1.0);
                if (_ProbeOutput == 3.0) return UNITY_MATRIX_V[2];
                if (_ProbeOutput == 4.0) return mul(UNITY_MATRIX_I_VP, float4(0, 0, 1, 1));
                if (_ProbeOutput == 5.0) return unity_OrthoParams;
                return input.viewDirectionWS;
            }
            ENDHLSL
        }
    }
}
