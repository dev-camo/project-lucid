using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000010. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    public interface IApplePluginControllerInputMonitor : Hardlight.IBaseInputMonitor
    {
        // Original 0x0600002e; genuine abstract declaration.
        System.Boolean TryGetButtonInputNames(Hardlight.GameInput gameInput, System.Int32 joystickIndex, out System.Collections.Generic.IReadOnlyList<Apple.GameController.Controller.GCControllerInputName> inputNames);

    }
}
