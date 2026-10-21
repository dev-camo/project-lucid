using Hardlight.UI.Binding;
using Hardlight.Utils;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Hardlight
{
    // Original Game.Runtime 0x02000015; bodies translated from the 1.10.1 x86-64 binary.
    // A dream power slot button on the character select screen.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIWidgetButtonDreamPowerSlot : UIInteractable
    {
        [SerializeField] private Button m_button;
        [SerializeField] private UITransitionAnimation m_transition;
        [SerializeField] private AnimationClip m_equipClip;
        [SerializeField] private AnimationClip m_unequipClip;
        [SerializeField] private UIWidgetNavigation m_uiWidgetNavigation;
        [SerializeField, Header("Audio")] private HLAudioSfxComponentButton m_sfxComponentButton;
        [SerializeField, HashEnum(null)] private HLAudioClipIdentifier m_clipEquipped;
        [SerializeField, HashEnum(null)] private HLAudioClipIdentifier m_clipUnequipped;
        [SerializeField, HashEnum(null)] private HLAudioClipIdentifier m_clipDisabled;
        public readonly Bindable<Sprite> Icon = new Bindable<Sprite>();
        public readonly Bindable<string> DebugName = new Bindable<string>();
        public readonly Bindable<bool> ValidForEquip = new Bindable<bool>();

        public bool IsEquipped { get; private set; }
        public UIWidgetNavigation UIWidgetNavigation => m_uiWidgetNavigation;

        // 0x06000034/5: the transition is queued first (when present), then the button and the inherited flag.
        public override bool Interactable
        {
            get => base.Interactable;
            set
            {
                if (m_transition != null)
                    m_transition.QueueTransitionToState(value ? UITransitionState.Normal : UITransitionState.Disabled);
                m_button.interactable = value;
                base.Interactable = value;
            }
        }

        // 0x06000036: goes through the virtual accessors and only writes a change.
        public void SetInteractable(bool interactable)
        {
            if (Interactable != interactable)
                Interactable = interactable;
        }

        // 0x06000037
        public void Action_PublishUIMessage(UIModernEvent eventId)
        {
            var exchange = ProcessManager.GetSystemAutoCreate<MessageExchangeBoundCallbackArg<UIModernMessage>>();
            var message = new UIModernMessage(UIModernEventType.OnClick);
            exchange.PublishMessage(in message, in eventId, MessageBroadcastType.Both);
        }

        // 0x06000038: the icon loads asynchronously; the animation is queued without waiting for it.
        public void Equip(DreamPowerDefinition definition)
        {
            IsEquipped = true;
            definition.IconAsset.LoadAsync(sprite => Icon.Value = sprite);
            m_transition.QueueCustomAnimation(m_equipClip);
            DebugName.Value = string.Empty;
        }

        // 0x06000039: the loaded icon is not unloaded here.
        public void Unequip()
        {
            Icon.Value = null;
            m_transition.QueueCustomAnimation(m_unequipClip);
            DebugName.Value = string.Empty;
            IsEquipped = false;
        }

        // 0x0600003a: only a Bought slot that is not already equipped can take a power.
        public void SetSlotState(UIDreamPowerStoreItemState itemState)
        {
            bool validForEquip = !IsEquipped && itemState == UIDreamPowerStoreItemState.Bought;
            ValidForEquip.Value = validForEquip;
            // Native stores the clip identifier directly (SetClip inlined).
            m_sfxComponentButton.SetClip(validForEquip ? m_clipEquipped : IsEquipped ? m_clipUnequipped : m_clipDisabled);
        }

        // 0x0600003b
        public UIWidgetButtonDreamPowerSlot() { }
    }
}
