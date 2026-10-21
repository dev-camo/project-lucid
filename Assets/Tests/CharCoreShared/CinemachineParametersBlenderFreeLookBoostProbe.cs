using HardlightProject;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only subclass of CinemachineParametersBlender_FreeLookBoost exposing its protected curve evaluation.
    // It lives in this runtime test assembly because Editor-only assemblies cannot supply attachable components.
    public sealed class CinemachineParametersBlenderFreeLookBoostProbe : CinemachineParametersBlender_FreeLookBoost
    {
        public float RunEvaluateBlendCurve(float deltaTime) => EvaluateBlendCurve(deltaTime);
    }
}
