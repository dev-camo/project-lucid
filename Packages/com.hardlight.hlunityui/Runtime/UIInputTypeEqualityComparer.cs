using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityUI.Runtime 0x02000068; ordinary, nonsealed reference type.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIInputTypeEqualityComparer : IEqualityComparer<UIInputType>
    {
        // Original 0x060002fb; compare the exact Int32 payload for any enum value.
        public bool Equals(UIInputType a, UIInputType b) => a == b;

        // Original 0x060002fc; the input payload itself is the signed hash.
        public int GetHashCode(UIInputType a) => (int)a;

        // Original 0x060002fd; only Object's constructor runs.
        public UIInputTypeEqualityComparer() { }
    }
}
