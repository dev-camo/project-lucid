using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009ce; bodies translated from the 1.10.1 x86-64 binary.
    // A slider pip tinted by a bound colour.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIWidgetSliderPip : UIWidget
    {
        public readonly Bindable<Color> Colour = new Bindable<Color>();

        // 0x06003876 (0x6A30F0)
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            Colour.Value = parameters.GetAs<UIWidgetSliderPipParameters>().Colour;
        }

        // 0x06003877 (0x6A3160)
        public UIWidgetSliderPip() { }
    }
}
