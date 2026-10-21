using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009da; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIWidgetTogglePaginationParameters : UIWidgetToggleParameters
    {
        public bool IsNew { get; }

        public bool Locked { get; }

        public bool UnlockSeen { get; }

        public bool IsVisible { get; }

        // 0x060038bc (0x6A52D0): the base constructor takes the string label overload, called with string.Empty.
        public UIWidgetTogglePaginationParameters(Action<bool> callback, bool isNew, bool locked, bool unlockSeen, bool isVisible)
            : base(string.Empty, callback)
        {
            IsNew = isNew;
            Locked = locked;
            UnlockSeen = unlockSeen;
            IsVisible = isVisible;
        }
    }
}
