using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLInput.Runtime 0x02000063; both complete native owner methods.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class InputAxis : BaseInputAxis<string, AxisBinding>
    {
        // 0x060001ef: capture the authored axis once. The mapped axis feeds the
        // source; the tracking key deliberately uses the captured original axis.
        protected override void UpdateAxisBinding(GameInput gameInput, IAxisProvider<string> axisProvider,
            IBaseInputAxisSource<string> inputAxisSource, InputType inputType, int joystickIndex,
            IReadOnlyList<InputModifier> modifiers)
        {
            string axis = axisProvider.Axis;
            string mappedAxis = InputUtilities.GetJoystickMappedAxis(axis, joystickIndex);
            string trackingKey = InputUtilities.GetTrackingKeyGameInputMappedAxis(gameInput, joystickIndex, axis);
            ProcessAxis(gameInput, mappedAxis, inputAxisSource, trackingKey, inputType, joystickIndex, modifiers);
        }

        public InputAxis() { } // 0x060001f0: the genuine closed generic base.
    }
}
