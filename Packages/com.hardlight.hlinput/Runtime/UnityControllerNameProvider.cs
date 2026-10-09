using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UnityControllerNameProvider : BaseControllerProvider
    {
        public UnityControllerNameProvider(float controllerConnectionPollingRateInSeconds)
            : base(controllerConnectionPollingRateInSeconds) { } // 06000066
        public override IReadOnlyList<string> GetControllerNames() { return Input.GetJoystickNames(); } // 06000067
    }
}
