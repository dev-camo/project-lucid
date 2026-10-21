using System;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 020008af; bodies 0x62EDA0..0x62F043 (1.10.1 x86-64).
    // One row of the debug collectables list: the collectable's name and X-of-Y count, with the increase and
    // decrease actions handed in by UIContainerDebugCollectables.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerDebugCollectableModifier : UIContainer
    {
        public readonly Bindable<string> FormatXOfY = new Bindable<string>();
        public readonly Bindable<string> Name = new Bindable<string>();
        public readonly Bindable<int> Collected = new Bindable<int>();
        public readonly Bindable<int> Total = new Bindable<int>();
        private Action m_onIncrease;
        private Action m_onDecrease;

        // 0600322b
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            UIContainerDebugCollectableModifierParameters modifierParameters =
                parameters.GetAs<UIContainerDebugCollectableModifierParameters>();
            FormatXOfY.Value = StringTable.GetString(Strings.FORMAT_X_OF_Y);
            Name.Value = modifierParameters.Name;
            Collected.Value = modifierParameters.Collected;
            Total.Value = modifierParameters.Total;
            m_onIncrease = modifierParameters.OnIncrease;
            m_onDecrease = modifierParameters.OnDecrease;
        }

        // 0600322c
        public void Action_OnIncrease() => m_onIncrease?.Invoke();

        // 0600322d
        public void Action_OnDecrease() => m_onDecrease?.Invoke();

        // 0600322e
        public void UpdateCollected(int amount) => Collected.Value = amount;

        // 0600322f
        public UIContainerDebugCollectableModifier() { }
    }
}
