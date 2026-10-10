using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x0200000e. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Serializable]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class ApplePluginControllerInputVectorised : Hardlight.BaseInputVectorisedGameInput<Hardlight.ApplePluginControllerInputBinding,Hardlight.ApplePluginControllerBindingData>
    {
        // Original 0x06000022; complete ARM64 and x86-64 bodies retained.
        public ApplePluginControllerInputVectorised()
        {
        }

    }
}
