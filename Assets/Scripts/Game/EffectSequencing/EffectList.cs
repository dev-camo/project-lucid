using System;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original0200060a: complete one field and constructor-only API.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EffectList
    {
        public SequencedEffect[] Effects;
        // Implicit original06002084 invokes Object only; Effects remains null.
    }
}
