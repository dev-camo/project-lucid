using HardlightProject;

namespace ProjectLucid.Tests.UIContainerShared
{
    // Test-only concrete UIContainerMissionBase: the original base is abstract and no shipped type derives from it, so the
    // tests attach this concrete probe; editor-only assemblies cannot supply attachable components.
    public sealed class MissionBaseProbeContainer : UIContainerMissionBase
    {
    }
}
