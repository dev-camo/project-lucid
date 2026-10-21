using System;
using Cinemachine;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000282 (BasicCameraBlendParameters 0x02000283).
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineParametersBlender_Basic : CinemachineParametersBlender
    {
        [SerializeField]
        private BasicCameraBlendParameters m_parameters;

        // 0x06000de7
        private CinemachineVirtualCamera m_virtualCamera => VirtualCamera as CinemachineVirtualCamera;

        // 0x06000de8: a single transposer and composer from the virtual camera.
        protected override void InitialiseCinemachineComponents()
        {
            m_transposers = new CinemachineTransposer[1];
            m_transposers[0] = m_virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
            m_composers = new CinemachineComposer[1];
            m_composers[0] = m_virtualCamera.GetCinemachineComponent<CinemachineComposer>();
        }

        // 0x06000de9
        protected override BlendParameters GetBlendParameters() => m_parameters;

        // 0x06000dea
        protected override void Evaluate(ref CameraState state, float blendValue)
        {
            base.Evaluate(ref state, blendValue);
            Vector3 followOffset = BlendParameter(m_parameters.FollowOffset, blendValue);
            for (int i = 0; i < m_transposers.Length; i++)
            {
                if (m_parameters.FollowOffset.Active)
                    m_transposers[i].m_FollowOffset = followOffset;
            }
        }

        // 0x06000deb
        public CinemachineParametersBlender_Basic() { }

        // 0x02000283, ctor 0x06000dec
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        protected class BasicCameraBlendParameters : BlendParameters
        {
            public Parameter<Vector3> FollowOffset;

            public BasicCameraBlendParameters() { }
        }
    }
}
