using Hardlight.UI.Binding;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original Game.Runtime 0x02000017; bodies translated from the 1.10.1 x86-64 binary.
    // The non-interactive dream power slot image shown beside a character.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetImageDreamPowerSlot : UIWidget
    {
        [SerializeField] private Animation m_animation;
        [SerializeField] private AnimationClip m_equipClip;
        [SerializeField] private AnimationClip m_unequipClip;
        [SerializeField] private AnimationClip m_enabledClip;
        [SerializeField] private AnimationClip m_disabledClip;
        public readonly Bindable<Sprite> Icon = new Bindable<Sprite>();
        public readonly Bindable<string> DebugName = new Bindable<string>();

        // 0x06000040: the icon loads asynchronously while the equip clip plays.
        public void Equip(DreamPowerDefinition definition)
        {
            definition.IconAsset.LoadAsync(sprite => Icon.Value = sprite);
            m_animation.Play(m_equipClip.name);
            DebugName.Value = string.Empty;
        }

        // 0x06000041: the loaded icon is not unloaded here.
        public void Unequip()
        {
            Icon.Value = null;
            m_animation.Play(m_unequipClip.name);
            DebugName.Value = string.Empty;
        }

        // 0x06000042
        public void Enable()
        {
            m_animation.Play(m_enabledClip.name);
        }

        // 0x06000043
        public void Disable()
        {
            m_animation.Play(m_disabledClip.name);
        }

        // 0x06000044
        public UIWidgetImageDreamPowerSlot() { }
    }
}
