using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008fd; bodies translated from the 1.10.1 x86-64 binary.
    // The pause menu of a level: shows the level and mission results so far, and restarts, respawns or quits.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerPause : UIContainerGameplayControl
    {
        [SerializeField] private UIModernEvent m_resumeEvent;
        [SerializeField] private UIContainerMissionResults m_resultsContainer;
        [SerializeField] private UIContainerMissionResults m_resultsSecondaryContainer;
        [SerializeField] private UIContainerMissionRanks m_missionRanksContainer;
        public readonly Bindable<Strings> LevelTitle = new Bindable<Strings>();
        public readonly Bindable<Strings> LevelSubtitle = new Bindable<Strings>();
        public readonly Bindable<int> DreamNumber = new Bindable<int>();
        public readonly Bindable<bool> ShowFPSWarningMessage = new Bindable<bool>();
        public readonly Bindable<Sprite> ZoneIconAsset = new Bindable<Sprite>();
        public readonly Bindable<Sprite> ActBackground = new Bindable<Sprite>();
        public readonly Bindable<bool> ShowFullscreenBackground = new Bindable<bool>();
        private Coroutine m_closingCoroutine;
        private LevelManager m_levelManager;
        private MissionManager m_missionManager;
        private UIManager m_uiManager;
        private ManagedAddressableAsset<Sprite> m_zoneAccent;

        // 0x060033fe (0x6700F0)
        protected override void Start()
        {
            base.Start();
            ProcessManager.GetSystemRef<UIManager>(null, true).InvokeOnValid(uiManager => m_uiManager = uiManager);
            ProcessManager.GetSystemRef<LevelManager>(null, true).InvokeOnValid(OnLevelManagerValid);
            ProcessManager.GetSystemRef<VisualQualityManager_SDT>(null, true).InvokeOnValid(OnQualityManagerValid);
        }

        // 0x060033ff (0x6702E0)
        private void OnLevelManagerValid(LevelManager levelManager)
        {
            m_levelManager = levelManager;
            ProcessManager.GetSystemRef<MissionManager>(null, true).InvokeOnValid(OnMissionManagerValid);
        }

        // 0x06003400 (0x6703D0)
        private void OnQualityManagerValid(VisualQualityManager_SDT qualityManager)
        {
            ShowFPSWarningMessage.Value = qualityManager.LevelDippedFrames;
            ShowFullscreenBackground.Value = !qualityManager.DeviceCanChangeGraphicsQuality();
        }

        // 0x06003401 (0x670440): fills in the results, ranks, titles, zone icon, act number and background of the current level.
        // Without an active mission only the level information is shown.
        private void OnMissionManagerValid(MissionManager missionManager)
        {
            m_missionManager = missionManager;
            if (!m_levelManager.TryGetCurrentLevel(out LevelManagerLevel currentLevel))
                return;

            GameplayLevelDefinition levelDefinition = currentLevel.LevelDefinition;
            MissionDefinition missionDefinition = null;
            if (missionManager.TryGetActiveMissionState(out MissionState missionState))
            {
                missionDefinition = missionState.Definition;
                m_resultsContainer.Setup(new UIContainerMissionResultsParameters(missionDefinition, levelDefinition));
                if (m_resultsSecondaryContainer != null)
                    m_resultsSecondaryContainer.Setup(new UIContainerMissionResultsParameters(missionDefinition, levelDefinition));

                ChallengeDefinition challengeDefinition = null;
                if (missionManager.ActiveMissionContext is MissionContextChallenge challengeContext)
                    challengeDefinition = challengeContext.ChallengeDefinition;

                m_missionRanksContainer.Setup(new UIContainerRanksParameters(missionDefinition, challengeDefinition));
            }

            LevelTitle.Value = levelDefinition.DisplayName;
            LevelSubtitle.Value = levelDefinition.ActName;
            if (!m_levelManager.GameLevels.TryGetZoneForLevel(levelDefinition, out GameplayLevelCategory zone))
                return;

            if (m_zoneAccent != null)
                m_zoneAccent.Unload();

            m_zoneAccent = UIContainerUtilities.GetZoneAccent(missionDefinition, zone, levelDefinition);
            ZoneIconAsset.Value = m_zoneAccent.Load();

            // The act number is the level's one-based position in its zone, or zero when the zone does not list it.
            string levelGuid = levelDefinition.GetGUID();
            int dreamNumber = 0;
            for (int i = 0; i < zone.GameplayEntries.Count; i++)
            {
                if (zone.GameplayEntries[i].GetGUID() != levelGuid)
                    continue;

                dreamNumber = i + 1;
                break;
            }

            DreamNumber.Value = dreamNumber;
            levelDefinition.LoadLevelImage(sprite => ActBackground.Value = sprite);
        }

        // 0x06003402 (0x670870)
        public void Action_Restart()
        {
            if (m_closingCoroutine != null)
                return;

            m_closingCoroutine = CoroutineUtils.RunCoroutine(Restart());
        }

        // 0x06003403 (0x670980)
        public void Action_Respawn()
        {
            if (m_closingCoroutine != null)
                return;

            m_closingCoroutine = CoroutineUtils.RunCoroutine(Respawn());
        }

        // 0x06003404 (0x670A90): the closing coroutine is never cleared here; ExitLevel leaves the level.
        public void Action_Exit()
        {
            if (m_closingCoroutine != null)
                return;

            m_closingCoroutine = CoroutineUtils.WaitForUI(() => { });
            FireMissionQuitAnalytics();
            ProcessManager.GetSystem<LevelManager>(null, true).ExitLevel(true);
        }

        // 0x06003405 (0x670910): sends the character back to the start, closes the pause menu and then restarts the level.
        private IEnumerator Restart()
        {
            if (ProcessManager.GetSystem<CharacterManager>(null, true).TryGetCurrentCharacter(out Character character))
                character.OnRestart();

            ProcessManager.GetSystem<App>(null, true).AddUniqueUIModernEvent(m_resumeEvent);
            yield return new WaitUntil(() => m_uiManager.IsOpen(m_identifier));
            ProcessManager.GetSystem<LevelManager>(null, true).Restart();
            m_closingCoroutine = null;
        }

        // 0x06003406 (0x670A20): closes the pause menu, waits for any character swap, then puts the character out of bounds
        // so the game respawns it.
        private IEnumerator Respawn()
        {
            ProcessManager.GetSystem<App>(null, true).AddUniqueUIModernEvent(m_resumeEvent);
            if (!m_uiManager.IsOpen(m_identifier))
                yield return new WaitUntil(() => m_uiManager.IsOpen(m_identifier));

            CharacterManager characterManager = ProcessManager.GetSystem<CharacterManager>(null, true);
            if (characterManager.IsCharacterSwapInProgress())
                yield return new WaitUntil(() => !characterManager.IsCharacterSwapInProgress());

            Character character = characterManager.GetCurrentCharacterUnsafe();
            if (character == null)
                yield break;

            character.Storage.SetValue<bool>(ActorFSMKeys.IsOutOfBounds, true);
            m_closingCoroutine = null;
        }

        // 0x06003407 (0x670D20)
        private void Close()
        {
            ProcessManager.GetSystem<App>(null, true).AddUniqueUIModernEvent(m_resumeEvent);
        }

        // 0x06003408 (0x670C10)
        private void FireMissionQuitAnalytics()
        {
            if (m_missionManager.TryGetActiveMissionState(out MissionState missionState))
                m_missionManager.ActiveMissionContext.SendMissionQuitAnalytics(missionState);
        }

        // 0x06003409 (0x670DA0): releases the loaded images and runs, once, whatever the pause screen was asked to do on close.
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (m_zoneAccent != null)
                m_zoneAccent.Unload();

            if (ActBackground.Value != null && m_levelManager.TryGetCurrentLevel(out LevelManagerLevel currentLevel))
                currentLevel.LevelDefinition.UnloadLevelImage();

            App app = ProcessManager.GetSystem<App>(null, true);
            if (app.Storage.HasValue<Action>(AppFSMKeys.OnPauseCloseEvent))
            {
                CoroutineUtils.WaitForUI(app.Storage.GetValue<Action>(AppFSMKeys.OnPauseCloseEvent, null, true));
                app.Storage.RemoveValue<Action>(AppFSMKeys.OnPauseCloseEvent);
            }
        }

        // 0x0600340a (0x6710B0)
        public UIContainerPause()
        {
        }
    }
}
