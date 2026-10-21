using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000887; bodies translated from the 1.10.1 x86-64 binary.
    // The challenges screen: one toggle per active challenge plus a bonus toggle (or a single shadow FTUE toggle),
    // the selected challenge's info, level preview, bonus and leaderboard panels, and the launch data for the level.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerChallengesOverview : UIContainer
    {
        [SerializeField] private UIContainerNavigation m_containerNavigation;
        [Tooltip("Parent to spawn the challenge toggles into")]
        [SerializeField] private UIToggleGroup m_challengeTogglesRoot;
        [SerializeField]
        [Tooltip("Toggle challenge prefab to spawn for the active challenges")]
        private UIWidgetSDTToggleChallenge m_challengeTogglePrefab;
        [Tooltip("Toggle challenge prefab to spawn that displays the bonus info for the challenge period when selected")]
        [SerializeField] private UIWidgetSDTToggleChallenge m_challengeToggleBonusPrefab;
        [Tooltip("Container for displaying info about the selected challenge")]
        [SerializeField] private UIContainerChallenge m_challengeInfoContainer;
        [Tooltip("Container for displaying challenge period bonus info when that toggle is selected")]
        [SerializeField] private UIContainerChallengeBonus m_challengeBonusInfoContainer;
        [Tooltip("Level definition view for displaying image previewing level for selected challenge")]
        [SerializeField] private UIContainerLevelDefinition m_levelDefinitionView;
        [SerializeField]
        [Tooltip("Displays the current score for the challenge period and allows opening of the leaderboard")]
        private UIContainerChallengeLeaderboard m_challengeLeaderboardContainer;
        [SerializeField] [HashEnum(typeof(Strings))] private Strings m_refreshesInLabel;
        [SerializeField]
        [Tooltip("UIEvent to send when there is bonus XP to notify the user about")]
        private UIModernEvent m_bonusXpAvailableEvent;
        [SerializeField]
        [Tooltip("UIEvent to send to show the challenges info popup")]
        private UIModernEvent m_showInfoEvent;
        [SerializeField]
        [Tooltip("Object to switch on if test mode is active, and switch off when inactive.")]
        private GameObject m_testModeDisplay;
        public readonly Bindable<string> RefreshesInLabel = new Bindable<string>();
        public readonly Bindable<bool> ValidChallengeSelected = new Bindable<bool>();
        public readonly Bindable<string> CycleDebugLabel = new Bindable<string>();
        public readonly Bindable<bool> ShowShadowFTUE = new Bindable<bool>();
        private SystemRef<ChallengeManager> m_challengeManagerRef;
        private readonly List<UIWidgetSDTToggleChallenge> m_uiWidgetToggleChallenges = new List<UIWidgetSDTToggleChallenge>();
        private UIWidgetSDTToggleChallenge m_uiWidgetToggleChallengeBonus;
        private UIWidgetSDTToggleChallenge m_bonusToggle;
        private ChallengeState m_selectedChallenge;
        private bool m_hasShownBonusXpAwarded;
        private readonly SystemRef<UINavigationManager> m_navigationManagerRef = ProcessManager.GetSystemRef<UINavigationManager>(null, true);

        // 0x0600313d (0x61DA20): the panels start hidden; the shadow FTUE selection flag left in the application
        // storage by an earlier launch is cleared.
        private void Start()
        {
            m_challengeManagerRef = ProcessManager.GetSystemRef<ChallengeManager>(null, true);
            m_challengeInfoContainer.gameObject.SetActive(false);
            m_challengeBonusInfoContainer.gameObject.SetActive(false);
            m_levelDefinitionView.gameObject.SetActive(false);
            RefreshesInLabel.Value = StringTable.GetString(m_refreshesInLabel);
            ValidChallengeSelected.Value = false;
            m_challengeManagerRef.InvokeOnValid(OnChallengeManagerValid);
            ProcessManager.GetSystem<App>().Storage.RemoveValue<bool>(AppFSMKeys.SelectShadowFTUEChallenge);
        }

        // 0x0600313e (0x61DCB0)
        private void OnDestroy()
        {
            if (m_challengeManagerRef.IsValid())
                m_challengeManagerRef.Get().OnChallengesUpdated -= OnChallengesUpdated;
        }

        // 0x0600313f (0x61DDF0): the current challenges are shown at once, then kept up to date.
        private void OnChallengeManagerValid(ChallengeManager challengeManager)
        {
            OnChallengesUpdated(challengeManager.GetChallengeStates());
            challengeManager.OnChallengesUpdated += OnChallengesUpdated;
        }

        // 0x06003140 (0x61DEE0): the toggle list is resized to the challenge count (a single toggle for the shadow FTUE),
        // each toggle is set up with its challenge, then the first unfinished challenge (or the bonus when all are
        // finished) is selected.
        private void OnChallengesUpdated(IReadOnlyList<ChallengeState> challengeStates)
        {
            ChallengeManager challengeManager = m_challengeManagerRef.Get();
            bool showShadowFtue = challengeManager.ShouldShowShadowFTUE();
            ShowShadowFTUE.Value = showShadowFtue;
            int count = showShadowFtue ? 1 : challengeStates.Count;
            // The native loops read the list count once. Destroy(component) removes only the component, as in the original.
            for (int i = m_uiWidgetToggleChallenges.Count - 1; i >= count; i--)
            {
                UIWidgetSDTToggleChallenge surplus = m_uiWidgetToggleChallenges[i];
                m_uiWidgetToggleChallenges.RemoveAt(i);
                surplus.SetSelected(false);
                surplus.SetToggleGroup(null);
                Destroy(surplus);
            }
            for (int missing = count - m_uiWidgetToggleChallenges.Count; missing > 0; missing--)
            {
                UIWidgetSDTToggleChallenge toggle = Instantiate(m_challengeTogglePrefab, m_challengeTogglesRoot.transform);
                toggle.SetToggleGroup(m_challengeTogglesRoot);
                m_uiWidgetToggleChallenges.Add(toggle);
            }

            bool allChallengesComplete = true;
            if (showShadowFtue)
            {
                if (m_bonusToggle != null)
                {
                    m_bonusToggle.SetSelected(false);
                    m_bonusToggle.SetToggleGroup(null);
                    Destroy(m_bonusToggle);
                }
                UIWidgetSDTToggleChallenge shadowToggle = m_uiWidgetToggleChallenges[0];
                ChallengeState challengeState = m_challengeManagerRef.Get().GetShadowFTUEChallengeState();
                shadowToggle.Setup(new UIWidgetSDTToggleChallenge.UIWidgetToggleChallengeParameters(true, false, string.Empty,
                    isSelected => ToggleChallenge(challengeState, isSelected), false, true));
                shadowToggle.SetSelected(false);
                if (m_challengeLeaderboardContainer != null)
                    m_challengeLeaderboardContainer.gameObject.SetActive(false);
            }
            else
            {
                if (m_bonusToggle == null)
                {
                    m_bonusToggle = Instantiate(m_challengeToggleBonusPrefab, m_challengeTogglesRoot.transform);
                    m_bonusToggle.SetToggleGroup(m_challengeTogglesRoot);
                }
                m_bonusToggle.transform.SetAsLastSibling();
                for (int i = 0; i < count; i++)
                {
                    UIWidgetSDTToggleChallenge toggle = m_uiWidgetToggleChallenges[i];
                    ChallengeState challengeState = challengeStates[i];
                    string label = (i + 1).ToString();
                    Action<bool> callback = isSelected => ToggleChallenge(challengeState, isSelected);
                    bool complete = challengeState.BestTimeSeconds > 0f;
                    allChallengesComplete &= complete;
                    toggle.Setup(new UIWidgetSDTToggleChallenge.UIWidgetToggleChallengeParameters(!challengeState.Played, complete, label,
                        callback, false, false));
                    toggle.SetSelected(false);
                }
                m_bonusToggle.Setup(new UIWidgetSDTToggleChallenge.UIWidgetToggleChallengeParameters(false, allChallengesComplete, null,
                    ToggleBonus, true, false));
                m_bonusToggle.SetSelected(false);
                m_challengeBonusInfoContainer.Setup(new UIContainerChallengeBonus.UIContainerChallengeBonusParameters(
                    challengeManager.GetBonusXP(), count, allChallengesComplete));
                if (m_challengeLeaderboardContainer != null)
                {
                    m_challengeLeaderboardContainer.gameObject.SetActive(true);
                    int totalScore = challengeManager.GetTotalScore();
                    bool canShowLeaderboard = challengeManager.CanShowLeaderboard();
                    m_challengeLeaderboardContainer.Setup(
                        new UIContainerChallengeLeaderboard.UIContainerChallengeLeaderboardParameters(totalScore, canShowLeaderboard));
                }
                if (challengeManager.FeatureState == ChallengeManager.ChallengeFeatureState.UnlockedSeenInterstitial)
                {
                    ProcessManager.GetSystem<App>().AddUniqueUIModernEvent(m_showInfoEvent);
                    challengeManager.SetFeatureUnlockedSeen();
                }
            }

            CoroutineUtils.OnNextFrame(() => m_containerNavigation.UpdateChildWidgets());
            m_challengeInfoContainer.gameObject.SetActive(false);
            m_challengeBonusInfoContainer.gameObject.SetActive(false);
            m_selectedChallenge = null;
            UpdateLevelPreview();
            ValidChallengeSelected.Value = false;
            if (challengeManager.PeriodBonusXP > 0 && !m_hasShownBonusXpAwarded)
            {
                ProcessManager.GetSystem<App>().AddUniqueUIModernEvent(m_bonusXpAvailableEvent);
                m_hasShownBonusXpAwarded = true;
            }
            else if (challengeManager.BonusXPUnacknowledged <= 0)
            {
                m_hasShownBonusXpAwarded = false;
            }

            bool testModeActive = challengeManager.TestModeActive;
            m_testModeDisplay.SetActive(testModeActive);
            if (testModeActive)
                CycleDebugLabel.Value = challengeManager.GetCycleDebugLabel();

            for (int i = 0; i < count; i++)
            {
                UIWidgetSDTToggleChallenge toggle = m_uiWidgetToggleChallenges[i];
                if (!toggle.IsComplete)
                {
                    toggle.SetSelected(false);
                    toggle.SetSelected(true);
                    TryNavigateToWidget(toggle);
                    ValidChallengeSelected.Value = true;
                    m_challengeTogglesRoot.NotifyToggleOn(toggle.GroupElement, true);
                    return;
                }
            }
            if (!showShadowFtue)
            {
                m_bonusToggle.SetSelected(false);
                m_bonusToggle.SetSelected(true);
                TryNavigateToWidget(m_bonusToggle);
                m_challengeTogglesRoot.NotifyToggleOn(m_bonusToggle.GroupElement, true);
            }
        }

        // 0x06003141 (0x61EDE0): selecting a challenge that is no longer valid clears the selection; deselecting only
        // clears it when it is the selected challenge.
        private void ToggleChallenge(ChallengeState challengeState, bool selected)
        {
            if (challengeState != null && selected && !ValidateChallengeState(challengeState))
            {
                m_selectedChallenge = null;
                ValidChallengeSelected.Value = false;
                return;
            }
            if (selected)
            {
                m_selectedChallenge = challengeState;
                m_challengeInfoContainer.Setup(new UIContainerChallenge.UIContainerChallengeParameters(m_selectedChallenge));
                m_challengeInfoContainer.gameObject.SetActive(true);
                m_challengeBonusInfoContainer.gameObject.SetActive(false);
                ValidChallengeSelected.Value = true;
            }
            else
            {
                if (m_selectedChallenge != challengeState)
                    return;
                m_selectedChallenge = null;
                m_challengeInfoContainer.Setup(new UIContainerChallenge.UIContainerChallengeParameters(null));
                m_challengeInfoContainer.gameObject.SetActive(false);
                ValidChallengeSelected.Value = false;
            }
            UpdateLevelPreview();
        }

        // 0x06003142 (0x61F0D0): the bonus panel replaces the challenge panel while the bonus toggle is selected.
        private void ToggleBonus(bool selected)
        {
            if (selected)
            {
                m_challengeBonusInfoContainer.gameObject.SetActive(true);
                m_challengeInfoContainer.gameObject.SetActive(false);
                ValidChallengeSelected.Value = false;
            }
            else
            {
                m_challengeBonusInfoContainer.gameObject.SetActive(false);
            }
        }

        // 0x06003143 (0x61ECB0)
        private void UpdateLevelPreview()
        {
            if (m_selectedChallenge != null)
                m_levelDefinitionView.Setup(new UIContainerLevelViewParameters(m_selectedChallenge.ChallengeDefinition.GameplayLevelDefinition));
            m_levelDefinitionView.gameObject.SetActive(m_selectedChallenge != null);
        }

        // 0x06003144 (0x61ED60)
        private void TryNavigateToWidget(UIWidgetToggle toggle)
        {
            if (m_navigationManagerRef.IsNull())
                return;
            m_navigationManagerRef.Get().Select(toggle.WidgetNavigation);
        }

        // 0x06003145 (0x61F140): a selection that is no longer valid is dropped; otherwise the level, the shadow FTUE
        // flag and the challenge's mission context are handed to the application.
        public void Action_SetChallengeLaunchData()
        {
            if (m_selectedChallenge == null || !ValidateChallengeState(m_selectedChallenge))
            {
                m_selectedChallenge = null;
                ValidChallengeSelected.Value = false;
                return;
            }
            ChallengeManager challengeManager = m_challengeManagerRef.Get();
            App app = ProcessManager.GetSystem<App>();
            bool shadowFtue = challengeManager.ShouldShowShadowFTUE();
            app.Storage.SetValue<bool>(AppFSMKeys.SelectShadowFTUEChallenge, shadowFtue);
            LevelManager levelManager = ProcessManager.GetSystem<LevelManager>();
            GameplayLevelDefinition loadingImageLevel = null;
            if (shadowFtue)
                loadingImageLevel = levelManager.GetLevelForCutscene(challengeManager.ShadowFTUECutsceneDefinition);
            levelManager.SetupFsmVarsToLoadLevel(m_selectedChallenge.ChallengeDefinition.GameplayLevelDefinition, loadingImageLevel);
            MissionManager missionManager = ProcessManager.GetSystem<MissionManager>();
            missionManager.SetActiveMissionContext(new MissionContextChallenge(m_selectedChallenge, challengeManager.TestModeActive));
        }

        // 0x06003146 (0x61F040): the shadow FTUE accepts only its own challenge; otherwise the challenge must be one of the
        // manager's current challenges.
        private bool ValidateChallengeState(ChallengeState challengeState)
        {
            ChallengeManager challengeManager = m_challengeManagerRef.Get();
            if (challengeManager.ShouldShowShadowFTUE())
                return challengeState.ChallengeDefinition == challengeManager.ShadowFTUEChallengeDefinition;
            return challengeManager.GetChallengeStates().Contains(challengeState);
        }

        // 0x06003147 (0x61F480)
        public void Action_ClearChallengeLaunchData()
        {
            ProcessManager.GetSystem<MissionManager>().SetActiveMissionContext(null);
        }

        // 0x06003148 (0x61EC30)
        public void ShowInfoPopup()
        {
            ProcessManager.GetSystem<App>().AddUniqueUIModernEvent(m_showInfoEvent);
        }

        // 0x06003149 (0x61F4F0)
        public UIContainerChallengesOverview() { }
    }
}
