using Cinemachine;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only subclass of CinemachineParametersBlender_FreeLook that builds the protected
    // FreeLookBlendParameters row set (the case stores it in the private serialized field) and exposes the
    // protected members a case drives. It lives in this runtime test assembly because Editor-only assemblies
    // cannot supply attachable components.
    public sealed class CinemachineParametersBlenderFreeLookProbe : CinemachineParametersBlender_FreeLook
    {
        public struct OrbitRow
        {
            public bool Active;
            public CinemachineFreeLook.Orbit Negative, Start, Positive;
        }

        public struct OffsetRow
        {
            public bool Active;
            public Vector3 Negative, Start, Positive;
        }

        private static Parameter<CinemachineFreeLook.Orbit> Orbit(OrbitRow row) => new Parameter<CinemachineFreeLook.Orbit>
        {
            Active = row.Active, NegativeEndValue = row.Negative, StartValue = row.Start, PositiveEndValue = row.Positive
        };

        private static Parameter<Vector3> Offset(OffsetRow row) => new Parameter<Vector3>
        {
            Active = row.Active, NegativeEndValue = row.Negative, StartValue = row.Start, PositiveEndValue = row.Positive
        };

        public object NewParameters(OrbitRow top, OrbitRow middle, OrbitRow bottom, OffsetRow topOffset, OffsetRow middleOffset, OffsetRow bottomOffset) =>
            new FreeLookBlendParameters
            {
                TopRigOrbit = Orbit(top), MiddleRigOrbit = Orbit(middle), BottomRigOrbit = Orbit(bottom),
                TopAimTrackedOffset = Offset(topOffset), MiddleAimTrackedOffset = Offset(middleOffset), BottomAimTrackedOffset = Offset(bottomOffset)
            };

        public object RunGetBlendParameters() => GetBlendParameters();

        public void UseComponents(CinemachineTransposer[] transposers, CinemachineComposer[] composers)
        {
            m_transposers = transposers;
            m_composers = composers;
        }

        public CinemachineTransposer[] Transposers => m_transposers;
        public CinemachineComposer[] Composers => m_composers;

        public void RunInitialise() => InitialiseCinemachineComponents();

        public void RunEvaluate(ref CameraState state, float blendValue) => Evaluate(ref state, blendValue);
    }
}
