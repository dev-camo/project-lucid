using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009c4; bodies translated from the 1.10.1 x86-64 binary.
    // A level entry on the level select screen: name, orb progress, mission reward states, lock
    // and hidden-boss presentation, and the reward requirement restrictions of the level.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetSDTLevel : UIWidget
    {
        [SerializeField] private CollectableType m_rewardType = CollectableType.Orb;
        [SerializeField] private UIToggle m_toggle;
        [SerializeField] private Image m_fillImage;
        [SerializeField] private AnimationClip m_unlockAnimationClip;
        [SerializeField] private AnimationClip m_bossPreviewAnimationClip;
        [SerializeField] private UIWidgetNavigation m_uiWidgetNavigation;
        [SerializeField] private bool m_useNumericalName = true;
        [SerializeField] private Transform m_rewardRequirementsParent;
        [SerializeField] private UIContainerRewardRestriction m_rewardRequirementUIContainerPrefab;
        [SerializeField] private CanvasGroup m_canvasGroup;
        [SerializeField] private UnityEvent m_onLevelHidden;
        public readonly Bindable<string> LevelName = new Bindable<string>();
        public readonly Bindable<bool> IsNextLevel = new Bindable<bool>();
        public readonly Bindable<int> LevelOrbsCollected = new Bindable<int>();
        public readonly Bindable<int> LevelTotalOrbs = new Bindable<int>();
        public readonly Bindable<string> FormatXOfY = new Bindable<string>();
        public readonly Bindable<bool> ShowRewardsLeftToUnlockMessage = new Bindable<bool>();
        public readonly Bindable<string> RewardsUnlockMessage = new Bindable<string>();
        public readonly Bindable<UIShowType> RedStarRingsShowState = new Bindable<UIShowType>();
        public readonly Bindable<UIShowType> BlueCoinsShowState = new Bindable<UIShowType>();
        public readonly Bindable<UIShowType> JukeboxShowState = new Bindable<UIShowType>();
        public readonly Bindable<UIShowType> RankShowState = new Bindable<UIShowType>();
        public readonly Bindable<bool> IsNew = new Bindable<bool>();
        public readonly Bindable<bool> IsLocked = new Bindable<bool>();
        public readonly Bindable<Sprite> RewardIcon = new Bindable<Sprite>();
        private UIWidgetSDTLevelParameters m_parameters;
        private readonly SystemRef<UINavigationManager> m_navigationManagerRef = ProcessManager.GetSystemRef<UINavigationManager>(null, true);
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);
        private MissionManager m_missionManager;
        private readonly List<UIContainerRewardRestriction> m_rewardRestrictionInstances = new List<UIContainerRewardRestriction>();

        // 0x06003831 (0x69FD70): the widget refreshes whenever the string table changes.
        private void Start()
        {
            StringTable.StringsTableHandlers += Refresh;
        }

        // 0x06003832 (0x69FE50)
        private void OnDestroy()
        {
            StringTable.StringsTableHandlers -= Refresh;
        }

        // 0x06003833 (0x69FF30)
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            m_missionManager = ProcessManager.GetSystem<MissionManager>(null, true);
            m_parameters = parameters.GetAs<UIWidgetSDTLevelParameters>();
            FormatXOfY.Value = StringTable.GetString(Strings.FORMAT_X_OF_Y);
            UpdateBinders();
        }

        // 0x06003834 (0x6A0580)
        private void OnEnable()
        {
            StartCoroutine(this.WaitForUI(Refresh));
        }

        // 0x06003835 (0x6A0600): releases the reward icon that UpdateBinders loaded.
        private void OnDisable()
        {
            if (m_dataManagerRef.IsNull())
                return;
            CollectableDefinition collectableDefinition = GetCollectableDefinition();
            if (collectableDefinition == null)
                return;
            collectableDefinition.UnloadIcon();
        }

        // 0x06003836 (0x6A0770): the native body inlines Hide, Lock, HideBoss and the unlock gating. A hidden level is hidden, a locked level is locked, an unseen
        // hidden boss plays its preview and a level whose unlock has not been seen unlocks.
        private void Refresh()
        {
            UpdateBinders();
            if (m_parameters.Hidden)
            {
                Hide();
                return;
            }
            if (m_parameters.Locked)
            {
                Lock();
                return;
            }
            if (IsHiddenBoss())
            {
                HideBoss();
                return;
            }
            if (m_parameters.LevelDefinition.AlwaysUnlocked || m_parameters.UnlockSeen)
                return;
            Unlock();
        }

        // 0x06003837 (0x6A0050): text names read ActName (+0x24 at 0x6A00DD).
        private void UpdateBinders()
        {
            GameplayLevelDefinition levelDefinition = m_parameters.LevelDefinition;
            LevelName.Value = m_useNumericalName ? m_parameters.LevelNumericalName.ToString() : StringTable.GetString(levelDefinition.ActName);
            IsNextLevel.Value = m_parameters.IsNextLevel;
            m_fillImage.fillAmount = Mathf.Clamp01(GetProgressionPercent(m_rewardType, m_parameters.LevelProgress));
            ProgressionCollectable levelOrbs = m_parameters.LevelProgress.ProgressGroupsIgnored.GetValueOrDefault(m_rewardType);
            LevelOrbsCollected.Value = levelOrbs.Collected;
            LevelTotalOrbs.Value = levelOrbs.Total;
            bool alwaysLocked = levelDefinition.HasRequirement<RequirementLevelAlwaysLocked>();
            bool showRewardsMessage;
            Strings rewardsMessage;
            if (levelDefinition.LockedByRequirement<RequirementRewardLevelUnlocked>())
            {
                showRewardsMessage = true;
                rewardsMessage = Strings.MENU_GENERIC_LOCKED_BY_SHADOW_REWARD_TRACK_V1_9;
            }
            else if (levelDefinition.LockedByRequirement<RequirementSpecificMissionComplete>())
            {
                showRewardsMessage = true;
                rewardsMessage = Strings.MENU_ACT_LOCKED_BY_PREVIOUS_ACT;
            }
            else
            {
                // Non-short-circuit: the reward requirement widgets are always refreshed.
                showRewardsMessage = alwaysLocked | ShowRewardRequirementUI();
                rewardsMessage = showRewardsMessage ? Strings.MENU_ACT_REWARD_REQUIREMENT : Strings.LEVEL_SELECT_PLAY_PREVIOUS_LEVEL;
            }
            RewardsUnlockMessage.Value = StringTable.GetString(rewardsMessage);
            ShowRewardsLeftToUnlockMessage.Value = showRewardsMessage;
            CollectableDefinition collectableDefinition = GetCollectableDefinition();
            if (collectableDefinition != null)
                RewardIcon.Value = collectableDefinition.LoadIcon();
            RedStarRingsShowState.Value = GetMissionShowType(MissionType.RedStarRings);
            BlueCoinsShowState.Value = GetMissionShowType(MissionType.BlueCoins);
            JukeboxShowState.Value = GetMissionShowType(MissionType.Jukebox);
            RankShowState.Value = GetRankShowType();
            IsNew.Value = m_parameters.IsNew;
            IsLocked.Value = m_parameters.Locked;
        }

        // 0x06003838 (0x6A0F30)
        private UIShowType GetMissionShowType(MissionType missionType)
        {
            if (m_parameters.Locked)
                return UIShowType.Hidden;
            return m_missionManager.GetMissionTypeShowType(m_parameters.LevelDefinition, missionType);
        }

        // 0x06003839 (0x6A0F60)
        private UIShowType GetRankShowType()
        {
            if (m_parameters.Locked)
                return UIShowType.Hidden;
            if (IsHiddenBoss())
                return UIShowType.Hidden;
            if (BossRankLevelsHidden())
                return UIShowType.Hidden;
            return m_missionManager.GetRankUIShowType(m_parameters.LevelDefinition);
        }

        // 0x0600383a (0x6A0C50): reuses one restriction container per unmet reward requirement,
        // creating more on demand, and destroys the surplus. True when any requirement is unmet.
        private bool ShowRewardRequirementUI()
        {
            if (DebugUnlockLevels.AreAllLevelsUnlocked())
                return false;
            int shown = 0;
            foreach (RewardRequirement requirement in m_parameters.LevelDefinition.RewardRequirements)
            {
                int remaining = GetRewardRemainingCount(requirement);
                if (remaining <= 0)
                    continue;
                if (m_rewardRestrictionInstances.Count <= shown)
                    m_rewardRestrictionInstances.Add(Instantiate(m_rewardRequirementUIContainerPrefab, m_rewardRequirementsParent));
                m_rewardRestrictionInstances[shown].Setup(new UIContainerRewardRestrictionParameters(requirement.RewardType, remaining));
                shown++;
            }
            for (int i = shown; i < m_rewardRestrictionInstances.Count; i++)
                Destroy(m_rewardRestrictionInstances[i]);
            return shown > 0;
        }

        // 0x0600383b (0x6A15F0)
        private int GetRewardRemainingCount(RewardRequirement requirement)
        {
            return requirement.Count - m_parameters.OverallProgress.GetValueOrDefault(requirement.RewardType).Collected;
        }

        // 0x0600383c (0x6A06E0)
        private CollectableDefinition GetCollectableDefinition()
        {
            if (m_parameters != null && m_parameters.LevelProgress != null && m_rewardType != CollectableType.None)
                return m_dataManagerRef.Get().CollectableDefinitions[m_rewardType];
            return null;
        }

        // 0x0600383d (0x6A0970)
        private void Lock()
        {
            m_toggle.Interactable = false;
            m_uiWidgetNavigation.enabled = false;
        }

        // 0x0600383e (0x6A0AD0)
        private void Unlock()
        {
            m_toggle.Transition.QueueCustomAnimation(m_unlockAnimationClip, OnUnlockComplete);
            m_uiWidgetNavigation.enabled = !m_parameters.Locked;
        }

        // 0x0600383f (0x6A16A0)
        private void OnUnlockComplete()
        {
            m_toggle.Transition.QueueTransitionToState(m_toggle.IsOn ? UITransitionState.Selected : UITransitionState.Normal);
            if (m_toggle.IsOn)
                return;
            m_toggle.IsOn = true;
            TryNavigateToWidget();
        }

        // 0x06003840 (0x6A0920)
        private void Hide()
        {
            m_toggle.Interactable = false;
            m_uiWidgetNavigation.enabled = false;
            m_canvasGroup.alpha = 0f;
            m_onLevelHidden.Invoke();
        }

        // 0x06003841 (0x6A0A40): the boss preview selects the toggle when it ends, unless the
        // level's unlock has already been seen.
        private void HideBoss()
        {
            m_toggle.Transition.QueueCustomAnimation(m_bossPreviewAnimationClip, () =>
            {
                if (m_toggle.IsOn)
                    m_toggle.Transition.QueueTransitionToState(UITransitionState.Selected);
                if (m_parameters.UnlockSeen)
                    return;
                m_toggle.IsOn = true;
                TryNavigateToWidget();
            });
        }

        // 0x06003842 (0x6A09B0): a boss level stays hidden until one of its missions is complete.
        private bool IsHiddenBoss()
        {
            GameplayLevelDefinition levelDefinition = m_parameters.LevelDefinition;
            if (!levelDefinition.IsBossLevel)
                return false;
            if (levelDefinition.MissionList != null)
                return !levelDefinition.MissionList.HasAnyCompleteMissions(levelDefinition);
            return true;
        }

        // 0x06003843 (0x6A1030): a boss level hides its ranks unless some mission group whose
        // requirements are met has a mission with ranks.
        private bool BossRankLevelsHidden()
        {
            GameplayLevelDefinition levelDefinition = m_parameters.LevelDefinition;
            if (!levelDefinition.IsBossLevel)
                return false;
            foreach (MissionGroup missionGroup in levelDefinition.MissionList.Groups)
            {
                if (!missionGroup.MeetsAllRequirements(levelDefinition))
                    continue;
                foreach (MissionDefinition missionDefinition in missionGroup.Definitions)
                {
                    if (missionDefinition.Ranks != null)
                        return false;
                }
            }
            return true;
        }

        // 0x06003844 (0x6A1750): the native body inlines TryNavigateToWidget.
        public void Select(bool navigateToWidget = true)
        {
            m_toggle.IsOn = true;
            if (navigateToWidget)
                TryNavigateToWidget();
        }

        // 0x06003845 (0x6A1870)
        public void Action_OnNavigateTo()
        {
            if (m_toggle.Interactable)
                m_toggle.IsOn = true;
        }

        // 0x06003846 (0x6A18B0)
        public void DeselectWithoutNotify()
        {
            m_toggle.SetIsOnWithoutNotify(false);
        }

        // 0x06003847 (0x6A18D0)
        public void AddValueChangeListener(UnityAction<bool> onValueChanged)
        {
            m_toggle.OnValueChanged.AddListener(onValueChanged);
        }

        // 0x06003848 (0x6A1660)
        public void ToggleWidgetNavigation(bool on)
        {
            m_uiWidgetNavigation.enabled = on && !m_parameters.Locked;
        }

        // 0x06003849 (0x6A17F0)
        private void TryNavigateToWidget()
        {
            if (m_navigationManagerRef.IsNull())
                return;
            m_navigationManagerRef.Get().Select(m_uiWidgetNavigation);
        }

        // 0x0600384a (0x6A0B70): collected over total, or zero when the collectable is not tracked.
        private float GetProgressionPercent(CollectableType collectableType, ProgressionCollectableLevel levelProgress)
        {
            if (!levelProgress.ProgressGroupsIgnored.TryGetValue(collectableType, out ProgressionCollectable progress))
                return 0f;
            return (float)progress.Collected / (float)progress.Total;
        }

        // 0x0600384b
        public UIWidgetSDTLevel() { }
    }
}
