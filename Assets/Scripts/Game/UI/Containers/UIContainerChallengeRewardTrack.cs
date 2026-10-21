using System.Collections.Generic;
using Hardlight;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200085b; bodies translated from the 1.10.1 x86-64 binary.
    // One challenge reward track: the scrolling strip of reward buttons, the XP slider and the add, collect and
    // "all collected" states that go with it.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerChallengeRewardTrack : UIContainer
    {
        [SerializeField] private RewardTrackType m_rewardTrackType = RewardTrackType.Adventure;
        [SerializeField] private HLScrollRectSelection m_scrollRectSelection;
        [SerializeField] private RectTransform m_scrollRectContentTransform;
        [SerializeField] private RectTransform m_rewardsContentTransform;
        [SerializeField] private UIWidgetChallengesSlider m_challengesSlider;
        [SerializeField]
        private SerializableDictionary<UIWidgetButtonChallengeRewardSize, UIWidgetButtonChallengeReward> m_rewardBySizePrefabs =
            new SerializableDictionary<UIWidgetButtonChallengeRewardSize, UIWidgetButtonChallengeReward>(HardlightEnumComparers.UIWidgetButtonChallengeRewardSizeComparer);
        [SerializeField] private ApplicationStateEvent m_openCollectRewardEvent;
        [Tooltip("Renderers that contains a custom shader needed to be modified when the track moves.")]
        [SerializeField] private CanvasRenderer[] m_movingTrackRenderers;
        [SerializeField] private UIContainerNavigation m_containerNavigation;
        [SerializeField] private UIContainerChallengeXPReward m_containerXPCounter;
        [SerializeField]
        [Tooltip("When adjusting the size of the slider, this will create extra padding on the sides so the slider doesn't end flushed.")]
        private float m_sliderPadding;
        [SerializeField]
        [Tooltip("Multiplies the offset in the slider shader. Thus, if set to 1, it will be 1-to-1 with the scroll values, other values will increase or decrease the offset.")]
        private float m_sliderShaderOffsetMultiplier = 10f;
        [Tooltip("The space between each reward.")]
        [SerializeField] private int m_spaceBetweenRewards;
        [SerializeField] private float m_unlockingCoroutineDelay = 0.5f;
        [SerializeField] private UIAnimatingCounter m_xpTotalCounter;
        public readonly Bindable<bool> ShowAddButton = new Bindable<bool>();
        public readonly Bindable<bool> ShowCollectButton = new Bindable<bool>();
        public readonly Bindable<bool> EnableButtons = new Bindable<bool>();
        public readonly Bindable<bool> ShowMoreXPNeeded = new Bindable<bool>();
        public readonly Bindable<bool> ShowAllCollected = new Bindable<bool>();
        public readonly Bindable<string> XPIncrement = new Bindable<string>();
        private App m_app;
        private DataManager m_dataManager;
        private ChallengeManager m_challengeManager;
        private SaveManager m_saveManager;
        private OrnamentManager m_ornamentManager;
        private RewardTrackDefinition m_rewardTrackDefinition;
        private CanvasShaderModifier m_sliderShaderModifier;
        private UINavigationManager m_uiNavigationManager;
        private readonly List<UIWidgetButtonChallengeReward> m_rewardButtons = new List<UIWidgetButtonChallengeReward>();
        private bool m_sliderIsAnimating;
        private Coroutine m_unlockingCoroutine;
        private bool m_isUnlocking;

        // 0x06003071 (0x60F970)
        private void Start()
        {
            m_app = ProcessManager.GetSystem<App>();
            m_dataManager = ProcessManager.GetSystem<DataManager>();
            m_challengeManager = ProcessManager.GetSystem<ChallengeManager>();
            m_saveManager = ProcessManager.GetSystem<SaveManager>();
            m_ornamentManager = ProcessManager.GetSystem<OrnamentManager>();
            m_uiNavigationManager = ProcessManager.GetSystem<UINavigationManager>();
            m_sliderShaderModifier = new CanvasShaderModifier(m_movingTrackRenderers);
            m_rewardTrackDefinition = m_dataManager.RewardTrackDefinitions[m_rewardTrackType];
            PopulateUI();
        }

        // 0x06003072 (0x610210)
        private void OnDestroy()
        {
            CoroutineUtils.StopUtilCoroutine(ref m_unlockingCoroutine);
        }

        // 0x06003073 (0x60FBA0): one button per reward of the track, spaced along the strip; the first reward that is ready
        // to collect (or the next one to earn) takes the navigation focus once the UI has settled.
        private void PopulateUI()
        {
            ChallengeRewardTrackState trackState = m_challengeManager.RewardTrackStates[m_rewardTrackDefinition.Type];
            List<ChallengeReward> rewards = trackState.Definition.Rewards;
            int rewardCount = rewards.Count;
            int trackWidth = m_spaceBetweenRewards * rewardCount;
            Vector2 sliderPosition = m_challengesSlider.GetAnchoredPosition();
            UIWidgetButtonChallengeReward focusReward = null;
            ChallengeReward nextReward = trackState.GetNextOrLastReward();
            m_scrollRectContentTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, (float)trackWidth + m_sliderPadding);
            bool focusChosen = false;
            for (int i = 0; i < rewardCount; i++)
            {
                int rewardNumber = i + 1;
                ChallengeReward reward = rewards[i];
                UIChallengeTrackRewardStatus status = GetRewardStatus(reward, rewardNumber);
                int rewardXP = reward.XPThreshold;
                int rewardOffset = m_spaceBetweenRewards * rewardNumber;
                int nextRewardXP = nextReward.XPThreshold;
                if (!m_rewardBySizePrefabs.TryGetValue(status.Reward.Size, out UIWidgetButtonChallengeReward rewardPrefab))
                    continue;
                UIWidgetButtonChallengeReward challengeButton = Instantiate(rewardPrefab, m_rewardsContentTransform);
                if (!focusChosen && (status.IsReadyToCollect() || nextRewardXP == rewardXP))
                {
                    focusReward = challengeButton;
                    focusChosen = true;
                }
                float horizontalPosition = (float)rewardOffset + sliderPosition.x;
                bool isLast = i == rewardCount - 1;
                bool isNextReward = nextRewardXP == rewardXP;
                challengeButton.Setup(new UIWidgetButtonChallengesRewardParameters(status, rewardXP.ToString(), horizontalPosition,
                    isNextReward, isLast, () => OnRewardClick(challengeButton), OnRewardHighlighted, m_rewardTrackType));
                m_rewardButtons.Add(challengeButton);
            }
            if (focusChosen)
            {
                CoroutineUtils.WaitForUI(() =>
                {
                    m_uiNavigationManager.Select(focusReward.WidgetNavigation);
                    focusReward.Highlight();
                });
            }
            m_challengesSlider.Setup(new UIWidgetChallengesSliderParameters(GetSliderValue(), trackWidth, trackWidth));
            m_containerNavigation.UpdateChildWidgets();
            if (m_xpTotalCounter != null)
            {
                int currentXP = m_challengeManager.GetCurrentXP();
                m_xpTotalCounter.SetUp(currentXP, currentXP);
            }
            RefreshBinders();
        }

        // 0x06003074 (0x610570): the buttons follow the slider and unlock animations; the add button is offered while the
        // XP can be spent on the next reward and nothing is waiting to be collected or unlocking; "more XP needed" only
        // when nothing else is on offer.
        private void RefreshBinders()
        {
            ChallengeRewardTrackState trackState = m_challengeManager.RewardTrackStates[m_rewardTrackDefinition.Type];
            int xpToNextReward = trackState.XPToNextReward();
            bool enableButtons = false;
            if (!m_sliderIsAnimating && !m_isUnlocking)
            {
                XPIncrement.Value = xpToNextReward.ToString();
                enableButtons = true;
            }
            EnableButtons.Value = enableButtons;
            bool showCollect;
            bool cannotSpendXP;
            if (trackState.HasUncollectedReward())
            {
                cannotSpendXP = true;
                showCollect = !m_sliderIsAnimating;
            }
            else
            {
                cannotSpendXP = !trackState.CanSpendXP(m_challengeManager.GetCurrentXP());
                showCollect = m_isUnlocking && !m_sliderIsAnimating;
            }
            ShowCollectButton.Value = showCollect;
            ShowAddButton.Value = !(cannotSpendXP && !m_sliderIsAnimating) && !m_isUnlocking;
            ShowAllCollected.Value = xpToNextReward == 0 && !ShowCollectButton.Value && !ShowAddButton.Value;
            ShowMoreXPNeeded.Value = !ShowCollectButton.Value && !ShowAllCollected.Value && !ShowAddButton.Value;
        }

        // 0x06003075 (0x610830): nothing happens while the slider animates or a reward is unlocking; otherwise the reward is
        // saved, reported and animated, its unlock queued for the meta game, and the collect screen event raised.
        private void OnRewardClick(UIWidgetButtonChallengeReward challengeButton, bool skipRefreshBinders = false)
        {
            if (m_sliderIsAnimating || m_isUnlocking)
                return;
            UIChallengeTrackRewardStatus status = challengeButton.RewardStatus;
            status.Save();
            status.FireAnalyticsEvents();
            challengeButton.Collect(status);
            IMetaGameUnlock unlock = status.GetMetaGameUnlock();
            m_app.Storage.GetValue<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks, null, true).Add(unlock);
            m_app.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_openCollectRewardEvent);
            CoroutineUtils.StopUtilCoroutine(ref m_unlockingCoroutine);
            m_isUnlocking = true;
            if (!skipRefreshBinders)
                RefreshBinders();
            m_unlockingCoroutine = CoroutineUtils.Delay(() =>
            {
                foreach (UIWidgetButtonChallengeReward rewardButton in m_rewardButtons)
                {
                    if (rewardButton.IsReadyToCollect())
                    {
                        m_uiNavigationManager.Select(rewardButton.WidgetNavigation);
                        rewardButton.Highlight();
                        break;
                    }
                }
                m_isUnlocking = false;
                if (!skipRefreshBinders)
                    RefreshBinders();
            }, m_unlockingCoroutineDelay);
        }

        // 0x06003076 (0x610BF0)
        public void Action_CollectReward()
        {
            TryCollectReward();
        }

        // 0x06003077 (0x610C00): the first reward button that is ready to collect is clicked.
        private bool TryCollectReward(bool skipRefreshBinders = false)
        {
            foreach (UIWidgetButtonChallengeReward rewardButton in m_rewardButtons)
            {
                if (rewardButton.IsReadyToCollect())
                {
                    OnRewardClick(rewardButton, skipRefreshBinders);
                    return true;
                }
            }
            return false;
        }

        // 0x06003078 (0x610D80)
        private void OnRewardHighlighted(RectTransform musicTrackTransform)
        {
            m_scrollRectSelection.Select(musicTrackTransform, true, false, null);
        }

        // 0x06003079 (0x610DA0)
        private void OnRewardHighlighted(RectTransform musicTrackTransform, bool instant)
        {
            m_scrollRectSelection.Select(musicTrackTransform, instant, false, null);
        }

        // 0x0600307a (0x610250): a reward is unlocked once the spent XP reaches its threshold; ornament rewards are
        // collected when the ornament is owned, every other reward when its save data says so.
        private UIChallengeTrackRewardStatus GetRewardStatus(ChallengeReward challengeReward, int rewardNumber)
        {
            SaveDataChallengeReward savedReward = m_saveManager.CurrentSave.GetRewardDataByTrackGUIDUnsafe(challengeReward.GetGUID());
            int spentXP = m_challengeManager.RewardTrackStates[m_rewardTrackDefinition.Type].SpentXP;
            bool collected = savedReward.Collected;
            if (challengeReward is ChallengeRewardOrnament ornamentReward)
                collected = m_ornamentManager.IsOrnamentCollected(ornamentReward.Ornament);
            return new UIChallengeTrackRewardStatus(challengeReward, collected, spentXP >= challengeReward.XPThreshold, rewardNumber,
                m_rewardTrackDefinition.Type);
        }

        // 0x0600307b (0x610DC0)
        public void Action_OnScrollRectScroll(Vector2 value)
        {
            if (m_sliderShaderModifier != null)
                m_sliderShaderModifier.SetDreamNoiseStandardOffset(value.x * m_sliderShaderOffsetMultiplier);
        }

        // 0x0600307c (0x610DF0): spends the XP for the next reward, counts the XP total down and animates the slider; once
        // it has arrived the reward that was reached is refreshed and brought into view.
        public void Action_SpendXP()
        {
            if (m_isUnlocking || m_sliderIsAnimating)
                return;
            ChallengeRewardTrackState trackState = m_challengeManager.RewardTrackStates[m_rewardTrackDefinition.Type];
            int xpBefore = m_challengeManager.GetCurrentXP();
            if (!trackState.CanSpendXP(xpBefore))
                return;
            trackState.SpendXP();
            int xpAfter = m_challengeManager.GetCurrentXP();
            if (m_xpTotalCounter)
            {
                m_xpTotalCounter.SetUp(xpBefore, xpAfter);
                m_xpTotalCounter.StartAnimating();
            }
            m_sliderIsAnimating = true;
            RefreshBinders();
            m_challengeManager.RefreshXPRewards();
            m_containerXPCounter.RefreshXP();
            m_challengesSlider.SetValue(GetSliderValue(), () =>
            {
                if (trackState.HighestAchievedRewardIndex >= 0)
                {
                    UIChallengeTrackRewardStatus status = GetRewardStatus(trackState.GetHighestRewardAchieved(), trackState.HighestAchievedRewardIndex);
                    UIWidgetButtonChallengeReward rewardButton = m_rewardButtons[trackState.HighestAchievedRewardIndex];
                    rewardButton.Refresh(status);
                    OnRewardHighlighted((RectTransform)rewardButton.transform, false);
                }
                m_sliderIsAnimating = false;
                if (!TryCollectReward(true))
                    RefreshBinders();
            });
        }

        // 0x0600307d (0x610400): the slider rests at the last reward reached plus the share of the way to the next one that
        // the spent XP covers. The track state's index is read again after GetNextOrLastReward, as in the original.
        private float GetSliderValue()
        {
            RewardTrackType trackType = m_rewardTrackDefinition.Type;
            ChallengeRewardTrackState trackState = m_challengeManager.RewardTrackStates[trackType];
            int spentXP = m_saveManager.CurrentSave.GetOrCreateRewardTrackData(trackType).SpentXP;
            ChallengeReward highestReward = trackState.GetHighestRewardAchieved();
            int highestIndex = trackState.HighestAchievedRewardIndex;
            int highestXP = 0;
            if (highestReward != null)
                highestXP = highestReward.XPThreshold;
            int nextIndex = highestIndex + 1;
            float xpBetweenRewards = Mathf.Max((float)(trackState.GetNextOrLastReward().XPThreshold - highestXP), 1f);
            float xpTowardsNext = (float)(spentXP - highestXP);
            int start = nextIndex * m_spaceBetweenRewards;
            int end = (trackState.HighestAchievedRewardIndex + 2) * m_spaceBetweenRewards;
            float share = xpTowardsNext / xpBetweenRewards;
            return (float)start + (float)(end - start) * share;
        }

        // 0x0600307e (0x611080)
        public UIContainerChallengeRewardTrack() { }
    }
}
