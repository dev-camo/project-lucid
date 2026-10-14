using HardlightProject;

namespace ProjectLucid.Tests.CutsceneShared
{
    // Test-only concrete host for the abstract original Cutscene; no original subclass
    // is restored yet. Editor-only assemblies cannot supply attachable components.
    public sealed class OwnedCutsceneProbe : Cutscene
    {
    }
}
