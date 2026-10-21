using HardlightProject;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only subclass of CinemachineParametersBlender_FreeLookBoost exposing its protected curve evaluation.
    public sealed class CinemachineParametersBlenderFreeLookBoostProbe : CinemachineParametersBlender_FreeLookBoost
    {
        public float RunEvaluateBlendCurve(float deltaTime) => EvaluateBlendCurve(deltaTime);
    }
}
