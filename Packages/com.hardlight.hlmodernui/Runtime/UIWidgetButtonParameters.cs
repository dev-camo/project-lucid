using System;
using Hardlight.Enums;
using Hardlight.Localisation;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIWidgetButtonParameters : IUIWidgetParameters
    {
        public readonly string Label;
        public readonly Action Callback;

        // Original 060001ad: Object base call, Label write, then Callback write.
        public UIWidgetButtonParameters(string label, Action callback)
        {
            Label = label;
            Callback = callback;
        }

        // Original 060001ae calls the real StringTable.GetString AFTER the Object base call.
        // Do not chain through the string constructor: that moves translation ahead of base.
        public UIWidgetButtonParameters(Strings label, Action callback)
        {
            Label = StringTable.GetString(label);
            Callback = callback;
        }
    }
}
