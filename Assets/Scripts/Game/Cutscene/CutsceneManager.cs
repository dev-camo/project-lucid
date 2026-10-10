using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Playables;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HardlightProject
{
    // Original Game.Runtime 02000438, including its genuine C# event, closure and
    // iterator families. Native evidence: supplied 1.10.1, original 060018d1..06001920.
    // This preserves the shipping manager; offline routing is a separate integration.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CutsceneManager : ISystem
    {
        private App m_app;
        private DataManager m_dataManager;
        private SaveManager m_saveManager;
        private LevelManager m_levelManager;
        private UIManager m_uiManager;
        private UIScreenTransitionManager m_uiScreenTransitionManager;
        private Cutscene m_currentCutscene;
        private CharacterManager m_characterManager;
        private SystemRef<CharacterManager> m_characterManagerRef;
        private readonly SystemRef<FullscreenShaderManager> m_fullscreenShaderManager = ProcessManager.GetSystemRef<FullscreenShaderManager>(null, true);
        private readonly SystemRef<VisualQualityManager_SDT> m_visualQualityManagerRef = ProcessManager.GetSystemRef<VisualQualityManager_SDT>(null, true);
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        private Transform m_virtualCameraContainer;
        private bool m_cutsceneIsLoading;
        private bool m_cutscenePlayOnLoad;
        private CutsceneDefinition m_loadingCutscene;
        private AsyncOperationHandle<GameObject> m_currentCutsceneHandle;
        private readonly List<string> m_cutscenesSeenThisSession = new List<string>();
        private StackableDataHandle m_pauseTimeOverride;
        private StackableDataHandle m_invulnerableOverride;
        private Coroutine m_skipCoroutine;
        private bool m_skipTapOccurred;
        private bool m_skipHoldOccurred;
        private HLAudioClipIdentifier m_previousMusicToRestore;
        private Coroutine m_duckMusicCoroutine;
        private bool m_previousGameplayControl;
        private const string DebugMenuPath = "Cutscenes";
        private readonly string m_debugWatchCutscene = "Cutscenes/Watch";

        public bool IsCutscenePlaying => m_currentCutscene != null;
        public bool CutsceneIsLoading => m_cutsceneIsLoading && m_cutscenePlayOnLoad;
        public bool WasCutsceneSkipped => m_currentCutscene != null && m_currentCutscene.WasSkipped;

        // Original field and method order and independent cached empty delegates.
        public event Action OnCutsceneStarted = () => { };
        public event Action OnCutsceneFinishing = () => { };
        public event Action OnCutsceneFinished = () => { };
        private Coroutine m_cameraTrackingCoroutine;
        public bool SkipTapOccurred { set => m_skipTapOccurred = value; }
        public bool SkipHoldOccurred { set => m_skipHoldOccurred = value; }

        // 060018dc. Registry publication precedes both process subscriptions.
        public CutsceneManager()
        {
            ProcessManager.RegisterSystem(this, null, false, false);
            this.SubscribeToAction(SystemAction.Initialise, PreInitialise);
            this.SubscribeToAction(SystemAction.Shutdown, Shutdown);
        }

        private void PreInitialise(object _)
        {
            ProcessManager.GetSystemRef<DataManager>(null, true).InvokeOnValid(Initialise);
        }

        // 060018de and the five original 06001903..07 assignment lambdas. The
        // character reference uses OnSystemStartup rather than InvokeOnValid.
        private void Initialise(DataManager dataManager)
        {
            m_dataManager = dataManager;
            ProcessManager.GetSystemRef<App>(null, true).InvokeOnValid(manager => m_app = manager);
            ProcessManager.GetSystemRef<SaveManager>(null, true).InvokeOnValid(manager => m_saveManager = manager);
            ProcessManager.GetSystemRef<UIManager>(null, true).InvokeOnValid(manager => m_uiManager = manager);
            ProcessManager.GetSystemRef<LevelManager>(null, true).InvokeOnValid(manager => m_levelManager = manager);
            ProcessManager.GetSystemRef<UIScreenTransitionManager>(null, true).InvokeOnValid(manager => m_uiScreenTransitionManager = manager);
            m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
            m_characterManagerRef.OnSystemStartup += InitialiseCharacterManager;
        }

        private void InitialiseCharacterManager(CharacterManager manager) => m_characterManager = manager;
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        private void InitialiseDebugMenu() { }

        // 060018e1. Retail debug actions are stripped, but the original system
        // lookup, dictionary traversal and deconstruction remain observable.
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        private void DebugAddWatchCutsceneOptions()
        {
            ProcessManager.GetSystem<DebugMenu>(null, true);
            foreach (var pair in m_dataManager.CutsceneDefinitions)
                pair.Deconstruct(out HLCutsceneIdentifier identifier, out CutsceneDefinition definition);
        }

        public void PreloadAnyValidCutscene(CutsceneTriggerType triggerType, Action callback = null)
        {
            if (HasAnyValidCutscene(triggerType, out CutsceneDefinition validCutscene))
                LoadCutscene(validCutscene, callback, false);
        }

        public void PlayAnyValidCutscene(CutsceneTriggerType triggerType, Action callback = null)
        {
            if (HasAnyValidCutscene(triggerType, out CutsceneDefinition validCutscene))
                LoadCutscene(validCutscene, callback, true);
        }

        // 060018e4. ShouldPlay can substitute the previous level's unseen results
        // definition. Preserve the live dictionary iterator and its disposal.
        public bool HasAnyValidCutscene(CutsceneTriggerType triggerType, out CutsceneDefinition validCutscene)
        {
            foreach (var pair in m_dataManager.CutsceneDefinitions)
            {
                pair.Deconstruct(out HLCutsceneIdentifier identifier, out CutsceneDefinition definition);
                CutsceneDefinition candidate = definition;
                if (ShouldPlayCutscene(ref candidate, triggerType))
                {
                    validCutscene = candidate;
                    return true;
                }
            }
            validCutscene = null;
            return false;
        }

        private bool HasUnseenResultsCutsceneInLevel(GameplayLevelDefinition levelDefinition, out CutsceneDefinition validCutscene)
        {
            validCutscene = null;
            if (DebugUnlockLevels.AreAllLevelsUnlocked())
                return false;
            foreach (var pair in m_dataManager.CutsceneDefinitions)
            {
                pair.Deconstruct(out HLCutsceneIdentifier identifier, out CutsceneDefinition definition);
                if (definition.TriggerRequirements.TriggerType != CutsceneTriggerType.OnResultsScreenClosed)
                    continue;
                if (definition.TriggerRequirements.LevelDefinition != levelDefinition)
                    continue;
                if (!RepetitionRuleAllowsPlay(definition))
                    continue;
                validCutscene = definition;
                return true;
            }
            return false;
        }

        // 060018e6. Requirements replace the initial level-match result when
        // present; every requirement and the final prefab lookup still run.
        private bool ShouldPlayCutscene(ref CutsceneDefinition definition, CutsceneTriggerType triggerType)
        {
            if (definition.TriggerRequirements.TriggerType != triggerType)
                return false;
            GameplayLevelDefinition currentLevel = null;
            if (triggerType == CutsceneTriggerType.OnLevelSelected)
            {
                LevelManagerLoadLevelParameters parameters = m_app.Storage.GetValue<LevelManagerLoadLevelParameters>(
                    AppFSMKeys.LevelLoadParameters, null, true);
                if (parameters == null)
                    return false;
                currentLevel = parameters.LevelDefinition;
                if (m_levelManager.GameLevels.TryGetPreviousLevel(currentLevel, out GameplayLevelDefinition previousLevel) &&
                    HasUnseenResultsCutsceneInLevel(previousLevel, out CutsceneDefinition previousResults))
                {
                    definition = previousResults;
                    return true;
                }
            }
            else if (m_levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
            {
                currentLevel = level.LevelDefinition;
            }
            if (!RepetitionRuleAllowsPlay(definition))
                return false;
            bool valid = definition.TriggerRequirements.LevelDefinition == null ||
                currentLevel != null && currentLevel == definition.TriggerRequirements.LevelDefinition;
            RequirementGameplayLevelBase[] requirements = definition.TriggerRequirements.LevelRequirements;
            if (requirements != null && requirements.Length > 0)
            {
                valid = true;
                for (int i = 0; i < requirements.Length; ++i)
                    valid &= requirements[i].RequirementsMet(definition.TriggerRequirements.LevelDefinition);
            }
            CharacterId characterId = CharacterId.None;
            if (m_characterManagerRef.IsValid() && m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character))
                characterId = character.IdType;
            return valid & GetCutscenePrefabForCharacter(definition, characterId, triggerType, out AssetReferenceT<GameObject> prefab);
        }

        private bool GetCutscenePrefabForCharacter(CutsceneDefinition definition, CharacterId characterId,
            CutsceneTriggerType triggerType, out AssetReferenceT<GameObject> prefab)
        {
            if (definition.PrefabsForCharacter.TryGetValue(characterId, out prefab) ||
                definition.PrefabsForCharacter.TryGetValue(CharacterId.None, out prefab))
                return true;
            if (triggerType == CutsceneTriggerType.OnResultsScreenClosed &&
                m_app.Storage.TryGetValue(AppFSMKeys.CharacterLastUsed, out CharacterId lastCharacter))
                return definition.PrefabsForCharacter.TryGetValue(lastCharacter, out prefab);
            return false;
        }

        // 060018e8. PerLevel and unknown enum values follow the saved seen list,
        // just like PlayOnceEver; only the two explicit values take other paths.
        private bool RepetitionRuleAllowsPlay(CutsceneDefinition definition)
        {
            if (definition.RepetitionRule == CutsceneDefinition.CutsceneRepetitionRule.PlayEveryTime)
                return true;
            if (definition.RepetitionRule == CutsceneDefinition.CutsceneRepetitionRule.PlayOncePerSession)
                return !m_cutscenesSeenThisSession.Contains(definition.GetGUID());
            return !m_saveManager.CurrentSave.SeenCutsceneGuids.Contains(definition.GetGUID());
        }

        public bool HasCutsceneBeenSeen(CutsceneDefinition definition) =>
            m_saveManager.CurrentSave.SeenCutsceneGuids.Contains(definition.GetGUID());

        // 060018ea. The existing async completion retains its earlier captured
        // callback. Promoting an unfinished preload does not replace that closure.
        public void LoadCutscene(CutsceneDefinition cutsceneDefinition, Action callback, bool playCutsceneOnLoad = true)
        {
            if (m_cutsceneIsLoading && playCutsceneOnLoad && cutsceneDefinition == m_loadingCutscene)
            {
                if (m_currentCutsceneHandle.IsDone)
                    PlayLoadedCutscene(m_loadingCutscene, callback, m_currentCutsceneHandle.Result);
                else
                {
                    Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.Normal;
                    m_cutscenePlayOnLoad = true;
                }
                return;
            }
            if (m_cutsceneIsLoading && !playCutsceneOnLoad && cutsceneDefinition == m_loadingCutscene)
                return;
            AddressableManager addressableManager = ProcessManager.GetSystem<AddressableManager>(null, true);
            if (!m_app.Storage.TryGetValue(AppFSMKeys.CurrentCharacterId, out CharacterId characterId))
                characterId = CharacterId.None;
            if (!GetCutscenePrefabForCharacter(cutsceneDefinition, characterId,
                cutsceneDefinition.TriggerRequirements.TriggerType, out AssetReferenceT<GameObject> cutscenePrefab))
            {
                HLOutput.LogError(string.Format("Trying to load cutscene {0} for {1} who it was not designed for.",
                    cutsceneDefinition.Identifier, characterId));
                cutscenePrefab = cutsceneDefinition.PrefabsForCharacter.GetValues()[0];
            }
            m_cutsceneIsLoading = true;
            m_loadingCutscene = cutsceneDefinition;
            m_cutscenePlayOnLoad = playCutsceneOnLoad;
            if (!playCutsceneOnLoad)
                Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.Low;
            m_currentCutsceneHandle = addressableManager.LoadAsset<GameObject>(cutscenePrefab, prefab =>
            {
                if (m_cutscenePlayOnLoad)
                    PlayLoadedCutscene(cutsceneDefinition, callback, prefab);
            }, () => { });
        }

        private void PlayLoadedCutscene(CutsceneDefinition cutsceneDefinition, Action callback, GameObject prefab)
        {
            Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.Normal;
            CleanupAnyPreviousCameras();
            if (m_levelManager.TryGetAddressableLevel(cutsceneDefinition.NonGameplaySceneName, out SceneInstance scene))
                SceneManager.SetActiveScene(scene.Scene);
            m_currentCutscene = Object.Instantiate(prefab).GetComponent<Cutscene>();
            PlayCutscene(cutsceneDefinition, callback);
        }

        // 060018ec. Preserve Play-before-time/subscription, original character
        // switch timing, and the same-character early return before Started.
        private void PlayCutscene(CutsceneDefinition cutsceneDefinition, Action callback)
        {
            m_cutsceneIsLoading = false;
            m_loadingCutscene = null;
            if (m_currentCutscene is Full3DCutscene full3D)
            {
                m_virtualCameraContainer = full3D.CameraContainer;
                m_virtualCameraContainer.SetParent(m_levelManager.ManagedGameObjectParent, true);
                if (cutsceneDefinition.IslandCullingTracksCamera)
                    m_cameraTrackingCoroutine = CoroutineUtils.RunCoroutine(
                        ProcessManager.GetSystem<GameplayIslandManager>(null, true).IslandCullingTracksCamera());
            }
            else if (cutsceneDefinition.RequiresMaxResolutionScaling || cutsceneDefinition.DisableGameplayCameraForGood)
            {
                m_visualQualityManagerRef.Get().EnableMenuRenderScale();
                m_cameraManagerRef.Get().ToggleGameplayCameraActive(false);
            }
            else if (!string.IsNullOrEmpty(cutsceneDefinition.NonGameplaySceneName) && m_cameraManagerRef.IsValid())
            {
                m_cameraManagerRef.Get().ToggleGameplayCameraActive(false);
            }
            GUICameraManager.Instance.ToggleCameras(false);
            m_currentCutscene.Definition = cutsceneDefinition;
            m_currentCutscene.TimelineDirector.Play();
            m_currentCutscene.TimelineDirector.time = cutsceneDefinition.StartTime;
            m_currentCutscene.TimelineDirector.stopped += OnCutsceneStopped;
            if (callback != null)
                m_currentCutscene.InvokeOnStopped = callback;
            PauseTimeCategories(cutsceneDefinition);
            PlayCutsceneAudio(cutsceneDefinition);
            ClearFullscreenEffects();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            m_previousGameplayControl = m_app.Storage.GetValue<bool>(AppFSMKeys.GameplayControlActive, false, true);
            m_app.SetHasGameplayControl(true, false);
            if (cutsceneDefinition.CharacterToSwitchTo != CharacterId.None)
            {
                Character currentCharacter = m_characterManagerRef.Get().GetCurrentCharacterUnsafe();
                if (m_characterManagerRef.Get().CharacterValid && cutsceneDefinition.CharacterInvulnerable)
                    m_invulnerableOverride = currentCharacter.AddModifierOverride<bool>((int)GameplayModifierType.Invulnerable, true);
                if (m_characterManagerRef.Get().CharacterValid && currentCharacter.IdType == cutsceneDefinition.CharacterToSwitchTo)
                {
                    m_currentCutscene.IsReadyToSkip = true;
                    return;
                }
                if (m_currentCutscene is Full3DCutscene)
                {
                    m_characterManager.RegisterOnCharacterChange(OnCharacterChanged, false);
                    CoroutineUtils.OnNextFrame(() => m_characterManager.SetCharacter(cutsceneDefinition.CharacterToSwitchTo, true));
                }
                else
                {
                    m_app.Storage.SetValue(AppFSMKeys.CurrentCharacterId, cutsceneDefinition.CharacterToSwitchTo);
                    m_app.Storage.SetValue(AppFSMKeys.MissionCharacterId, cutsceneDefinition.CharacterToSwitchTo);
                }
            }
            if ((object)cutsceneDefinition.TriggerRequirements.LevelDefinition != null &&
                cutsceneDefinition.TriggerRequirements.TriggerType == CutsceneTriggerType.OnLevelSelected &&
                cutsceneDefinition.TriggerRequirements.LevelDefinition.IsBossLevel)
                m_app.Storage.SetValue(AppFSMKeys.FirstTimeBossEntry, true);
            m_currentCutscene.IsReadyToSkip = true;
            OnCutsceneStarted();
        }

        private void PlayCutsceneAudio(CutsceneDefinition cutsceneDefinition)
        {
            AudioManager audioManager = ProcessManager.GetSystem<AudioManager>(null, true);
            if (cutsceneDefinition.DuckMusicVolume)
            {
                HLAudioMixerDefinition mainMixer = audioManager.GetMixer(HLAudioMixerIdentifier.Main);
                float startVolume = m_saveManager.GetSaveDataSettings().MusicVolume;
                float targetVolume = startVolume * cutsceneDefinition.DuckMusicVolumeFractionReduction;
                CoroutineUtils.StopUtilCoroutine(ref m_duckMusicCoroutine);
                m_duckMusicCoroutine = CoroutineUtils.RunCoroutine(FadeMusicVolume(startVolume, targetVolume,
                    cutsceneDefinition.MusicVolumeTransitionTime, mainMixer));
            }
            if (cutsceneDefinition.BackgroundMusicBehaviour == CutsceneDefinition.MusicBehaviour.KeepCurrentMusic)
                return;
            HLAudioSourceIdentifier source = HLAudioSourceIdentifier.UiBackground;
            HLAudioClipIdentifier previous = HLAudioClipIdentifier.None;
            if (audioManager.TryGetAudioSourceData(HLAudioSourceIdentifier.Background, out HLAudioSourceData backgroundSource))
            {
                previous = backgroundSource.CurrentAudio;
                if (previous != HLAudioClipIdentifier.None)
                    source = HLAudioSourceIdentifier.Background;
            }
            if (audioManager.TryGetAudioSourceData(HLAudioSourceIdentifier.UiBackground, out HLAudioSourceData uiSource))
                previous = uiSource.CurrentAudio;
            if (cutsceneDefinition.BackgroundMusicBehaviour == CutsceneDefinition.MusicBehaviour.RestoreCurrentMusicOnComplete)
                m_previousMusicToRestore = previous;
            audioManager.PlayClipAtSource(cutsceneDefinition.BackgroundMusic, source, false, null);
        }

        // 060018ee and genuine <FadeMusicVolume>d__66. The inclusive loop and
        // increment-first behavior intentionally retain zero/NaN duration cases.
        private IEnumerator FadeMusicVolume(float startVolume, float targetVolume, float transitionTime, HLAudioMixerDefinition mainMixer)
        {
            float timer = 0f;
            while (timer <= transitionTime)
            {
                timer += Time.unscaledDeltaTime;
                mainMixer.SetMusicVolume(Mathf.Lerp(startVolume, targetVolume, timer / transitionTime));
                yield return null;
            }
            mainMixer.SetMusicVolume(targetVolume);
        }

        public bool TryGetCurrentComicCutscene(out ComicCutscene comicCutscene)
        {
            comicCutscene = null;
            if (m_currentCutscene == null)
                return false;
            comicCutscene = m_currentCutscene as ComicCutscene;
            return comicCutscene != null;
        }

        public bool TryGetCurrentCutscene(out Cutscene cutscene)
        {
            cutscene = m_currentCutscene;
            return cutscene != null;
        }

        private void OnCharacterChanged(Character _) => m_currentCutscene.IsReadyToSkip = true;
        private void OnCharacterChanged() => m_currentCutscene.IsReadyToSkip = true;

        private void ClearFullscreenEffects()
        {
            if (!m_fullscreenShaderManager.IsNull())
                m_fullscreenShaderManager.Get().StopAllEffectOverrides();
        }

        private void PauseTimeCategories(CutsceneDefinition cutsceneDefinition)
        {
            if (cutsceneDefinition.TimeCategoriesToPause.Count == 0)
                return;
            TimeSetting setting = TimeSetting.GetDefault(1f);
            foreach (TimeCategory category in cutsceneDefinition.TimeCategoriesToPause)
                if (category != TimeCategory.UnityGlobal)
                    setting.SetCategory(category, 0f);
            m_pauseTimeOverride = ProcessManager.GetSystem<TimeManager>(null, true).ApplyTimeSetting(setting);
        }

        private void OnCutsceneStopped(PlayableDirector _)
        {
            if (m_currentCutscene.Definition.PlayTransitionOnStopped)
            {
                m_app.AddWaitTransitionHandle();
                m_uiScreenTransitionManager.QueueTransition(CleanupCutscene, null);
            }
            else
                CleanupCutscene();
        }

        // 060018f6. Callbacks can mutate these fields. Preserve fresh reads,
        // partial cleanup on fault, and Finished-before-clearing-current order.
        private void CleanupCutscene()
        {
            OnCutsceneFinishing();
            if (m_currentCutscene is Full3DCutscene full3D)
            {
                CoroutineUtils.StopUtilCoroutine(ref m_cameraTrackingCoroutine);
                foreach (GameObject actor in full3D.Actors)
                {
                    actor.transform.SetParent(m_levelManager.ManagedGameObjectParent, true);
                    CoroutineUtils.OnNextFrame(() => Object.Destroy(actor));
                }
            }
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            m_app.SetHasGameplayControl(m_previousGameplayControl, false);
            bool inTheatre = m_app.Storage.HasValue(AppFSMKeys.CutsceneTheatre);
            if (m_currentCutscene.Definition.TriggerNextCutscene != null && !inTheatre)
                QueueCutscene(m_currentCutscene.Definition.TriggerNextCutscene);
            m_currentCutscene.TimelineDirector.stopped -= OnCutsceneStopped;
            DisableCutsceneSkip();
            ResumeTimeCategories(m_currentCutscene.Definition);
            Object.Destroy(m_currentCutscene.gameObject);
            if (m_virtualCameraContainer != null)
                m_virtualCameraContainer.gameObject.SetActive(false);
            m_currentCutscene.InvokeOnStopped?.Invoke();
            if (m_characterManagerRef.IsValid())
            {
                m_characterManager.ReleaseOnCharacterChange(OnCharacterChanged);
                if (m_characterManager.TryGetCurrentCharacter(out Character character))
                {
                    character.EndImpulses();
                    if (m_currentCutscene.Definition.CharacterInvulnerable && m_invulnerableOverride != null)
                        character.RemoveModifierOverrides(m_invulnerableOverride);
                }
            }
            HandleCutsceneRepetition();
            AnalyticsEventCollector.CutsceneWatchedEvent(m_currentCutscene);
            bool lastCutsceneInChain = m_currentCutscene.Definition.TriggerNextCutscene == null;
            CleanupAudio(lastCutsceneInChain);
            if (m_currentCutscene.Definition.RequiresMaxResolutionScaling)
            {
                m_visualQualityManagerRef.Get().EnableGameplayRenderScale();
                m_cameraManagerRef.Get().ToggleGameplayCameraActive(true);
            }
            switch (m_currentCutscene.Definition.TriggerRequirements.TriggerType)
            {
                case CutsceneTriggerType.OnTriggerVolumeEntered:
                case CutsceneTriggerType.OnBossPhaseChanged:
                case CutsceneTriggerType.OnFirstTimeUserExperience:
                case CutsceneTriggerType.OnLevelEntered:
                    break;
                default:
                    if (lastCutsceneInChain && !m_currentCutscene.Definition.ExplicitlyLeadsIntoGameplay)
                        GUICameraManager.Instance.ToggleCameras(true);
                    break;
            }
            OnCutsceneFinished();
            m_currentCutscene = null;
            ProcessManager.GetSystem<AddressableManager>(null, true).ReleaseHandle(m_currentCutsceneHandle);
        }

        private void CleanupAudio(bool lastCutsceneInChain)
        {
            AudioManager audioManager = ProcessManager.GetSystem<AudioManager>(null, true);
            SaveDataSettings settings = m_saveManager.GetSaveDataSettings();
            HLAudioMixerDefinition mainMixer = audioManager.GetMixer(HLAudioMixerIdentifier.Main);
            if (m_previousMusicToRestore != HLAudioClipIdentifier.None && lastCutsceneInChain)
            {
                audioManager.PlayClipAtSource(m_previousMusicToRestore, HLAudioSourceIdentifier.UiBackground, false, null);
                m_previousMusicToRestore = HLAudioClipIdentifier.None;
            }
            if (m_currentCutscene.Definition.DuckMusicVolume)
            {
                float targetVolume = settings.MusicVolume;
                CoroutineUtils.StopUtilCoroutine(ref m_duckMusicCoroutine);
                m_duckMusicCoroutine = CoroutineUtils.RunCoroutine(FadeMusicVolume(mainMixer.GetMusicVolume(), targetVolume,
                    m_currentCutscene.Definition.MusicVolumeTransitionTime, mainMixer));
            }
        }

        private void HandleCutsceneRepetition()
        {
            string guid = m_currentCutscene.Definition.GetGUID();
            m_saveManager.CurrentSave.AddSeenCutsceneGuid(guid);
            m_saveManager.RequestSave();
            m_cutscenesSeenThisSession.AddUnique(guid);
        }

        private void ResumeTimeCategories(CutsceneDefinition cutsceneDefinition)
        {
            if (cutsceneDefinition.TimeCategoriesToPause.Count != 0 && m_pauseTimeOverride != null)
                ProcessManager.GetSystem<TimeManager>(null, true).RemoveTimeSetting(m_pauseTimeOverride);
        }

        public void EnableCutsceneSkip()
        {
            if (!m_currentCutscene.Definition.CanBeSkipped)
                return;
            if (m_skipCoroutine != null)
                CoroutineUtils.StopUtilCoroutine(ref m_skipCoroutine);
            m_skipCoroutine = CoroutineUtils.RunCoroutine(EnableSkipWhenReady());
        }

        private IEnumerator EnableSkipWhenReady()
        {
            yield return new WaitUntil(() => m_currentCutscene.IsReadyToSkip);
            if (!m_uiManager.IsOpen(m_currentCutscene.Definition.SkipUIContainer))
                m_uiManager.GetOrCreate(m_currentCutscene.Definition.SkipUIContainer);
        }

        public void DisableCutsceneSkip()
        {
            if (m_skipCoroutine != null)
                CoroutineUtils.StopUtilCoroutine(ref m_skipCoroutine);
            if (m_uiManager.IsOpen(m_currentCutscene.Definition.SkipUIContainer))
                m_uiManager.Close(m_currentCutscene.Definition.SkipUIContainer);
        }

        public void SkipCutscene(float _ = 0f) => m_currentCutscene.TimelineDirector.Stop();

        private void CleanupAnyPreviousCameras()
        {
            if (m_virtualCameraContainer != null)
                Object.Destroy(m_virtualCameraContainer.gameObject);
        }

        public bool ConsumeSkipTapEvent()
        {
            bool occurred = m_skipTapOccurred;
            m_skipTapOccurred = false;
            if (m_currentCutscene == null)
                return false;
            m_currentCutscene.WasSkipped = occurred;
            return occurred;
        }

        public bool ConsumeSkipHoldEvent()
        {
            bool occurred = m_skipHoldOccurred;
            m_skipHoldOccurred = false;
            if (m_currentCutscene == null)
                return false;
            m_currentCutscene.WasSkipped = occurred;
            return occurred;
        }

        private void Shutdown(object context = null)
        {
            if (m_characterManager != null)
                m_characterManager.ReleaseOnCharacterChange(OnCharacterChanged);
            ProcessManager.UnregisterSystem(this);
        }

        public void QueueCutscene(CutsceneDefinition cutscene)
        {
            if (m_app.Storage.GetValue<bool>(AppFSMKeys.CutsceneSkipEndGame, false, true))
                return;
            m_app.Storage.SetValue(AppFSMKeys.CutsceneQueued, cutscene);
            if (!string.IsNullOrEmpty(cutscene.NonGameplaySceneName))
                m_app.Storage.SetValue(AppFSMKeys.SceneLoadQueued, cutscene.NonGameplaySceneName);
        }
    }
}
