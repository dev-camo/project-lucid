using HardlightProject;

namespace ProjectLucid.Tests.UIContainerShared
{
    // Test-only concrete UIContainerMissionParametersBase, whose original constructor is protected.
    public sealed class MissionBaseProbeParameters : UIContainerMissionParametersBase
    {
        public MissionBaseProbeParameters(MissionState missionState) : base(missionState)
        {
        }
    }
}
