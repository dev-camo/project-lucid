using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009d0; bodies translated from the 1.10.1 x86-64 binary.
    // A tab toggle of the collections screen: its image follows the on/off state and its badge shows a count.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIWidgetToggleCollections : UIWidgetToggle
    {
        [SerializeField] private Image m_target;
        [SerializeField] private Sprite m_imageWhenOff;
        [SerializeField] private Sprite m_imageWhenOn;
        [SerializeField] private UIWidgetSDTAlertBadge m_alertBadge;

        // 0x0600387a (0x6A3200): the badge shows the count, never the alert mark.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            m_alertBadge.SetAlert(false, parameters.GetAs<UIWidgetToggleCollectionsParameters>().DisplayNumber);
        }

        // 0x0600387b (0x6A3280): the base callback runs first; the sprite is chosen by indexing the
        // adjacent off/on fields with the value.
        public override void Action_OnValueChanged(bool value)
        {
            base.Action_OnValueChanged(value);
            m_target.sprite = value ? m_imageWhenOn : m_imageWhenOff;
        }

        // 0x0600387c (0x6A3260)
        public void UpdateBadge(int displayNumber)
        {
            m_alertBadge.SetAlert(false, displayNumber);
        }

        // 0x0600387d (0x6A32B0)
        public UIWidgetToggleCollections() { }
    }
}
