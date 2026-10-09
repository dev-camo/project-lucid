using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLInput.Runtime 0x02000064; both complete native owner methods.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class InputButton : BaseInputButton<KeyCode, ButtonBinding>
    {
        // 0x060001f1: type selection uses the original key, after mapping it.
        // ResolveButtonInput is the genuine callee whose body is native-inlined.
        protected override void UpdateButtonBinding(GameInput gameInput, IKeyProvider<KeyCode> keyProvider,
            IBaseInputKeySource<KeyCode> inputKeySource, InputType inputTypeOverride, int joystickIndex,
            IReadOnlyList<InputModifier> modifiers)
        {
            KeyCode keyCode = keyProvider.Key;
            KeyCode mappedKeyCode = InputUtilities.GetJoystickMappedKeyCode(keyCode, joystickIndex);
            InputType inputType = InputTypeResolver.ResolveButtonInput(joystickIndex, inputTypeOverride,
                InputUtilities.IsMouseKeyCode(keyCode));
            ProcessKey(gameInput, mappedKeyCode, inputKeySource, inputType, joystickIndex, modifiers);
        }

        public InputButton() { } // 0x060001f2: the genuine closed generic base.
    }
}
