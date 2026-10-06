using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class MetadataDefinition : UnityEngine.ScriptableObject
    {
        // Original Game.Runtime 0x06001d43.
        public abstract Hardlight.MetadataGroupKey MetadataGroupKey { get; }

        // Original Game.Runtime 0x06001d44.
        // Native performs only the empty original base-constructor chain.
        protected MetadataDefinition() { }

    }
}
