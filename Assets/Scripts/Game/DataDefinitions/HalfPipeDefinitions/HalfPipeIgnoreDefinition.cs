using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime0x02000561: complete fieldless one-method owner.
    // Native ctor06001d25 delegates only to the real ScriptableObject base.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "HalfPipeIgnoreDefinition", menuName = "HardlightProject/DefinitionData/Definitions/HalfPipeIgnoreDefinition")]
    public class HalfPipeIgnoreDefinition : ScriptableObject
    {
        public HalfPipeIgnoreDefinition() { }
    }
}
