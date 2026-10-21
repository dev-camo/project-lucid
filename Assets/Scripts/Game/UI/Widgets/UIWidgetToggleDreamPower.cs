using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009d2; bodies translated from the 1.10.1 x86-64 binary.
    // One dream power in the store grid: icon, cost, tier and the animation for its item state.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetToggleDreamPower : UIWidget
    {
        [SerializeField] private UIToggle m_toggle;
        [SerializeField] private UIToggleGroupElement m_groupElement;
        [SerializeField] private UITransitionAnimation m_transitionAnimation;
        [SerializeField] private AnimationClip m_lockedByArchetypeAnimation;
        [SerializeField] private AnimationClip m_lockedByProgression;
        [SerializeField] private AnimationClip m_purchasableAnimation;
        [SerializeField] private AnimationClip m_selectedAnimation;
        [SerializeField] private AnimationClip m_clearSelectedAnimation;
        [SerializeField] private AnimationClip m_availableAnimation;
        [SerializeField] private AnimationClip m_equippedAnimation;
        [SerializeField] private UIWidgetNavigation m_uiWidgetNavigation;
        [SerializeField] private UnityEvent m_onSelected;

        public readonly Bindable<Sprite> Icon = new Bindable<Sprite>();
        public readonly Bindable<string> DebugName = new Bindable<string>();
        public readonly Bindable<string> Cost = new Bindable<string>();
        public readonly Bindable<bool> ShowEquipped = new Bindable<bool>();
        public readonly Bindable<bool> ShowCost = new Bindable<bool>();
        public readonly Bindable<bool> ShowSelected = new Bindable<bool>();
        public readonly Bindable<Strings> TierName = new Bindable<Strings>();
        public readonly Bindable<string> TierIndex = new Bindable<string>();
        public readonly Bindable<Sprite> BackgroundImage = new Bindable<Sprite>();

        private UIWidgetToggleDreamPowerSlotParameters m_parameters;
        private DreamPowerStoreBandDefinition m_bandDefinition;
        private static readonly SystemRef<DreamPowerStoreManager> s_dreamPowerStoreManagerRef = ProcessManager.GetSystemRef<DreamPowerStoreManager>(null, true);

        // 0x0600387f (0x6A3320)
        public UIToggleGroupElement GroupElement => m_groupElement;

        // 0x06003880 (0x6A3330)
        public UIWidgetNavigation UIWidgetNavigation => m_uiWidgetNavigation;

        // 0x06003881 (0x6A3340): the tier index is one-based; the icon loads asynchronously and
        // the widget navigation is enabled only for an available dream power.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIWidgetToggleDreamPowerSlotParameters>();
            DreamPowerDefinition definition = m_parameters.Definition;
            DreamPowerStoreManager manager = s_dreamPowerStoreManagerRef.Get();
            m_bandDefinition = manager.GetBandDefinition(definition);
            TierName.Value = m_bandDefinition.Name;
            TierIndex.Value = (manager.GetBandIndex(definition) + 1).ToString();
            BackgroundImage.Value = m_bandDefinition.SlotLockedBackground.Load();
            DebugName.Value = string.Empty;
            definition.IconAsset.LoadAsync(sprite => Icon.Value = sprite);
            Cost.Value = definition.PurchaseCost.ToString();
            m_uiWidgetNavigation.enabled = m_parameters.Available;
            SetVisualState();
        }

        // 0x06003882 (0x6A35C0): clears the flags, then plays the animation of the item state. The
        // purchasable animation (with the cost) is also the fallback for an out-of-range state.
        private void SetVisualState()
        {
            ShowEquipped.Value = false;
            ShowCost.Value = false;
            m_transitionAnimation.QueueCustomAnimation(m_clearSelectedAnimation, null);
            switch (m_parameters.State)
            {
                case UIDreamPowerStoreItemState.LockedByProgression:
                    m_transitionAnimation.QueueCustomAnimation(m_lockedByProgression, null);
                    ShowSelected.Value = false;
                    break;
                case UIDreamPowerStoreItemState.LockedByArchetype:
                    m_transitionAnimation.QueueCustomAnimation(m_lockedByArchetypeAnimation, null);
                    ShowSelected.Value = false;
                    break;
                case UIDreamPowerStoreItemState.Bought:
                    m_transitionAnimation.QueueCustomAnimation(m_availableAnimation, null);
                    break;
                case UIDreamPowerStoreItemState.Equipped:
                    ShowEquipped.Value = true;
                    m_transitionAnimation.QueueCustomAnimation(m_equippedAnimation, null);
                    break;
                default:
                    ShowCost.Value = true;
                    m_transitionAnimation.QueueCustomAnimation(m_purchasableAnimation, null);
                    break;
            }
        }

        // 0x06003883 (0x6A36F0): the band background is unloaded through the Unity null check.
        private void OnDestroy()
        {
            if (m_parameters != null)
                m_parameters.Definition.IconAsset.Unload();
            if (m_bandDefinition != null)
                m_bandDefinition.SlotLockedBackground.Unload();
        }

        // 0x06003884 (0x6A3780)
        public void Action_OnToggleIsOn(bool toggleValue)
        {
            OnSelected(toggleValue);
        }

        // 0x06003885 (0x6A3790): selecting shows the selection from the Bought state upwards and
        // reports the definition; deselecting restores the item-state visuals.
        private void OnSelected(bool toggleValue)
        {
            if (toggleValue)
            {
                m_onSelected?.Invoke();
                ShowSelected.Value = m_parameters.State >= UIDreamPowerStoreItemState.Bought;
                m_transitionAnimation.QueueCustomAnimation(m_clearSelectedAnimation, null);
                m_transitionAnimation.QueueCustomAnimation(m_selectedAnimation, null);
                m_parameters.OnToggleChanged(m_parameters.Definition);
            }
            else
            {
                ShowSelected.Value = false;
                SetVisualState();
            }
        }

        // 0x06003886 (0x6A3850): switches the toggle on and runs the selection of OnSelected(true), which the native body inlines.
        public void Action_OnNavigatedTo()
        {
            m_toggle.IsOn = true;
            OnSelected(true);
        }

        // 0x06003887 (0x6A38F0)
        public void Action_OnNavigatedAway()
        {
            m_toggle.IsOn = false;
            ShowSelected.Value = false;
            SetVisualState();
        }

        // 0x06003888 (0x6A3930): the field initializers run before the UIWidget base constructor.
        public UIWidgetToggleDreamPower() { }
    }
}
