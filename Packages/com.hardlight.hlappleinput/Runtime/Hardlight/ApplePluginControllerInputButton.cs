using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x0200000d. Every method body follows the complete ARM64 and x86-64 originals.
// Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ApplePluginControllerInputButton : BaseInputButton<GCControllerInputName, ApplePluginControllerBinding>
    {
        protected override void UpdateButtonBinding(
            GameInput gameInput,
            IKeyProvider<GCControllerInputName> keyProvider,
            IBaseInputKeySource<GCControllerInputName> inputKeySource,
            InputType inputTypeOverride,
            int joystickIndex,
            IReadOnlyList<InputModifier> modifiers)
        {
            InputType inputType = InputTypeResolver.ResolveButtonInput(joystickIndex, inputTypeOverride, false);
            GCControllerInputName key = keyProvider.Key;
            ProcessKey(gameInput, key, inputKeySource, inputType, joystickIndex, modifiers);
        }

        public ApplePluginControllerInputButton()
        {
        }
    }
}
