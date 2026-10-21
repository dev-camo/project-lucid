using System;
using Cinemachine;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000284 (FreeLookBlendParameters 0x02000285).
    [RequireComponent(typeof(CinemachineFreeLook))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CinemachineParametersBlender_FreeLook : CinemachineParametersBlender
    {
        [SerializeField]
        private FreeLookBlendParameters m_parameters;
        private static readonly int NumberOfRigs = CinemachineFreeLook.RigNames.Length;

        // 0x06000ded
        private CinemachineFreeLook m_freeLookCamera => VirtualCamera as CinemachineFreeLook;

        // 0x06000dee: one transposer and composer per rig, in rig order.
        protected override void InitialiseCinemachineComponents()
        {
            m_transposers = new CinemachineTransposer[NumberOfRigs];
            m_composers = new CinemachineComposer[NumberOfRigs];
            for (int i = 0; i < NumberOfRigs; i++)
            {
                CinemachineVirtualCamera rig = m_freeLookCamera.GetRig(i);
                m_transposers[i] = rig.GetCinemachineComponent<CinemachineTransposer>();
                m_composers[i] = rig.GetCinemachineComponent<CinemachineComposer>();
            }
        }

        // 0x06000def
        protected override BlendParameters GetBlendParameters() => m_parameters;

        // 0x06000df0
        protected override void Evaluate(ref CameraState state, float blendValue)
        {
            base.Evaluate(ref state, blendValue);
            BlendRigOrbit(m_parameters.TopRigOrbit, 0, blendValue);
            BlendRigOrbit(m_parameters.MiddleRigOrbit, 1, blendValue);
            BlendRigOrbit(m_parameters.BottomRigOrbit, 2, blendValue);
            BlendRigTrackedOffset(m_parameters.TopAimTrackedOffset, 0, blendValue);
            BlendRigTrackedOffset(m_parameters.MiddleAimTrackedOffset, 1, blendValue);
            BlendRigTrackedOffset(m_parameters.BottomAimTrackedOffset, 2, blendValue);
        }

        // 0x06000df1: both orbit components follow the sign and magnitude of the blend value.
        private void BlendRigOrbit(Parameter<CinemachineFreeLook.Orbit> blendParameter, int rigNumber, float blendValue)
        {
            if (!blendParameter.Active) return;
            CinemachineFreeLook.Orbit orbit = new CinemachineFreeLook.Orbit(
                LerpWithNegative(blendParameter.StartValue.m_Height, blendParameter.PositiveEndValue.m_Height,
                    blendParameter.NegativeEndValue.m_Height, blendValue),
                LerpWithNegative(blendParameter.StartValue.m_Radius, blendParameter.PositiveEndValue.m_Radius,
                    blendParameter.NegativeEndValue.m_Radius, blendValue));
            m_freeLookCamera.m_Orbits[rigNumber] = orbit;
        }

        // 0x06000df2: the original gates every rig's write on the top rig's Active flag.
        private void BlendRigTrackedOffset(Parameter<Vector3> aimTrackedOffset, int rigNumber, float blendValue)
        {
            if (!aimTrackedOffset.Active) return;
            Vector3 trackedOffset = BlendParameter(aimTrackedOffset, blendValue);
            if (m_parameters.TopAimTrackedOffset.Active)
                m_composers[rigNumber].m_TrackedObjectOffset = trackedOffset;
        }

        // 0x06000df3
        public CinemachineParametersBlender_FreeLook() { }

        // 0x02000285, ctor 0x06000df5
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        protected class FreeLookBlendParameters : BlendParameters
        {
            public Parameter<CinemachineFreeLook.Orbit> TopRigOrbit;
            public Parameter<CinemachineFreeLook.Orbit> MiddleRigOrbit;
            public Parameter<CinemachineFreeLook.Orbit> BottomRigOrbit;
            public Parameter<Vector3> TopAimTrackedOffset;
            public Parameter<Vector3> MiddleAimTrackedOffset;
            public Parameter<Vector3> BottomAimTrackedOffset;

            public FreeLookBlendParameters() { }
        }
    }
}
