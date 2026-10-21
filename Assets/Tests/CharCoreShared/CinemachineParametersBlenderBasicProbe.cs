using Cinemachine;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only subclass of CinemachineParametersBlender_Basic that builds the protected
    // BasicCameraBlendParameters (the case stores it in the private serialized field) and exposes the
    // protected members a case drives. It lives in this runtime test assembly because Editor-only
    // assemblies cannot supply attachable components.
    public sealed class CinemachineParametersBlenderBasicProbe : CinemachineParametersBlender_Basic
    {
        public object NewParameters(bool followOffsetActive, Vector3 negativeEnd, Vector3 start, Vector3 positiveEnd) =>
            new BasicCameraBlendParameters
            {
                FollowOffset = new Parameter<Vector3>
                {
                    Active = followOffsetActive, NegativeEndValue = negativeEnd, StartValue = start, PositiveEndValue = positiveEnd
                }
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
