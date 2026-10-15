using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime0x02000420 is genuinely fieldless. Its real
    // base is plain ScriptableObject; there is no authored GUID base/field.
    [CreateAssetMenu(fileName = "HashedGroupConfiguration", menuName = "HardlightProject/DefinitionData/HashedGroupConfiguration")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HashedGroupConfiguration : ScriptableObject
    {
        // 0x06001859; ARM64 0x774704 forwards to ScriptableObject0x26d8528.
        public HashedGroupConfiguration() { }
    }
}
