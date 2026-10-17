using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000005. Every method body follows the complete ARM64 and x86-64 originals.
// Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GCControllerInputNameEqualityComparer : IEqualityComparer<GCControllerInputName>
    {
        public static readonly GCControllerInputNameEqualityComparer GCControllerInputNameComparer =
            new GCControllerInputNameEqualityComparer();

        public bool Equals(GCControllerInputName a, GCControllerInputName b)
        {
            return a == b;
        }

        public int GetHashCode(GCControllerInputName a)
        {
            return (int)a;
        }

        public GCControllerInputNameEqualityComparer()
        {
        }
    }
}
