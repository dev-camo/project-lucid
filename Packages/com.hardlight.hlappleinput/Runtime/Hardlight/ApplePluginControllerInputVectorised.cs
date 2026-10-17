using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x0200000e. Every method body follows the complete ARM64 and x86-64 originals.
// Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ApplePluginControllerInputVectorised
        : BaseInputVectorisedGameInput<ApplePluginControllerInputBinding, ApplePluginControllerBindingData>
    {
        public ApplePluginControllerInputVectorised()
        {
        }
    }
}
