using HardlightProject;

namespace ProjectLucid.Tests.WorldSurfaceShared
{
    // Test-only descendant that makes the protected SetUp of the shipped EnemyNavigationHomeRadius callable. Unity calls it from
    // Awake, which Edit Mode never reaches, and from the Editor OnValidate that AddComponent sends (0x578060, whose
    // CanValidate 0x6B1910 always answers true), so a new component is already initialised; ClearInitialised goes back
    // through the shipped protected setter so a case can watch SetUp mark it again. The bodies that run are the shipped ones.
    public sealed class EnemyNavigationHomeRadiusProbe : EnemyNavigationHomeRadius
    {
        public void RunSetUp() => SetUp();

        public void ClearInitialised() => IsInitialised = false;
    }
}
