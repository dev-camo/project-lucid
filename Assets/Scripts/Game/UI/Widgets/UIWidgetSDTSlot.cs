using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009c9: one objective slot, shown filled while ActivateImage is true.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetSDTSlot : UIWidget
    {
        public readonly Bindable<bool> ActivateImage = new Bindable<bool>();

        // 06003860 (0x6A2460)
        public void Activate() => ActivateImage.Value = true;

        // 06003861 (0x6A2490)
        public void Deactivate() => ActivateImage.Value = false;

        // 06003862 (0x6A24B0): the binding is only written on a change.
        public void SetSlotActive(bool active)
        {
            if (ActivateImage.Value != active)
                ActivateImage.Value = active;
        }

        public UIWidgetSDTSlot() { }
    }
}
