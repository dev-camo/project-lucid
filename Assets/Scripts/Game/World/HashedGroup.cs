using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original 02000a80 is genuinely fieldless and has this one base-only constructor.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class HashedGroup : MonoBehaviour
    {
        protected HashedGroup() { }
    }
}
