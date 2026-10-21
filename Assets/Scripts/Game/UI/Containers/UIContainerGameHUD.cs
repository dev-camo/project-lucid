using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Hardlight.UI.Binding;

namespace HardlightProject
{
    // Original Game.Runtime 020008be and 020008bf (closure); bodies 0x6323C0..0x633F90 (1.10.1 x86-64).
    // The gameplay HUD: collectable counters, boss health, character switching and the game speed toggle.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerGameHUD : UIContainer
    {
        [SerializeField]
        private Transform m_childContainerParent;
        [SerializeField]
        private UIContainerCollectableCounter[] m_collectableContainers;
        [SerializeField]
        private UIContainerBossHealth m_bossHealthContainerPrefab;
        [SerializeField]
        private UIVisibilityGroupOverrider m_focusedUIOverrides;
        [SerializeField]
        private UIVisibilityGroupOverrider m_controllerUIOverrides;
        public readonly Bindable<bool> CharacterSelectAllowed = new Bindable<bool>();
        public readonly Bindable<bool> CharacterSelectVisible = new Bindable<bool>();
        public readonly Bindable<bool> GameSpeedToggleVisible = new Bindable<bool>();
        public readonly Bindable<bool> GameSpeedApplied = new Bindable<bool>();
        private readonly SystemRef<CollectableManager> m_collectableManagerRef = ProcessManager.GetSystemRef<CollectableManager>(null, true);
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);
        private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>(null, true);
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);
        private App m_app;
        private bool m_inFocusMode;
        private bool m_isCollectingObjective;
        private InputBridge m_inputBridge;
        private List<CharacterId> m_validCharacterIds = new List<CharacterId>();
        private UIContainerBossHealth m_bossHealthContainer;

        // 0600326f
        private SaveDataSettings SaveDataSettings => m_saveManagerRef.Get().GetSaveDataSettings();

        // 06003270: the boss health container is instanced and set up without parameters.
        private void Start()
        {
            m_app = ProcessManager.GetSystem<App>();
            ShowCollectableViews();
            SetupBossUI();
            DetermineIfCharacterSelectIsActive();
            ListenToMissionObjectiveCollect();
            ControlMapping.Subscribe(GameInput.CharacterSelectNext, OnCharacterInputSelectNext);
            ControlMapping.Subscribe(GameInput.CharacterSelectPrev, OnCharacterInputSelectPrev);
            m_inputBridge = ProcessManager.GetSystem<UIRuntimeConfiguration>().InputBridge;
            OnUpdateLastInputType(m_inputBridge.GetLastInputType());
            InputBridge.OnUpdateLastInputType += OnUpdateLastInputType;
        }

        // 06003271: every subscription made in Start is undone, the objective callbacks only while the
        // active mission still has an instanced tracker.
        private void OnDestroy()
        {
            if (m_collectableManagerRef.IsValid())
                m_collectableManagerRef.Get().OnCollectableStateUpdate -= OnCollectableStateUpdate;
            if (m_missionManagerRef.TryGet(out MissionManager missionManager)
                && missionManager.TryGetActiveMissionState(out MissionState missionState)
                && missionState.Tracker is MissionInstancedTracker instancedTracker)
            {
                instancedTracker.RemoveOnObjectiveCollected(DisableCharacterSelectByMissionObjectiveCollected);
                instancedTracker.OnStatusChanged -= EnableCharacterSelectByMissionObjectiveCollected;
            }
            ControlMapping.Unsubscribe(GameInput.CharacterSelectNext, OnCharacterInputSelectNext);
            ControlMapping.Unsubscribe(GameInput.CharacterSelectPrev, OnCharacterInputSelectPrev);
            InputBridge.OnUpdateLastInputType -= OnUpdateLastInputType;
        }

        // 06003272: focus mode follows the app's HUD focus flag. The speed toggle shows while the
        // maximum game speed is above the saved speed and the toggle is enabled in the settings, and
        // character selection is allowed while the character can switch, no boss bar is showing and
        // the canvas is on.
        private void Update()
        {
            bool focusMode = m_app.Storage.GetValue(AppFSMKeys.UIHUDFocusMode, false, true);
            if (m_inFocusMode != focusMode)
            {
                if (focusMode)
                    EnterFocusMode();
                else
                    LeaveFocusMode();
                m_inFocusMode = focusMode;
            }
            TimeManager timeManager = m_timeManagerRef.Get();
            GameSpeedToggleVisible.Value = timeManager.GameSpeedMax > SaveDataSettings.GameSpeed
                && SaveDataSettings.GameSpeedToggleEnabled;
            GameSpeedApplied.Value = timeManager.GameSpeedApplied;
            if (CharacterSelectVisible.Value && m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character))
            {
                CharacterSelectAllowed.Value = CharacterSelectActive(character)
                    & !m_bossHealthContainer.isActiveAndEnabled
                    & m_canvas.enabled;
            }
        }

        // 06003273
        private void EnterFocusMode()
        {
            m_focusedUIOverrides.ActivateOverrides();
        }

        // 06003274
        private void LeaveFocusMode()
        {
            m_focusedUIOverrides.DeactivateOverrides();
        }

        // 06003275
        private bool CharacterSelectActive(Character character)
        {
            if (m_isCollectingObjective)
                return false;
            return character.CanSwitch();
        }

        // 06003276
        private void OnCharacterInputSelectNext(float amount)
        {
            ChangeCharacter(m_characterManagerRef.Get().GetCurrentCharacterUnsafe().IdType, 1);
        }

        // 06003277
        private void OnCharacterInputSelectPrev(float amount)
        {
            ChangeCharacter(m_characterManagerRef.Get().GetCurrentCharacterUnsafe().IdType, -1);
        }

        // 06003278: step through the active mission's characters, wrapping at both ends.
        private void ChangeCharacter(CharacterId currentCharacterId, int indexChange)
        {
            if (!CharacterSelectVisible.Value || !CharacterSelectAllowed.Value)
                return;
            CharacterManager characterManager = m_characterManagerRef.Get();
            m_missionManagerRef.Get().GetCharactersForActiveMission(ref m_validCharacterIds);
            int index = m_validCharacterIds.FindIndex(id => id == currentCharacterId) + indexChange;
            if (index < 0)
                index = m_validCharacterIds.Count - 1;
            else if (index >= m_validCharacterIds.Count)
                index = 0;
            characterManager.SwapCharacter(m_validCharacterIds[index], true);
        }

        // 06003279
        public void Action_GameSpeedToggle()
        {
            TimeManager timeManager = m_timeManagerRef.Get();
            timeManager.GameSpeedApplied = !timeManager.GameSpeedApplied;
            timeManager.UpdateGameSpeedOverride();
            GameSpeedApplied.Value = timeManager.GameSpeedApplied;
        }

        // 0600327a
        private void ShowCollectableViews()
        {
            if (m_collectableManagerRef.IsValid())
                SetupCollectableViews(m_collectableManagerRef.Get());
            else
                m_collectableManagerRef.OnSystemStartup += SetupCollectableViews;
        }

        // 0600327b
        private void SetupBossUI()
        {
            m_bossHealthContainer = Instantiate(m_bossHealthContainerPrefab, m_childContainerParent);
            m_bossHealthContainer.Setup(null);
        }

        // 0600327c: each counter is set up from the current state of its collectable.
        private void SetupCollectableViews(CollectableManager collectableManager)
        {
            collectableManager.OnCollectableStateUpdate += OnCollectableStateUpdate;
            for (int i = 0; i < m_collectableContainers.Length; i++)
            {
                UIContainerCollectableCounter container = m_collectableContainers[i];
                CollectableType type = container.Type;
                bool exists = collectableManager.TryGetCollectableState(type, out CollectableState state);
                CollectableDefinition definition = collectableManager.GetCollectableDefinition(type);
                container.Setup(new UIContainerCollectableCounterParameters(state, definition, exists));
            }
        }

        // 0600327d
        private void OnCollectableStateUpdate(CollectableType collectableType, CollectableState collectableState, CollectableChangeMetadata metadata)
        {
            for (int i = 0; i < m_collectableContainers.Length; i++)
            {
                if (m_collectableContainers[i].Type == collectableType)
                    m_collectableContainers[i].OnCollectableStateUpdate(collectableState, metadata);
            }
        }

        // 0600327e
        private void OnUpdateLastInputType(UIInputType lastInputType)
        {
            if (m_inputBridge.DoesInputTypeRequireSelection(lastInputType))
                m_controllerUIOverrides.ActivateOverrides();
            else
                m_controllerUIOverrides.DeactivateOverrides();
        }

        // 0600327f: character selection is always shown with every level unlocked or without an active
        // mission. A mission with an allowed-character override shows it when those characters differ
        // in archetype; otherwise it needs at least two archetypes.
        private void DetermineIfCharacterSelectIsActive()
        {
            if (DebugUnlockLevels.AreAllLevelsUnlocked())
            {
                CharacterSelectVisible.Value = true;
                return;
            }
            if (!m_missionManagerRef.TryGet(out MissionManager missionManager) || !missionManager.HasActiveMission())
            {
                CharacterSelectVisible.Value = true;
                return;
            }
            if (missionManager.ActiveMissionContext?.OverrideAllowedCharacters?.Count > 0)
            {
                DataManager dataManager = m_dataManagerRef.Get();
                IReadOnlyList<CharacterId> allowedCharacters = missionManager.ActiveMissionContext.OverrideAllowedCharacters;
                CharacterArchetype firstArchetype = dataManager.Characters[allowedCharacters[0]].Archetype;
                bool archetypesDiffer = false;
                for (int i = 1; i < allowedCharacters.Count; i++)
                {
                    if (dataManager.Characters[allowedCharacters[i]].Archetype != firstArchetype)
                    {
                        archetypesDiffer = true;
                        break;
                    }
                }
                CharacterSelectVisible.Value = archetypesDiffer;
                return;
            }
            if (missionManager.GetCharacterArchetypesForActiveMission().Count >= 2)
                CharacterSelectVisible.Value = true;
        }

        // 06003280: character selection pauses while an objective is being collected.
        private void ListenToMissionObjectiveCollect()
        {
            if (m_missionManagerRef.Get().TryGetActiveMissionState(out MissionState missionState)
                && missionState.Tracker is MissionInstancedTracker instancedTracker)
            {
                instancedTracker.InvokeOnObjectiveCollected(DisableCharacterSelectByMissionObjectiveCollected);
                instancedTracker.OnStatusChanged += EnableCharacterSelectByMissionObjectiveCollected;
            }
        }

        // 06003281
        private void DisableCharacterSelectByMissionObjectiveCollected()
        {
            m_isCollectingObjective = true;
        }

        // 06003282
        private void EnableCharacterSelectByMissionObjectiveCollected(MissionInstancedTracker instancedTracker)
        {
            m_isCollectingObjective = false;
        }

        // 06003283
        public UIContainerGameHUD() { }
    }
}
