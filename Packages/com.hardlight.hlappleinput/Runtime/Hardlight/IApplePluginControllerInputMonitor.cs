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
    public interface IApplePluginControllerInputMonitor : IBaseInputMonitor
    {
        bool TryGetButtonInputNames(GameInput gameInput, int joystickIndex,
            out IReadOnlyList<GCControllerInputName> inputNames);
    }
}
