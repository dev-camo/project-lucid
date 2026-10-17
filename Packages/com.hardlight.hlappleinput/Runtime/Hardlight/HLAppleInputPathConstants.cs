using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x0200000a. Every method body follows the complete ARM64 and x86-64 originals.
// Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class HLAppleInputPathConstants : MonoBehaviour
    {
        public const string ModuleMenuBase = "Hardlight/HLAppleInput/";
        public const string ControllerProviders = "ControllerProviders/";
        public const string InputBindings = "Input Bindings/";

        public HLAppleInputPathConstants()
        {
        }
    }
}
