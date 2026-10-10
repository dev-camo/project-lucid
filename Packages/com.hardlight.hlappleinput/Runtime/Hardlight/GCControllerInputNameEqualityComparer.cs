using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000005. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class GCControllerInputNameEqualityComparer : System.Collections.Generic.IEqualityComparer<Apple.GameController.Controller.GCControllerInputName>
    {
        public static readonly Hardlight.GCControllerInputNameEqualityComparer GCControllerInputNameComparer = new GCControllerInputNameEqualityComparer();

        // Original 0x06000005; complete ARM64 and x86-64 bodies retained.
        public System.Boolean Equals(Apple.GameController.Controller.GCControllerInputName a, Apple.GameController.Controller.GCControllerInputName b)
        {
            return a == b;
        }

        // Original 0x06000006; complete ARM64 and x86-64 bodies retained.
        public System.Int32 GetHashCode(Apple.GameController.Controller.GCControllerInputName a)
        {
            return (int)a;
        }

        // Original 0x06000007; complete ARM64 and x86-64 bodies retained.
        public GCControllerInputNameEqualityComparer()
        {
        }

    }
}
