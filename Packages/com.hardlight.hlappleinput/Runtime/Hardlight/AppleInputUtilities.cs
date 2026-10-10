using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000002. Apple service calls are preserved and remain unexecuted.
[Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
[Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
public static class AppleInputUtilities
{
    private static readonly System.Int32 s_gcControllerInputNameCount = Enum.GetValues(typeof(GCControllerInputName)).Length;
    private static readonly System.Collections.Generic.Dictionary<Hardlight.GameInput,System.Collections.Generic.Dictionary<System.Int32,System.Collections.Generic.Dictionary<Apple.GameController.Controller.GCControllerInputName,System.String>>> s_indexedGameInputMappedApplePluginControllerAxisLookup = new Dictionary<GameInput, Dictionary<int, Dictionary<GCControllerInputName, string>>>(s_gcControllerInputNameCount, HardlightEnumComparers.GameInputComparer);
    public static readonly System.Int32 InputNameLength = Enum.GetValues(typeof(GCControllerInputName)).Length;

    // Original 0x06000001; complete ARM64 and x86-64 bodies retained.
    public static System.String GetTrackingKeyGameInputMappedApplePluginControllerAxis(Hardlight.GameInput gameInput, System.Int32 joystickIndex, Apple.GameController.Controller.GCControllerInputName inputName)
    {
        return InputUtilities.GetTrackingKey(gameInput, joystickIndex, inputName,
            s_indexedGameInputMappedApplePluginControllerAxisLookup,
            s_gcControllerInputNameCount, GCControllerInputNameEqualityComparer.GCControllerInputNameComparer);
    }

}
