using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x0200000d. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Serializable]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class ApplePluginControllerInputButton : Hardlight.BaseInputButton<Apple.GameController.Controller.GCControllerInputName,Hardlight.ApplePluginControllerBinding>
    {
        // Original 0x06000020; complete ARM64 and x86-64 bodies retained.
        protected override void UpdateButtonBinding(Hardlight.GameInput gameInput, Hardlight.IKeyProvider<Apple.GameController.Controller.GCControllerInputName> keyProvider, Hardlight.IBaseInputKeySource<Apple.GameController.Controller.GCControllerInputName> inputKeySource, Hardlight.InputType inputTypeOverride, System.Int32 joystickIndex, System.Collections.Generic.IReadOnlyList<Hardlight.InputModifier> modifiers)
        {
            InputType inputType = InputTypeResolver.ResolveButtonInput(joystickIndex, inputTypeOverride, false);
            GCControllerInputName key = keyProvider.Key;
            ProcessKey(gameInput, key, inputKeySource, inputType, joystickIndex, modifiers);
        }

        // Original 0x06000021; complete ARM64 and x86-64 bodies retained.
        public ApplePluginControllerInputButton()
        {
        }

    }
}
