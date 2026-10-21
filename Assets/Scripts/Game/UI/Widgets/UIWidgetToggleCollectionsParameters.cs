using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009d1; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetToggleCollectionsParameters : UIWidgetToggleParameters
    {
        public readonly int DisplayNumber;

        // 0x0600387e (0x6A32C0): the label is string.Empty, passed to the (string, Action<bool>) base overload.
        public UIWidgetToggleCollectionsParameters(Action<bool> callback, int displayNumber = 0) : base(string.Empty, callback)
        {
            DisplayNumber = displayNumber;
        }
    }
}
