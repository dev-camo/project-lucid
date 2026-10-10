using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterBrain_Replay : CharacterBrain
    {
        // Original 0600130c is a genuine empty native body on both backends.
        public override void Close() { }

        // Original 0600130d delegates to the genuine CharacterBrain ctor.
        public CharacterBrain_Replay() : base() { }
    }
}
