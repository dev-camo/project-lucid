using HardlightProject;

namespace ProjectLucid.Tests.WorldSurfaceShared
{
    // Test-only descendant that makes the protected SetUp of the shipped EnemyNavigationStaticAndReturnAfterChase callable. Unity calls it from
    // Awake, which Edit Mode never reaches; the body that runs is the shipped one.
    public sealed class EnemyNavigationStaticProbe : EnemyNavigationStaticAndReturnAfterChase
    {
        public void RunSetUp() => SetUp();
    }
}
