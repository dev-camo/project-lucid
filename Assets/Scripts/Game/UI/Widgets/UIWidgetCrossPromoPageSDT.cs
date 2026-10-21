using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009af; bodies translated from the 1.10.1 x86-64 binary.
    // The game's cross promotion page: every accessor reads or writes a bindable.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIWidgetCrossPromoPageSDT : CrossPromoPageBase
    {
        public readonly Bindable<Sprite> Image = new Bindable<Sprite>();
        public readonly Bindable<string> Text = new Bindable<string>();
        public readonly Bindable<bool> IsReady = new Bindable<bool>();

        // 0x060037f0
        public override void SetSprite(Sprite sprite)
        {
            Image.Value = sprite;
        }

        // 0x060037f1
        public override Sprite GetSprite()
        {
            return Image.Value;
        }

        // 0x060037f2
        public override void SetText(string text)
        {
            Text.Value = text;
        }

        // 0x060037f3
        public override void SetReadyState(bool isReady)
        {
            IsReady.Value = isReady;
        }

        // 0x060037f4
        public override bool GetReadyState()
        {
            return IsReady.Value;
        }

        // 0x060037f5
        public UIWidgetCrossPromoPageSDT() { }
    }
}
