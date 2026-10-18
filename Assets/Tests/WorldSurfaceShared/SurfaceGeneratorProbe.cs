using HardlightProject;

namespace ProjectLucid.Tests.WorldSurfaceShared
{
    // Test-only concrete SurfaceGenerator, so the abstract owner's constructor can run on an
    // attached component; Editor-only assemblies cannot supply attachable components.
    public sealed class SurfaceGeneratorProbe : SurfaceGenerator
    {
    }
}
