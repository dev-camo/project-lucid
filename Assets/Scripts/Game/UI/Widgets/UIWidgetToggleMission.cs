using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009d4; bodies translated from the 1.10.1 x86-64 binary.
    // A mission toggle: completion, new marker, rank icon, sub-reward states and reward icon.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIWidgetToggleMission : UIWidgetToggle
    {
        [SerializeField] private AnimationClip m_unlockAnimationClip;
        public readonly Bindable<bool> Complete = new Bindable<bool>();
        public readonly Bindable<bool> IsNew = new Bindable<bool>();
        public readonly Bindable<bool> ShowRankIcon = new Bindable<bool>();
        public readonly Bindable<bool> SubRewardsAnchorRight = new Bindable<bool>();
        public readonly Bindable<Sprite> RankIconBase = new Bindable<Sprite>();
        public readonly Bindable<Sprite> RankIconBackground = new Bindable<Sprite>();
        public readonly Bindable<Sprite> RankIconForeground = new Bindable<Sprite>();
        public readonly Bindable<UIShowType> RedStarRingsShowState = new Bindable<UIShowType>();
        public readonly Bindable<UIShowType> BlueCoinsShowState = new Bindable<UIShowType>();
        public readonly Bindable<UIShowType> JukeboxShowState = new Bindable<UIShowType>();
        public readonly Bindable<Sprite> RewardImage = new Bindable<Sprite>();
        private UIWidgetToggleMissionParameters m_parameters;
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);
        private ManagedAddressableAsset<Sprite> m_baseAddressableAsset;
        private ManagedAddressableAsset<Sprite> m_backgroundAddressableAsset;
        private ManagedAddressableAsset<Sprite> m_foregroundAddressableAsset;

        // 0x0600388c (0x6A3CC0): a locked mission hides its sub-reward states and is disabled;
        // an unlocked mission whose unlock has not been seen plays the unlock animation.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIWidgetToggleMissionParameters>();
            Complete.Value = m_parameters.Complete;
            IsNew.Value = m_parameters.IsNewState == MissionIsNewState.IsNew;
            ShowRankIcon.Value = false;
            RedStarRingsShowState.Value = m_parameters.Locked ? UIShowType.Hidden : m_parameters.RedStarRingsState;
            BlueCoinsShowState.Value = m_parameters.Locked ? UIShowType.Hidden : m_parameters.BlueCoinsState;
            JukeboxShowState.Value = m_parameters.Locked ? UIShowType.Hidden : m_parameters.JukeboxState;
            SubRewardsAnchorRight.Value = m_parameters.SubRewardsAnchorRight;
            RewardImage.Value = m_dataManagerRef.Get().CollectableDefinitions[m_parameters.RewardType].LoadIcon();
            if (m_parameters.Locked)
                Lock();
            else if (IsReadyToUnlock())
                AnimateUnlock();
        }

        // 0x0600388d (0x6A3F60)
        private void OnDisable()
        {
            if (m_baseAddressableAsset != null)
                m_baseAddressableAsset.Unload();
            if (m_backgroundAddressableAsset != null)
                m_backgroundAddressableAsset.Unload();
            if (m_foregroundAddressableAsset != null)
                m_foregroundAddressableAsset.Unload();
            if (m_dataManagerRef.TryGet(out DataManager dataManager))
                dataManager.CollectableDefinitions[m_parameters.RewardType].UnloadIcon();
        }

        // 0x0600388e (0x6A4050)
        public void SetIsNew(bool isNew)
        {
            IsNew.Value = isNew;
        }

        // 0x0600388f (0x6A3F00)
        private void Lock()
        {
            m_toggle.Transition.QueueTransitionToState(UITransitionState.Disabled);
        }

        // 0x06003890 (0x6A3F20)
        private bool IsReadyToUnlock()
        {
            return !m_parameters.Locked && !m_parameters.UnlockSeen;
        }

        // 0x06003891 (0x6A3F40)
        private void AnimateUnlock()
        {
            m_toggle.Transition.QueueCustomAnimation(m_unlockAnimationClip, null);
        }

        // 0x06003892 (0x6A3EE0)
        public void SetRankIconVisibility(bool visible)
        {
            ShowRankIcon.Value = visible;
        }

        // 0x06003893 (0x6A4070): the previous asset is unloaded; a null asset clears the icon.
        public void SetRankIconBase(ManagedAddressableAsset<Sprite> addressableAsset)
        {
            if (m_baseAddressableAsset != null)
                m_baseAddressableAsset.Unload();
            m_baseAddressableAsset = addressableAsset;
            RankIconBase.Value = addressableAsset != null ? addressableAsset.Load() : null;
        }

        // 0x06003894 (0x6A4120)
        public void SetRankIconBackground(ManagedAddressableAsset<Sprite> addressableAsset)
        {
            if (m_backgroundAddressableAsset != null)
                m_backgroundAddressableAsset.Unload();
            m_backgroundAddressableAsset = addressableAsset;
            RankIconBackground.Value = addressableAsset != null ? addressableAsset.Load() : null;
        }

        // 0x06003895 (0x6A41D0)
        public void SetRankIconForeground(ManagedAddressableAsset<Sprite> addressableAsset)
        {
            if (m_foregroundAddressableAsset != null)
                m_foregroundAddressableAsset.Unload();
            m_foregroundAddressableAsset = addressableAsset;
            RankIconForeground.Value = addressableAsset != null ? addressableAsset.Load() : null;
        }

        // 0x06003896 (0x6A4290)
        public UIWidgetToggleMission() { }
    }
}
