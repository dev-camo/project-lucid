using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x0200000a. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public sealed class HLAppleInputPathConstants : UnityEngine.MonoBehaviour
    {
        public const System.String ModuleMenuBase = "Hardlight/HLAppleInput/";
        public const System.String ControllerProviders = "ControllerProviders/";
        public const System.String InputBindings = "Input Bindings/";

        // Original 0x0600001a; complete ARM64 and x86-64 bodies retained.
        public HLAppleInputPathConstants()
        {
        }

    }
}
