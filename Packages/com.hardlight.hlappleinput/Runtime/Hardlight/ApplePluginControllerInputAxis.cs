using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x0200000c. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Serializable]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class ApplePluginControllerInputAxis : Hardlight.BaseInputAxis<Apple.GameController.Controller.GCControllerInputName,Hardlight.ApplePluginControllerBinding>
    {
        // Original 0x0600001e; complete ARM64 and x86-64 bodies retained.
        protected override void UpdateAxisBinding(Hardlight.GameInput gameInput, Hardlight.IAxisProvider<Apple.GameController.Controller.GCControllerInputName> axisProvider, Hardlight.IBaseInputAxisSource<Apple.GameController.Controller.GCControllerInputName> inputAxisSource, Hardlight.InputType inputType, System.Int32 joystickIndex, System.Collections.Generic.IReadOnlyList<Hardlight.InputModifier> modifiers)
        {
            GCControllerInputName axis = axisProvider.Axis;
            string trackingKey = AppleInputUtilities.GetTrackingKeyGameInputMappedApplePluginControllerAxis(gameInput, joystickIndex, axis);
            ProcessAxis(gameInput, axis, inputAxisSource, trackingKey, inputType, joystickIndex, modifiers);
        }

        // Original 0x0600001f; complete ARM64 and x86-64 bodies retained.
        public ApplePluginControllerInputAxis()
        {
        }

    }
}
