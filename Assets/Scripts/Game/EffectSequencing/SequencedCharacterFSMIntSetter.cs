using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original 02000616: only the public constructor.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SequencedCharacterFSMIntSetter : SequencedCharacterFSMValueSetter<int>
    {
        public SequencedCharacterFSMIntSetter() { }
    }
}
