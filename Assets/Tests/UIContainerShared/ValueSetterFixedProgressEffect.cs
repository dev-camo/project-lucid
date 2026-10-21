using HardlightProject;

namespace ProjectLucid.Tests.UIContainerShared
{
    // Test-only SequencedEffect whose Progress is the value a case sets, and which counts how often it is read.
    // Editor-only assemblies cannot supply attachable components.
    public sealed class ValueSetterFixedProgressEffect : SequencedEffect
    {
        public float Value;
        public int Reads;

        public override float Progress
        {
            get
            {
                Reads++;
                return Value;
            }
        }
    }
}
