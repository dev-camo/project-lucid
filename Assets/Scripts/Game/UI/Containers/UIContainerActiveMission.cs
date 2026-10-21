using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000876; bodies translated from the 1.10.1 x86-64 binary.
    // The in-game active mission panel: description, timers, mission and rank iconography.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerActiveMission : UIContainer
    {
        [SerializeField] private UIWidgetArchetypeImageSpawner m_characterArchetypeImageSpawner;
        [Tooltip("Elapsed time format. Parameters: {0} minutes, {1} seconds.")]
        [SerializeField] private Strings m_timeFormatString = Strings.MISSION_TIMER_ELAPSED_FORMAT;
        [Tooltip("Will display mission information for sub-missions if they have an island override.")]
        [SerializeField] private bool m_includeIslandOverrides;
        [Tooltip("By default the rank times are calculated once at the start, with this option the container will continuously synchronise the rank times with the mission times.")]
        [SerializeField] private bool m_syncRankTimeWithMissionTime;
        [Tooltip("By default the mission icon will be set from definition, if this is true then the icon will be rank achieved by the mission's elapsed time.")]
        [SerializeField] private bool m_useMissionTimeRankIcon;
        [Tooltip("By default the best time is not displayed, this turns it on for time trial missions.")]
        [SerializeField] private bool m_showBestTime;
        [Tooltip("Hide rank information, if the best time is invalid.")]
        [SerializeField] private bool m_hideRankIfBestTimeIsInvalid;
        [SerializeField]
        [Tooltip("Optional animation to play on the mission icon, for situations such as when the ranks are changing.")]
        private Animation m_missionIconAnimation;
        public readonly Bindable<bool> ShowMission = new Bindable<bool>();
        public readonly Bindable<string> MissionDescription = new Bindable<string>();
        public readonly Bindable<Sprite> MissionIcon = new Bindable<Sprite>();
        public readonly Bindable<Sprite> MissionIconBackground = new Bindable<Sprite>();
        public readonly Bindable<Sprite> MissionIconForeground = new Bindable<Sprite>();
        public readonly Bindable<bool> MissionComplete = new Bindable<bool>();
        public readonly Bindable<bool> IsUnlocked = new Bindable<bool>();
        public readonly Bindable<bool> IsChallenge = new Bindable<bool>();
        public readonly Bindable<string> TimeElapsedFormatted = new Bindable<string>();
        public readonly Bindable<string> TimeRemainingFormatted = new Bindable<string>();
        public readonly Bindable<float> TimeTotalSeconds = new Bindable<float>();
        public readonly Bindable<bool> ShowBestTime = new Bindable<bool>();
        public readonly Bindable<bool> NewBestTime = new Bindable<bool>();
        public readonly Bindable<string> BestTimeElapsedFormatted = new Bindable<string>();
        public readonly Bindable<Sprite> BestTimeRankIcon = new Bindable<Sprite>();
        public readonly Bindable<Sprite> BestTimeRankIconBackground = new Bindable<Sprite>();
        public readonly Bindable<Sprite> BestTimeRankIconForeground = new Bindable<Sprite>();
        public readonly Bindable<bool> ShowRank = new Bindable<bool>();
        public readonly Bindable<string> NextRankTimeToBeatFormatted = new Bindable<string>();
        public readonly Bindable<Sprite> NextRankIcon = new Bindable<Sprite>();
        public readonly Bindable<Sprite> NextRankIconBackground = new Bindable<Sprite>();
        public readonly Bindable<Sprite> NextRankIconForeground = new Bindable<Sprite>();
        public readonly Bindable<string> LowestRankTimeElapsedFormatted = new Bindable<string>();
        public readonly Bindable<Sprite> LowestRankIcon = new Bindable<Sprite>();
        public readonly Bindable<Sprite> LowestRankIconBackground = new Bindable<Sprite>();
        public readonly Bindable<Sprite> LowestRankIconForeground = new Bindable<Sprite>();
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>(null, true);
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);
        private readonly List<ManagedAddressableAsset<Sprite>> m_managedAddressableSprites = new List<ManagedAddressableAsset<Sprite>>();
        private MissionManager m_missionManager;
        private MissionState m_currentMissionState;
        private MissionRank m_currentMissionRank;
        private bool m_showRank;
        private bool m_shouldPlayRankAudio;

        // 0x060030e9 (0x617760)
        private void Start()
        {
            m_missionManagerRef.InvokeOnValid(OnMissionManagerValid);
            StringTable.StringsTableHandlers += Refresh;
        }

        // 0x060030ea (0x6178B0): releases every icon loaded by SetIcon and unsubscribes.
        private void OnDestroy()
        {
            StringTable.StringsTableHandlers -= Refresh;
            foreach (ManagedAddressableAsset<Sprite> managedAddressableSprite in m_managedAddressableSprites)
                managedAddressableSprite.Unload();
            if (m_missionManagerRef.TryGet(out MissionManager missionManager))
                missionManager.OnMissionIslandActive -= OnMissionIslandActive;
            if (m_identifier != null)
                m_identifier.Close();
        }

        // 0x060030eb (0x617B50)
        private void Update()
        {
            SynchroniseRankTimes();
        }

        // 0x060030ec (0x617D00): without island overrides the active mission is shown once. Otherwise
        // an island override (or else the active mission) is shown and followed as islands change.
        private void OnMissionManagerValid(MissionManager missionManager)
        {
            MissionState missionState;
            m_missionManager = missionManager;
            if (!m_includeIslandOverrides && missionManager.TryGetActiveMissionState(out missionState))
            {
                Refresh(missionState);
                return;
            }
            if (missionManager.TryGetIslandOverrideMissionState(out missionState) || missionManager.TryGetActiveMissionState(out missionState))
            {
                Refresh(missionState);
                missionManager.OnMissionIslandActive += OnMissionIslandActive;
            }
        }

        // 0x060030ed (0x6184B0)
        private void OnMissionIslandActive(MissionState missionState)
        {
            if (m_currentMissionState != missionState)
                Refresh(missionState);
        }

        // 0x060030ee (0x6184D0)
        public void Refresh()
        {
            Refresh(m_currentMissionState);
        }

        // 0x060030ef (0x617E00)
        public void Refresh(MissionState missionState)
        {
            m_currentMissionState = missionState;
            if (missionState == null || m_missionManager == null)
            {
                ShowMission.Value = false;
                return;
            }
            if (m_missionIconAnimation != null && m_missionIconAnimation.playAutomatically)
                m_missionIconAnimation.Play();
            SaveManager saveManager = m_saveManagerRef.Get();
            MissionDefinition missionDefinition = missionState.Definition;
            IMissionContext activeMissionContext = m_missionManager.ActiveMissionContext;
            float timeElapsed = 0f;
            if (missionState.IsInstanceCompleted || missionState.TimerStarted())
                timeElapsed = missionState.TrackerTimeElapsed;
            float bestTime = activeMissionContext.BestTimeSeconds;
            float timeTotal = (float)(missionDefinition.TimeLimitSeconds + missionDefinition.ObjectivesAdditionalTotalTimeSeconds);
            float timeRemaining = (float)(timeTotal - timeElapsed);
            ShowMission.Value = true;
            // Non-short-circuit: SetUpComplete is read even when the archetype requirement is met.
            bool unlocked = missionDefinition.ArchetypeRequirementMet(saveManager.CurrentSave) | missionState.SetUpComplete;
            IsUnlocked.Value = unlocked;
            UpdateMissionDescription(missionState, missionDefinition, unlocked);
            MissionComplete.Value = missionState.Complete;
            TimeElapsedFormatted.Value = MissionStringsUtil.GetMissionTimerFormat(timeElapsed, m_timeFormatString);
            TimeTotalSeconds.Value = timeTotal;
            TimeRemainingFormatted.Value = MissionStringsUtil.GetMissionTimerFormat(timeRemaining, m_timeFormatString);
            UpdateIconography(activeMissionContext.MissionDefinition, missionDefinition, bestTime, timeElapsed);
            ShowBestTime.Value = m_showBestTime && m_showRank && missionDefinition.IsTimeTrial() && bestTime > 0f && missionDefinition.IsReachGoalTracker;
            NewBestTime.Value = bestTime >= timeElapsed;
            if (missionDefinition.CompletedMissionOverrides != null && missionDefinition.CompletedMissionOverrides.FailOnTimeLimitReached)
                bestTime = (float)(timeTotal - bestTime);
            BestTimeElapsedFormatted.Value = MissionStringsUtil.GetMissionTimerFormat(bestTime, m_timeFormatString);
            if (m_characterArchetypeImageSpawner != null && m_missionManagerRef.IsValid())
            {
                CharacterArchetype characterArchetype = activeMissionContext.MissionDefinition.CharacterArchetype;
                CharacterId characterId = CharacterId.None;
                if (activeMissionContext.MissionDefinition.ExclusiveToCharacter)
                    characterId = activeMissionContext.MissionDefinition.CharacterId;
                m_characterArchetypeImageSpawner.Spawn(characterArchetype, characterId);
            }
            IsChallenge.Value = m_missionManagerRef.IsValid() && m_missionManagerRef.Get().ActiveMissionContext is MissionContextChallenge;
        }

        // 0x060030f0 (0x6184F0): a locked archetype mission shows the archetype's locked text,
        // otherwise the active context's own description or the formatted mission name.
        private void UpdateMissionDescription(MissionState missionState, MissionDefinition missionDefinition, bool unlocked)
        {
            if (!unlocked && missionDefinition.CharacterArchetype != CharacterArchetype.None)
            {
                if (m_appRef.Get().DataManager.CharacterArchetypes.TryGetValue(missionDefinition.CharacterArchetype, out CharacterArchetypeDefinition archetypeDefinition))
                    MissionDescription.Value = StringTable.GetString(archetypeDefinition.LockedMissionStringId);
                return;
            }
            IMissionContext activeMissionContext = m_missionManagerRef.Get().ActiveMissionContext;
            MissionDefinition contextDefinition = activeMissionContext != null ? activeMissionContext.MissionDefinition : null;
            MissionDescription.Value = contextDefinition == missionDefinition ? activeMissionContext.Description : missionState.Definition.GetFormattedMissionName();
        }

        // 0x060030f1 (0x618C80)
        public void SetRankAudioActive()
        {
            m_shouldPlayRankAudio = true;
        }

        // 0x060030f2 (0x618C90)
        public void PlayRankAudio()
        {
            if (m_currentMissionRank == null)
                return;
            RankDefinition rankDefinition = m_currentMissionRank.GetRankTypeDefinition();
            if (rankDefinition == null)
                return;
            if (rankDefinition.AudioClipIdentifier == HLAudioClipIdentifier.None)
                return;
            ProcessManager.GetSystem<AudioManager>(null, true).PlayOneShotAtSource(rankDefinition.AudioClipIdentifier, HLAudioSourceIdentifier.Ui, 0f, 1f);
        }

        // 0x060030f3 (0x618750): time trials show the next rank to beat, the rank of the best time
        // and the lowest rank; other missions only show their objective icon.
        private void UpdateIconography(MissionDefinition activeContextMissionDefinition, MissionDefinition missionDefinition, float bestTimeSeconds, float timeElapsedSeconds)
        {
            bool showRank = false;
            m_showRank = activeContextMissionDefinition.IsTimeTrial();
            if (m_showRank)
            {
                if (m_hideRankIfBestTimeIsInvalid)
                {
                    showRank = bestTimeSeconds > activeContextMissionDefinition.TheoreticalBestTimePossibleSeconds;
                    m_showRank = showRank;
                }
                else
                {
                    showRank = true;
                }
            }
            ShowRank.Value = showRank;
            if (!m_showRank)
            {
                MissionIcon.Value = missionDefinition.MissionObjectiveIcon;
                MissionIconBackground.Value = null;
                MissionIconForeground.Value = null;
                return;
            }
            if (activeContextMissionDefinition.TryGetRankForTimeSecondsIgnoreMinimumTime(timeElapsedSeconds, out m_currentMissionRank))
            {
                NextRankTimeToBeatFormatted.Value = MissionStringsUtil.GetMissionTimerFormat(AdjustForObjectives(activeContextMissionDefinition, m_currentMissionRank.TimeInSeconds), m_timeFormatString);
                ManagedAddressableAsset<Sprite> baseIcon = m_currentMissionRank.GetBaseAddressable();
                ManagedAddressableAsset<Sprite> backgroundIcon = m_currentMissionRank.GetBackgroundAddressable();
                ManagedAddressableAsset<Sprite> foregroundIcon = m_currentMissionRank.GetForegroundAddressable();
                SetIcon(NextRankIcon, baseIcon);
                SetIcon(NextRankIconBackground, backgroundIcon);
                SetIcon(NextRankIconForeground, foregroundIcon);
                if (m_useMissionTimeRankIcon)
                {
                    SetIcon(MissionIcon, baseIcon);
                    SetIcon(MissionIconBackground, backgroundIcon);
                    SetIcon(MissionIconForeground, foregroundIcon);
                }
                else
                {
                    MissionIcon.Value = missionDefinition.MissionObjectiveIcon;
                    MissionIconBackground.Value = null;
                    MissionIconForeground.Value = null;
                }
            }
            else
            {
                NextRankTimeToBeatFormatted.Value = MissionStringsUtil.GetMissionTimerFormat(0f, m_timeFormatString);
                NextRankIcon.Value = null;
                NextRankIconBackground.Value = null;
                NextRankIconForeground.Value = null;
                MissionIcon.Value = missionDefinition.MissionObjectiveIcon;
                MissionIconBackground.Value = null;
                MissionIconForeground.Value = null;
            }
            if (activeContextMissionDefinition.TryGetRankForTimeSeconds(bestTimeSeconds, out MissionRank bestTimeRank))
            {
                SetIcon(BestTimeRankIcon, bestTimeRank.GetBaseAddressable());
                SetIcon(BestTimeRankIconBackground, bestTimeRank.GetBackgroundAddressable());
                SetIcon(BestTimeRankIconForeground, bestTimeRank.GetForegroundAddressable());
            }
            else
            {
                BestTimeRankIcon.Value = null;
                BestTimeRankIconBackground.Value = null;
                BestTimeRankIconForeground.Value = null;
            }
            MissionRank lowestRank = activeContextMissionDefinition.GetBestRank();
            if (lowestRank != null)
            {
                LowestRankTimeElapsedFormatted.Value = MissionStringsUtil.GetMissionTimerFormat(AdjustForObjectives(activeContextMissionDefinition, lowestRank.TimeInSeconds), m_timeFormatString);
                SetIcon(LowestRankIcon, lowestRank.GetBaseAddressable());
                SetIcon(LowestRankIconBackground, lowestRank.GetBackgroundAddressable());
                SetIcon(LowestRankIconForeground, lowestRank.GetForegroundAddressable());
                if (m_shouldPlayRankAudio)
                    PlayRankAudio();
            }
            else
            {
                LowestRankTimeElapsedFormatted.Value = MissionStringsUtil.GetMissionTimerFormat(0f, m_timeFormatString);
                LowestRankIcon.Value = null;
                LowestRankIconBackground.Value = null;
                LowestRankIconForeground.Value = null;
            }
        }

        // 0x060030f4 (0x617B60): while the mission runs past the displayed rank time, moves the
        // next rank to beat down to the rank for the elapsed time.
        private void SynchroniseRankTimes()
        {
            MissionState activeMissionState;
            if (!m_syncRankTimeWithMissionTime || !m_showRank)
                return;
            if (!m_missionManager.TryGetActiveMissionState(out activeMissionState))
                return;
            float timeElapsed = activeMissionState.TrackerTimeElapsed;
            if (m_currentMissionRank.RankType == RankType.D)
                return;
            if (m_currentMissionRank.TimeInSeconds >= timeElapsed)
                return;
            MissionDefinition missionDefinition = activeMissionState.Definition;
            if (!missionDefinition.TryGetRankForTimeSecondsIgnoreMinimumTime(timeElapsed, out m_currentMissionRank))
                return;
            NextRankTimeToBeatFormatted.Value = MissionStringsUtil.GetMissionTimerFormat(AdjustForObjectives(missionDefinition, m_currentMissionRank.TimeInSeconds), m_timeFormatString);
            SetIcon(NextRankIcon, m_currentMissionRank.GetBaseAddressable());
            SetIcon(NextRankIconBackground, m_currentMissionRank.GetBackgroundAddressable());
            SetIcon(NextRankIconForeground, m_currentMissionRank.GetForegroundAddressable());
            if (m_missionIconAnimation != null)
                m_missionIconAnimation.Play();
        }

        // 0x060030f5 (0x618DC0): the loaded asset is tracked so OnDestroy can release it.
        private void SetIcon(Bindable<Sprite> bindable, ManagedAddressableAsset<Sprite> addressableAsset)
        {
            bindable.Value = addressableAsset.Load();
            m_managedAddressableSprites.Add(addressableAsset);
        }

        // 0x060030f6 (0x618D70): a time-limit mission counts down, so rank times are measured from the limit.
        private static float AdjustForObjectives(MissionDefinition missionDefinition, float timeInSeconds)
        {
            if (missionDefinition.FailOnTimeLimitReached)
                return (float)((float)(missionDefinition.TimeLimitSeconds + missionDefinition.ObjectivesAdditionalTotalTimeSeconds) - timeInSeconds);
            return timeInSeconds;
        }

        // 0x060030f7
        public UIContainerActiveMission() { }
    }
}
