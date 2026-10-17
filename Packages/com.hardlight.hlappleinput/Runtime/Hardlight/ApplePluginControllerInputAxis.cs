using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x0200000c. Every method body follows the complete ARM64 and x86-64 originals.
namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ApplePluginControllerInputAxis : BaseInputAxis<GCControllerInputName, ApplePluginControllerBinding>
    {
        // Original 0x0600001e
        protected override void UpdateAxisBinding(GameInput gameInput,
            IAxisProvider<GCControllerInputName> axisProvider,
            IBaseInputAxisSource<GCControllerInputName> inputAxisSource, InputType inputType, int joystickIndex,
            IReadOnlyList<InputModifier> modifiers)
        {
            GCControllerInputName axis = axisProvider.Axis;
            string trackingKey = AppleInputUtilities.GetTrackingKeyGameInputMappedApplePluginControllerAxis(
                gameInput, joystickIndex, axis);
            ProcessAxis(gameInput, axis, inputAxisSource, trackingKey, inputType, joystickIndex, modifiers);
        }

        // Original 0x0600001f
        public ApplePluginControllerInputAxis()
        {
        }
    }
}
