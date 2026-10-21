using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 020008a9; bodies translated from the 1.10.1 x86-64 binary.
    // The debug menu container: level, ornament and dream power unlocks, invincibility, FPS and pool displays,
    // challenge test mode and XP, the respawn button and one button per active mission. Calls to the
    // ChallengeManager and Character debug methods (DebugAddXP, DebugResetXP, DebugToggleInvulnerable) are
    // compiled out of retail, so AddXP, ResetXP and Action_ToggleInvincibility keep only what surrounded them.
    // Its closures are <>c__DisplayClass32_0/1/2 (OnMissionManagerValid), 43_0 (SetupForMission) and
    // 44_0 (SetupMissionObjectiveButton); 30 fields precede its first method.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerDebug : UIContainer
    {
        [SerializeField]
        private HLScrollRectSelection m_scrollRectSelection;

        [SerializeField]
        private UIContainerNavigation m_containerNavigation;

        [SerializeField]
        private RectTransform m_missionCompleteRoot;

        [SerializeField]
        private UIWidgetButton m_missionCompleteButtonPrefab;

        [SerializeField]
        private UIWidgetButton m_respawnButton;

        [SerializeField]
        private Toggle m_actToggle;

        [SerializeField]
        private Toggle m_ornamentToggle;

        [SerializeField]
        private Toggle m_invincibilityToggle;

        [SerializeField]
        private Toggle m_fpsToggle;

        [SerializeField]
        private Toggle m_poolsToggle;

        [SerializeField]
        private Toggle m_missionTimerUnlimitedToggle;

        [SerializeField]
        private Toggle m_challengeTestModeToggle;

        [SerializeField]
        private Toggle m_unlockDreamPowersToggle;

        [SerializeField]
        private Button m_challengeCycleButtonNext;

        [SerializeField]
        private Button m_challengeCycleButtonPrevious;

        [SerializeField]
        private Toggle m_challengeFeatureToggle;

        [FormerlySerializedAs("m_xpPlus10")]
        [SerializeField]
        private Button m_xpPlus1k;

        [SerializeField]
        [FormerlySerializedAs("m_xpPlus100")]
        private Button m_xpPlus10k;

        [SerializeField]
        [FormerlySerializedAs("m_xpPlus1000")]
        private Button m_xpPlus100k;

        [SerializeField]
        private Button m_xpReset;

        public readonly Bindable<string> ChallengeXP = new Bindable<string>();
        public readonly Bindable<string> ChallengeCycleLabel = new Bindable<string>();
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>(null, true);
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);
        private readonly SystemRef<TrackManager> m_trackManagerRef = ProcessManager.GetSystemRef<TrackManager>(null, true);
        private OrnamentManager m_ornamentManager;
        private ChallengeManager m_challengeManager;
        private int m_pressedMissionButtonIndex = -1;

        // 060031fb (1.10.1 RVA 0x62B6F0): the act, FPS, mission timer and pools toggles are set to the current
        // state without notifying, and the invincibility and respawn controls stay hidden until a character exists.
        private void Start()
        {
            bool allLevelsUnlocked = DebugUnlockLevels.AreAllLevelsUnlocked();
            SetUnlockAllLevels(allLevelsUnlocked);
            m_actToggle.SetIsOnWithoutNotify(allLevelsUnlocked);
            m_missionManagerRef.InvokeOnValid(OnMissionManagerValid);
            m_invincibilityToggle.gameObject.SetActive(false);
            m_respawnButton.gameObject.SetActive(false);
            m_characterManagerRef.InvokeOnValid(OnCharacterManagerValid);
            m_fpsToggle.SetIsOnWithoutNotify(VisualQualityManager_SDT.FPSDisplay);
            m_missionTimerUnlimitedToggle.SetIsOnWithoutNotify(MissionTimer.UnlimitedTime);
            SetPoolsToggleState();
            ProcessManager.GetSystemRef<OrnamentManager>(null, true).InvokeOnValid(OnOrnamentManagerValid);
            ProcessManager.GetSystemRef<ChallengeManager>(null, true).InvokeOnValid(OnChallengeManagerValid);
            ProcessManager.GetSystemRef<DreamPowerStoreManager>(null, true).InvokeOnValid(OnDreamPowerStoreManagerValid);
        }

        // 060031fc (1.10.1 RVA 0x62BC40)
        private void OnCharacterManagerValid(CharacterManager characterManager)
        {
            if (characterManager.TryGetCurrentCharacter(out Character character))
            {
                m_invincibilityToggle.gameObject.SetActive(true);
                m_invincibilityToggle.SetIsOnWithoutNotify(character.DebugInvulnerable());
                m_respawnButton.gameObject.SetActive(true);
                SetRespawnButton(character);
            }
        }

        // 060031fd (1.10.1 RVA 0x62BF10): the old mission buttons are destroyed. In the meta game only the next
        // mission's button is offered; otherwise each active mission gets a button that completes it, or, for a
        // mission with at least two objectives still to do, one that completes its next objective.
        private void OnMissionManagerValid(MissionManager missionManager)
        {
            foreach (RectTransform missionButton in m_missionCompleteRoot)
                Destroy(missionButton.gameObject);
            App app = m_appRef.Get();
            if (app.IsMetaGameActive())
            {
                SetupForMenu();
                return;
            }
            if (!missionManager.TryGetAllActiveMissionStates(out List<MissionState> activeMissionStates)) return;
            for (int i = 0; i < activeMissionStates.Count; i++)
            {
                MissionState activeMissionState = activeMissionStates[i];
                string label = "Complete: " + activeMissionState.Definition.name;
                UIWidgetButton button = Instantiate(m_missionCompleteButtonPrefab, m_missionCompleteRoot);
                Action callback;
                if (activeMissionState.Target >= 2 && activeMissionState.Target - activeMissionState.Progress >= 2)
                {
                    callback = SetupMissionObjectiveButton(activeMissionState, out label, i);
                }
                else
                {
                    callback = () =>
                    {
                        button.SetInteractable(false);
                        Action onPauseClose = app.Storage.GetValue<Action>(AppFSMKeys.OnPauseCloseEvent, null, true);
                        onPauseClose += activeMissionState.Tracker.MissionObjectivesCompleted;
                        app.Storage.SetValue(AppFSMKeys.OnPauseCloseEvent, onPauseClose);
                    };
                }
                void Select(UIWidgetNavigation _) => m_scrollRectSelection.Select((RectTransform)button.transform, true);
                if (i == m_pressedMissionButtonIndex)
                {
                    if (button.TryGetComponent(out UIWidgetNavigation navigation))
                        CoroutineUtils.WaitForUI(() => ProcessManager.GetSystem<UINavigationManager>().Select(navigation));
                }
                button.Setup(new UIWidgetButtonParameters(label, callback));
                button.SetInteractable(true);
                if (button.TryGetComponent(out UIWidgetNavigation selectedNavigation))
                    selectedNavigation.OnWidgetSelected += Select;
            }
            m_containerNavigation.UpdateChildWidgets();
        }

        // 060031fe (1.10.1 RVA 0x62C830): the three XP buttons run lambdas 33_0..33_2 (AddXP), the reset button ResetXP.
        private void OnChallengeManagerValid(ChallengeManager challengeManager)
        {
            m_challengeManager = challengeManager;
            m_challengeTestModeToggle.SetIsOnWithoutNotify(challengeManager.TestModeActive);
            m_challengeFeatureToggle.SetIsOnWithoutNotify(challengeManager.FeatureState != ChallengeManager.ChallengeFeatureState.Locked);
            m_challengeCycleButtonNext.onClick.AddListener(ChallengeCycleAdvance);
            m_challengeCycleButtonPrevious.onClick.AddListener(ChallengeCycleRegress);
            OnUpdatedChallengeCycle();
            // The AddXP amounts below are inferred from the field names: retail AddXP ignores its argument, so they
            // are not recoverable from the binary.
            m_xpPlus1k.onClick.AddListener(() => AddXP(1000));
            m_xpPlus10k.onClick.AddListener(() => AddXP(10000));
            m_xpPlus100k.onClick.AddListener(() => AddXP(100000));
            m_xpReset.onClick.AddListener(ResetXP);
            UpdateChallengeXPLabel();
            m_containerNavigation.UpdateChildWidgets();
        }

        // 060031ff (1.10.1 RVA 0x62CB70)
        private void UpdateChallengeXPLabel()
        {
            ChallengeXP.Value = string.Format("Challenge XP: {0}", m_challengeManager.GetCurrentXP());
        }

        // 06003200 (1.10.1 RVA 0x62CC10): only the label refresh remains in retail; xpAmount is never read.
        private void AddXP(int xpAmount)
        {
            UpdateChallengeXPLabel();
        }

        // 06003201 (1.10.1 RVA 0x62CCB0)
        private void ResetXP()
        {
            UpdateChallengeXPLabel();
        }

        // 06003202 (1.10.1 RVA 0x62CD50)
        private void ChallengeCycleAdvance()
        {
            m_challengeManager.DebugAdvanceCycle();
            ChallengeCycleLabel.Value = m_challengeManager.GetCycleDebugLabel();
        }

        // 06003203 (1.10.1 RVA 0x62CDA0)
        private void ChallengeCycleRegress()
        {
            m_challengeManager.DebugRegressCycle();
            ChallengeCycleLabel.Value = m_challengeManager.GetCycleDebugLabel();
        }

        // 06003204 (1.10.1 RVA 0x62CB30)
        private void OnUpdatedChallengeCycle()
        {
            ChallengeCycleLabel.Value = m_challengeManager.GetCycleDebugLabel();
        }

        // 06003205 (1.10.1 RVA 0x62CDF0)
        private void OnDreamPowerStoreManagerValid(DreamPowerStoreManager dreamPowerStoreManager)
        {
            m_unlockDreamPowersToggle.SetIsOnWithoutNotify(dreamPowerStoreManager.IsDebugUnlockAll());
            m_containerNavigation.UpdateChildWidgets();
        }

        // 06003206 (1.10.1 RVA 0x62C640): the next mission is offered when the mission select container cannot do so.
        private void SetupForMenu()
        {
            if (CanSetupForMissionSelect()) return;
            SaveDataLevelMission nextMission = m_saveManagerRef.Get().GetNextMission();
            if (nextMission != null) SetupForMission(nextMission);
        }

        // 06003207 (1.10.1 RVA 0x62CE30): with a mission select container open, the first mission of the level that
        // can be set up gets its button. Every path after the container and mission list are found returns true.
        private bool CanSetupForMissionSelect()
        {
            if (FindObjectOfType<UIContainerMissionSelect>() == null) return false;
            App app = m_appRef.Get();
            SaveManager saveManager = m_saveManagerRef.Get();
            GameplayLevelDefinition levelDefinition = app.Storage.GetValue<MetaGameState>(AppFSMKeys.MetaGameState, null, true).LevelDefinition;
            SaveDataLevel levelData = saveManager.CurrentSave.GetOrCreateLevelData(levelDefinition.GetGUID());
            if (levelDefinition.MissionList == null) return false;
            IReadOnlyList<MissionDefinition> missions = levelDefinition.MissionList.GetMissions(false);
            for (int i = 0; i < missions.Count; i++)
            {
                if (SetupForMission(levelData.GetOrCreateMissionData(missions[i].GetGUID()))) break;
            }
            return true;
        }

        // 06003208 (1.10.1 RVA 0x62D180): no button for an unknown mission, or for a completed one already ranked S or whose best time has no rank.
        // The button writes the best rank's time less 0.1 seconds, marks the mission complete and rebuilds the menu.
        private bool SetupForMission(SaveDataLevelMission nextMissionSave)
        {
            MissionDefinition nextMission = ProcessManager.GetSystem<MissionManager>().GetMissionDefinition(nextMissionSave.GUID);
            if (nextMission == null) return false;
            if (nextMissionSave.Complete)
            {
                if (!nextMission.TryGetRankForTimeSeconds(nextMissionSave.BestTimeSeconds, out MissionRank missionRank)) return false;
                if (missionRank.RankType == RankType.S) return false;
            }
            UIWidgetButton button = Instantiate(m_missionCompleteButtonPrefab, m_missionCompleteRoot);
            button.gameObject.SetActive(true);
            string label = "Complete mission: " + nextMission.name;
            button.Setup(new UIWidgetButtonParameters(label, () =>
            {
                if (nextMission.Ranks != null)
                    nextMissionSave.BestTimeSeconds = nextMission.GetBestRank().TimeInSeconds - 0.1f;
                nextMissionSave.MarkComplete(1);
                Destroy(button.gameObject);
                SetupForMenu();
            }));
            void Select(UIWidgetNavigation _) => m_scrollRectSelection.Select((RectTransform)button.transform, true);
            if (button.TryGetComponent(out UIWidgetNavigation navigation))
                navigation.OnWidgetSelected += Select;
            m_containerNavigation.UpdateChildWidgets();
            return true;
        }

        // 06003209 (1.10.1 RVA 0x62C6C0): the button label is the mission name and its progress; pressing it
        // completes the tracker's first incomplete objective and rebuilds the buttons with this one pre-selected.
        public Action SetupMissionObjectiveButton(MissionState activeMissionState, out string label, int buttonIndex)
        {
            label = string.Format("{0}: {1}/{2}", activeMissionState.Definition.name, activeMissionState.Progress, activeMissionState.Target);
            return Callback;

            void Callback()
            {
                MissionInstancedTracker instancedTracker = activeMissionState.Tracker as MissionInstancedTracker;
                if (instancedTracker == null) return;
                foreach (MissionObjective objective in instancedTracker.MissionObjectives)
                {
                    if (objective.IsComplete) continue;
                    objective.Action_ObjectiveComplete();
                    break;
                }
                m_pressedMissionButtonIndex = buttonIndex;
                OnMissionManagerValid(ProcessManager.GetSystem<MissionManager>());
            }
        }

        // 0600320a (1.10.1 RVA 0x62D4C0)
        public void Action_OnUnlockAllLevelsToggle(bool active)
        {
            SetUnlockAllLevels(active);
        }

        // 0600320b (1.10.1 RVA 0x62BB50)
        private static void SetUnlockAllLevels(bool active)
        {
            if (active) DebugUnlockLevels.Activate();
            else DebugUnlockLevels.Deactivate();
        }

        // 0600320c (1.10.1 RVA 0x62D530)
        private void OnOrnamentManagerValid(OrnamentManager manager)
        {
            m_ornamentManager = manager;
            m_ornamentToggle.SetIsOnWithoutNotify(manager.DebugAllUnlocked);
        }

        // 0600320d (1.10.1 RVA 0x62D570)
        public void Action_OnUnlockAllOrnamentsToggle(bool active)
        {
            m_ornamentManager.DebugAllUnlocked = active;
        }

        // 0600320e (1.10.1 RVA 0x62D590): the character is looked up and nothing is toggled in retail.
        public void Action_ToggleInvincibility(bool _)
        {
            Character character = null;
            if (!m_characterManagerRef.IsNull())
                m_characterManagerRef.Get().TryGetCurrentCharacter(out character);
        }

        // 0600320f (1.10.1 RVA 0x62D610)
        public void Action_OnFPSToggle(bool active)
        {
            VisualQualityManager_SDT.Debug_SetFPSCounter(active);
        }

        // 06003210 (1.10.1 RVA 0x62D620)
        private void SetFPSToggle(bool active)
        {
            VisualQualityManager_SDT.Debug_SetFPSCounter(active);
        }

        // 06003211 (1.10.1 RVA 0x62BBC0)
        private void SetPoolsToggleState()
        {
            if (m_trackManagerRef.TryGet(out TrackManager trackManager))
                m_poolsToggle.SetIsOnWithoutNotify(trackManager.DebugInstancePoolsUIIsOpen());
        }

        // 06003212 (1.10.1 RVA 0x62D630)
        public void Action_OnPoolsToggle(bool active)
        {
            if (m_trackManagerRef.TryGet(out TrackManager trackManager))
                trackManager.DebugToggleInstancePoolsInfo();
        }

        // 06003213 (1.10.1 RVA 0x62D690)
        public void Action_OnMissionTimerToggle(bool active)
        {
            MissionTimer.Debug_SetUnlimitedTime(active);
        }

        // 06003214 (1.10.1 RVA 0x62BCD0): the button offers the respawn point after the character's last one.
        private void SetRespawnButton(IGraphUser character)
        {
            RespawnPoint nextRespawnPoint = GetNextRespawnPoint(character.Storage.GetValue<Transform>(ActorFSMKeys.LastRespawnPoint, null, true));
            m_respawnButton.Setup(new UIWidgetButtonParameters("Respawn: " + nextRespawnPoint.gameObject.name, Action_OnRespawnRequest));
        }

        // 06003215 (1.10.1 RVA 0x62D9C0): the character moves to the next respawn point, keeping no velocity, and
        // is flagged as out of bounds so that the respawn teleports it.
        public void Action_OnRespawnRequest()
        {
            Character character = m_characterManagerRef.Get().GetCurrentCharacterUnsafe();
            RespawnPoint nextRespawnPoint = GetNextRespawnPoint(character.Storage.GetValue<Transform>(ActorFSMKeys.LastRespawnPoint, null, true));
            character.Storage.SetValue(ActorFSMKeys.LastRespawnPoint, nextRespawnPoint.transform);
            character.Storage.SetValue(ActorFSMKeys.RespawnRetainVelocity, false);
            character.Storage.SetValue(ActorFSMKeys.RespawnTeleportRequired, true);
            character.Storage.SetValue(ActorFSMKeys.IsOutOfBounds, true);
            SetRespawnButton(character);
        }

        // 06003216 (1.10.1 RVA 0x62D6D0): the point after the one whose transform is given, wrapping to the first.
        // An unknown transform also gives the first point, and so does the last point's own transform.
        private static RespawnPoint GetNextRespawnPoint(Transform previousRespawnTransform)
        {
            IReadOnlyList<RespawnPoint> respawnPoints = ProcessManager.GetSystem<LevelRespawnPoints>().GetRespawnPoints();
            for (int i = 0; i < respawnPoints.Count; i++)
            {
                if (respawnPoints[i].transform != previousRespawnTransform) continue;
                if (i < respawnPoints.Count - 1) return respawnPoints[i + 1];
            }
            return respawnPoints[0];
        }

        // 06003217 (1.10.1 RVA 0x62DDD0)
        public void Action_TestNotification()
        {
            ProcessManager.GetSystem<NotificationManager>().Debug_TestNotificationInOneMinute();
        }

        // 06003218 (1.10.1 RVA 0x62DE40)
        public void Action_OnChallengeTestModeToggle(bool value)
        {
            ProcessManager.GetSystem<ChallengeManager>().DebugSetTestMode(value);
        }

        // 06003219 (1.10.1 RVA 0x62DEC0)
        public void Action_OnChallengeFeatureToggle(bool value)
        {
            ProcessManager.GetSystem<ChallengeManager>().DebugSetFeatureState(
                value ? ChallengeManager.ChallengeFeatureState.UnlockedShowInterstitial : ChallengeManager.ChallengeFeatureState.Locked);
        }

        // 0600321a (1.10.1 RVA 0x62DF40)
        public void Action_UnlockDreamPowersToggle(bool value)
        {
            DreamPowerStoreManager dreamPowerStoreManager = ProcessManager.GetSystem<DreamPowerStoreManager>();
            if (value) dreamPowerStoreManager.BuyAllPowers();
            else dreamPowerStoreManager.ResetBoughtPowers();
        }

        // 0600321b (1.10.1 RVA 0x62DFC0)
        public void Action_OnWidgetSelected(RectTransform rectTransform)
        {
            m_scrollRectSelection.Select(rectTransform, true);
        }

        // 0600321c (1.10.1 RVA 0x62DFE0): field initialisers precede the UIContainer base call.
        public UIContainerDebug() { }
    }
}
