using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000002. Every method body follows the complete ARM64 and x86-64 originals.
// Apple service calls are preserved and remain unexecuted.
[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
public static class AppleInputUtilities
{
    private static readonly int s_gcControllerInputNameCount = Enum.GetValues(typeof(GCControllerInputName)).Length;
    private static readonly Dictionary<GameInput, Dictionary<int, Dictionary<GCControllerInputName, string>>>
        s_indexedGameInputMappedApplePluginControllerAxisLookup =
            new Dictionary<GameInput, Dictionary<int, Dictionary<GCControllerInputName, string>>>(
                s_gcControllerInputNameCount, HardlightEnumComparers.GameInputComparer);
    public static readonly int InputNameLength = Enum.GetValues(typeof(GCControllerInputName)).Length;

    public static string GetTrackingKeyGameInputMappedApplePluginControllerAxis(
        GameInput gameInput, int joystickIndex, GCControllerInputName inputName)
    {
        return InputUtilities.GetTrackingKey(gameInput, joystickIndex, inputName,
            s_indexedGameInputMappedApplePluginControllerAxisLookup,
            s_gcControllerInputNameCount, GCControllerInputNameEqualityComparer.GCControllerInputNameComparer);
    }
}
