using HardlightProject;

namespace ProjectLucid.Tests.UIContainerShared
{
    // Test-only concrete UIContainerMissionBase: the original base is abstract and no shipped type derives from it
    // that the mission-screen tests could attach without its own services. Editor-only assemblies cannot supply
    // attachable components.
    public sealed class MissionBaseProbeContainer : UIContainerMissionBase
    {
    }
}
