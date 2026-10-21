using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009b2; bodies translated from the 1.10.1 x86-64 binary.
    // Shows a dream power's name and icon, or the empty state when no power is given.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetDreamPowerDescription : UIWidget
    {
        public readonly Bindable<bool> Empty = new Bindable<bool>();
        public readonly Bindable<Strings> Name = new Bindable<Strings>();
        public readonly Bindable<Sprite> Icon = new Bindable<Sprite>();
        private UIWidgetDreamPowerDescriptionParameters m_parameters;

        // 0x060037ff: the icon is retained here and released in OnDestroy.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIWidgetDreamPowerDescriptionParameters>();
            if (m_parameters.Definition != null)
            {
                Empty.Value = false;
                Name.Value = m_parameters.Definition.Name;
                m_parameters.Definition.IconAsset.LoadAsync(sprite => Icon.Value = sprite);
            }
            else
            {
                Empty.Value = true;
            }
        }

        // 0x06003800
        private void OnDestroy()
        {
            if (m_parameters != null && m_parameters.Definition != null)
                m_parameters.Definition.IconAsset.Unload();
        }

        // 0x06003801
        public UIWidgetDreamPowerDescription() { }
    }
}
