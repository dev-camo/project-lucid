using System;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLModernUI.Runtime 0200001d, its one original constructor calls UnityEvent<bool>.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIToggleEvent : UnityEvent<bool>
    {
        public UIToggleEvent() { }
    }
}
