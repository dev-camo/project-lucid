using HardlightProject;

namespace ProjectLucid.Tests.WorldSurfaceShared
{
    // Test-only descendants that make the protected SetUp of each shipped enemy navigation callable. Unity calls it
    // from Awake, which Edit Mode never reaches; the body that runs is the shipped one.
    public sealed class Enemies03ForwardNavigationProbe : EnemyNavigationForward
    {
        public void RunSetUp() => SetUp();
    }

    public sealed class Enemies03HomeRadiusNavigationProbe : EnemyNavigationHomeRadius
    {
        public void RunSetUp() => SetUp();
    }

    public sealed class Enemies03StaticNavigationProbe : EnemyNavigationStaticAndReturnAfterChase
    {
        public void RunSetUp() => SetUp();
    }

    public sealed class Enemies03SplineNavigationProbe : EnemyNavigationSpline
    {
        public void RunSetUp() => SetUp();
    }
}
