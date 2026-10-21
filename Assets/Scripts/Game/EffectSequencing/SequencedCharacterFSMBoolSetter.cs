using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original 02000615: only the public constructor.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SequencedCharacterFSMBoolSetter : SequencedCharacterFSMValueSetter<bool>
    {
        public SequencedCharacterFSMBoolSetter() { }
    }
}
