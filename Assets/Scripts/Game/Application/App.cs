using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using HLAnalytics = Hardlight.Analytics.Analytics;
using Object = UnityEngine.Object;

namespace HardlightProject
{
    // Original Game.Runtime 0x020000a8. Preserve original services and their
    // registration, callback, storage and exception order. The compiler generates
    // the two empty callbacks and the single-yield FSM acquisition iterator.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class App : MonoBehaviour, IGraphUser, ISystem
    {
        // Original fields 0x04000337..0x04000343, in metadata order.
        public IGraphStorage Storage { get; } = new FSMStorage(0);
        // Original 0x06000556..0x06000558: getters retain auto-register defaults.
        public SplashScreen SplashScreen => ProcessManager.GetSystemSafe<SplashScreen>();
        public DataManager DataManager => ProcessManager.GetSystemSafe<DataManager>();
        public GameCenter GameCenter => ProcessManager.GetSystemSafe<GameCenter>();

        public event Action<bool> OnGameplayControlActive = _ => { };
        public Action<bool> OnForceReleaseCursor = _ => { };
        public bool IsReady { get; private set; }

        [Tooltip("The FSM the user will use when 'Update' is called")]
        [SerializeField] private FiniteStateMachineScriptableObject m_applicationFSMObject;
        private FiniteStateMachine m_applicationFSM;
        [SerializeField]
        [Tooltip("Object pool size for tracking FSMStateChangeActions - increase if seeing allocation warnings")]
        private int m_fsmStateChangePoolSizeDevelopment = 200;
        [SerializeField] private int m_fsmStateChangePoolSizeProduction = 60;
        [Tooltip("Instantiate log handler if enabled in game configuration.")]
        [SerializeField] private GameObject m_logHandlerPrefab;
        private FSMStateLoader m_fsmStateLoader;
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();
        private readonly SystemRef<UIScreenTransitionManager> m_screenTransitionManagerRef = ProcessManager.GetSystemRef<UIScreenTransitionManager>();
        private GameObject m_logHandler;

        // Original 0x0600055d. Original property-store Int32 client version is 0.
        protected void Awake()
        {
            ProcessManager.RegisterSystem(this);
            ProcessManager.RegisterSystem(new HLPropertyStore("H4RDl1ght_S4VE", 0, "Primary"));
            m_saveManagerRef.InvokeOnValid(OnSaveManagerValid);
            if (SystemConfiguration.GetConfig<CoreGameConfiguration>().LogHandlerEnabled && m_logHandlerPrefab != null)
                m_logHandler = Object.Instantiate(m_logHandlerPrefab, transform);
            // Shipping ARM/x86 use the production field. The development field
            // remains serialized but no development-selection branch is present.
            FSMStateChangeAction.InitialObjectPoolSize = m_fsmStateChangePoolSizeProduction;
        }

        // Original 0x0600055e is an immediate return on both architectures.
        public void DestroyUser() { }

        // Original 0x0600055f: analytics precedes asynchronous FSM loading.
        protected void Start()
        {
            InitialiseAnalytics();
            PreloadApplicationFSMs();
        }

        // Original 0x06000560: system Update occurs only while the FSM exists.
        private void Update()
        {
            // Preserve the original single FSM receiver through the time/context callbacks.
            FiniteStateMachine applicationFSM = m_applicationFSM;
            if (applicationFSM != null)
            {
                applicationFSM.Update(this, new FSMUpdateContext(Time.deltaTime, FSMUpdateType.Update));
                ProcessManager.ProcessSystemAction(SystemAction.Update);
            }
        }

        // Original 0x06000561. Release the live loaded list, clear this graph user,
        // unsubscribe save callbacks, then Shutdown/AppShutdown all systems.
        private void OnDestroy()
        {
            if (m_logHandler != null)
            {
                Object.Destroy(m_logHandler);
                m_logHandler = null;
            }
            List<FiniteStateMachineScriptableObject> loadedStateMachines = m_fsmStateLoader?.LoadedStateMachines;
            if (loadedStateMachines != null)
                foreach (FiniteStateMachineScriptableObject stateMachine in loadedStateMachines)
                    stateMachine.ReleaseFSM();
            m_applicationFSM?.ClearUser(this);
            if (m_saveManagerRef.TryGet(out SaveManager saveManager))
            {
                saveManager.OnCloudSaveResolved -= OnCloudSaveResolved;
                saveManager.OnNewAppVersionRequired -= OnNewAppVersionRequired;
            }
            ProcessManager.ProcessSystemAction(SystemAction.Shutdown);
            ProcessManager.ProcessSystemAction(SystemAction.AppShutdown);
        }

        // Original 0x06000562: preserve this lifecycle order.
        public void InitialiseProcessManagerSystems()
        {
            ProcessManager.ProcessSystemAction(SystemAction.AppInitialise);
            ProcessManager.ProcessSystemAction(SystemAction.Initialise);
        }

        // Original 0x06000563: check without auto-registration, then register,
        // re-fetch the actual system and send its two lifecycle actions.
        public void RegisterAndInitialiseSystem<TSystem>() where TSystem : class, ISystem, new()
        {
            if (ProcessManager.GetSystemSafe<TSystem>(null, false) != null) return;
            ProcessManager.RegisterSystem<TSystem>();
            TSystem system = ProcessManager.GetSystem<TSystem>();
            ProcessManager.ProcessSystemAction(system, SystemAction.AppInitialise);
            ProcessManager.ProcessSystemAction(system, SystemAction.Initialise);
        }

        // Original 0x06000564. Existing registration suppresses the supplied
        // instance entirely; otherwise actions target the supplied instance.
        public void RegisterAndInitialiseSystem<TSystem>(TSystem system) where TSystem : class, ISystem
        {
            if (ProcessManager.GetSystemSafe<TSystem>(null, false) != null) return;
            ProcessManager.RegisterSystem(system);
            ProcessManager.ProcessSystemAction(system, SystemAction.AppInitialise);
            ProcessManager.ProcessSystemAction(system, SystemAction.Initialise);
        }

        // Original 0x06000565.
        public void ShutdownAndUnregisterSystem<TSystem>() where TSystem : class, ISystem
        {
            TSystem system = ProcessManager.GetSystemSafe<TSystem>(null, false);
            if (system != null)
            {
                ProcessManager.ProcessSystemAction(system, SystemAction.Shutdown);
                ProcessManager.ProcessSystemAction(system, SystemAction.AppShutdown);
                ProcessManager.UnregisterSystem(system);
            }
        }

        // Original 0x06000566: no null or registration check for this overload.
        public void ShutdownAndUnregisterSystem<TSystem>(TSystem system) where TSystem : class, ISystem
        {
            ProcessManager.ProcessSystemAction(system, SystemAction.Shutdown);
            ProcessManager.ProcessSystemAction(system, SystemAction.AppShutdown);
            ProcessManager.UnregisterSystem(system);
        }

        // Original 0x06000567. Conditional attribute is original metadata.
        [Conditional("BUILD_DEVELOPMENT")]
        private void AddDebugButtons()
        {
            DebugUnlockLevels.AddButton();
            CharacterReplay.AddDebugButton();
            VisualQualityManager_SDT.AddDebugButton();
        }

        // Original 0x06000568 and callback 0x06000585. Keep the unused App
        // lookup and live ApplicationStateMachines.Values enumerable.
        private void PreloadApplicationFSMs()
        {
            ProcessManager.GetSystem<App>();
            m_fsmStateLoader = new FSMStateLoader(DataManager.ApplicationStateMachines.Values,
                () => CoroutineUtils.RunCoroutine(OnAllFSMsLoaded()));
            m_fsmStateLoader.LoadStates();
        }

        // Original wrapper 0x06000569, iterator 0x0600058b..0x06000590 and
        // callback 0x06000586. A single acquisition yield precedes all storage
        // initialization; the subscription handle is deliberately discarded.
        private IEnumerator OnAllFSMsLoaded()
        {
            yield return m_applicationFSMObject.AcquireFSM();
            m_applicationFSM = m_applicationFSMObject.FSM;
            m_applicationFSM.InitialiseUser(this);
            Storage.SetValue(AppFSMKeys.StateEvents, new List<ApplicationStateEvent>());
            Storage.SetValue(AppFSMKeys.UIModernEvents, new List<UIModernEvent>());
            Storage.SetValue(AppFSMKeys.LevelUnloadOperations, new List<AsyncOperation>());
            Storage.SetValue(AppFSMKeys.MissionIntroList, new List<MissionState>());
            Storage.SetValue(AppFSMKeys.MetaGameUnlocks, new List<IMetaGameUnlock>());
            Storage.SetValue(AppFSMKeys.LoadedLevels, new Dictionary<string, AsyncOperationHandle>());
            ReplayMode replayMode = SystemConfiguration.GetConfig<CoreGameConfiguration>().ReplayMode;
            Storage.SetValue(AppFSMKeys.ReplayMode, replayMode);
            ProcessManager.GetSystemAutoCreate<MessageExchangeBoundCallbackArg<UIModernMessage>>()
                .SubscribeToMessage<UIModernEvent>(new UIModernMessage(UIModernEventType.OnClick),
                    (in UIModernEvent eventId) =>
                    {
                        // Original 0x06000586 snapshots the aliased event before graph-storage callbacks.
                        UIModernEvent capturedEvent = eventId;
                        GetUIModernEvents().AddUnique(capturedEvent);
                    });
            IsReady = true;
        }

        // Original 0x0600056a/6b: these reads do not insert a default.
        public bool IsMetaGameActive() => Storage.GetValue(AppFSMKeys.MetaGameActive, false, false);
        public bool IsGameplayActive() => Storage.GetValue(AppFSMKeys.GameplayActive, false, false);

        // Original 0x0600056c: dereferences preserve original failure behavior.
        public bool IsStateActive(FSMIdentifier stateId) => m_applicationFSM.GetActiveState(Storage).StateId == stateId.Id;

        // Original 0x0600056d/6e: nullable stored list is passed through.
        public List<UIModernEvent> GetUIModernEvents() => Storage.GetValue<List<UIModernEvent>>(AppFSMKeys.UIModernEvents, null, true);
        public void AddUniqueUIModernEvent(UIModernEvent uiModernEvent) => GetUIModernEvents().AddUnique(uiModernEvent);

        // Original 0x0600056f/70: the name lists are constructed and populated
        // even though shipping code does not subsequently use or clear them.
        [Conditional("BUILD_DEVELOPMENT")]
        private void CheckLeftoverUIModernEvents()
        {
            List<UIModernEvent> events = GetUIModernEvents();
            if (events != null)
            {
                List<string> names = new List<string>();
                foreach (UIModernEvent eventId in events) names.Add(eventId.name);
            }
        }

        [Conditional("BUILD_DEVELOPMENT")]
        private void CheckLeftoverStateEvents()
        {
            List<ApplicationStateEvent> events = Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true);
            if (events != null)
            {
                List<string> names = new List<string>();
                foreach (ApplicationStateEvent eventId in events) names.Add(eventId.name);
            }
        }

        // Original 0x06000571: register session, consent, initialize analytics,
        // then activate session management. The platform services are retained.
        private void InitialiseAnalytics()
        {
#if PROJECT_LUCID_ORIGINAL_GAME_TELEMETRY
            RegisterAndInitialiseSystem<AnalyticsSessionManager>();
            RegisterAndInitialiseSystem<AnalyticsConsentManager>();
            HLAnalytics.Initialise(new AnalyticsSettings());
            ProcessManager.GetSystem<AnalyticsSessionManager>().ActivateAnalyticsSessionManagement();
        #else
            ProjectLucid.Offline.OfflineTelemetryStartup.Initialise();
#endif
        }

        // Original 0x06000572. Preserve pause/session/badge/save call order.
        private void OnApplicationFocus(bool focused)
        {
#if PROJECT_LUCID_ORIGINAL_GAME_TELEMETRY
            AnalyticsSessionManager sessionManager = ProcessManager.GetSystemSafe<AnalyticsSessionManager>();
            if (focused)
            {
                HLAnalytics.ApplicationPause(false);
                sessionManager?.ApplicationFocused(true, false);
                HLNotifications.ClearNotificationBadge();
            }
            else
            {
                sessionManager?.ApplicationFocused(false, false);
                HLAnalytics.ApplicationPause(true);
                if (m_saveManagerRef.TryGet(out SaveManager saveManager)) saveManager.OnApplicationFocus();
            }
        #else
            ProjectLucid.Offline.OfflineTelemetryStartup.ApplicationFocus(focused);
            if (!focused && m_saveManagerRef.TryGet(out SaveManager saveManager)) saveManager.OnApplicationFocus();
#endif
        }

        // Original 0x06000573: session shutdown precedes consent shutdown.
        private void OnApplicationQuit()
        {
#if PROJECT_LUCID_ORIGINAL_GAME_TELEMETRY
            ShutdownAndUnregisterSystem<AnalyticsSessionManager>();
            ShutdownAndUnregisterSystem<AnalyticsConsentManager>();
            HLAnalytics.Shutdown();
        #else
            ProjectLucid.Offline.OfflineTelemetryStartup.Shutdown();
#endif
        }

        // Original 0x06000574. Exclusive-character element zero precedes level
        // parameter construction and all three writes. Empty uses original Sonic.
        public void FastLoad(GameplayLevelDefinition levelDefinition)
        {
            // Count and element zero use the same original exclusive-character list capture.
            List<CharacterDefinition> exclusiveCharacters = levelDefinition.ExclusiveCharacters;
            CharacterId characterId = exclusiveCharacters.Count > 0
                ? exclusiveCharacters[0].Id : CharacterId.Sonic;
            Storage.SetValue(AppFSMKeys.LevelLoadParameters, new LevelManagerLoadLevelParameters(levelDefinition));
            Storage.SetValue(AppFSMKeys.CurrentCharacterId, characterId);
            Storage.SetValue(AppFSMKeys.IsFastLoading, true);
        }

        // Original 0x06000575: skip storage when forced; fallback is not cached.
        public string GetLastLevelUIVisitedGUID(bool forceUseSavedValue = false)
        {
            if (!forceUseSavedValue && Storage.TryGetValue(AppFSMKeys.LastLevelUIVisitedGUID, out string value)
                && !string.IsNullOrEmpty(value)) return value;
            if (m_saveManagerRef.IsValid())
            {
                SaveManager saveManager = m_saveManagerRef.Get();
                return saveManager.IsAnySaveOpen ? saveManager.CurrentSave.LastLevelVisitedGUID : null;
            }
            return null;
        }

        // Original 0x06000576: storage precedes the optional current-save write.
        public void SetLastLevelUIVisitedGUID(GameplayLevelDefinition gameplayLevel, bool updateInSave)
        {
            Storage.SetValue(AppFSMKeys.LastLevelUIVisitedGUID, gameplayLevel.GetGUID());
            if (updateInSave)
            {
                SaveManager saveManager = m_saveManagerRef.Get();
                saveManager.CurrentSave.SetLastLevelVisited(gameplayLevel);
                saveManager.RequestSave();
            }
        }

        // Original 0x06000577/78. Null removal uses the original typed key API
        // and ScriptableObjectWithGuid inequality, including its handle semantics.
        public string GetLastLevelLoadedGUID() => Storage.GetValue<string>(AppFSMKeys.LastLevelLoadedGUID, null, true);
        public void SetLastLevelLoadedGUID(GameplayLevelDefinition sceneDefinition)
        {
            if (sceneDefinition != null) Storage.SetValue(AppFSMKeys.LastLevelLoadedGUID, sceneDefinition.GetGUID());
            else Storage.RemoveValue<string>(AppFSMKeys.LastLevelLoadedGUID);
        }

        // Original 0x06000579/7a: reads without inserting defaults.
        public bool RestartInProgress() => Storage.GetValueOnly(AppFSMKeys.RestartInProgress, false);
        public bool HasGameplayControl() => Storage.GetValueOnly(AppFSMKeys.GameplayControlActive, false);

        // Original 0x0600057b: short-circuit the metagame lookup; always write
        // and invoke, including repeated values. No callback null check is present.
        public void SetHasGameplayControl(bool on, bool skipMetaGameCheck = false)
        {
            bool gameplayControlActive = on && (skipMetaGameCheck || !IsMetaGameActive());
            Storage.SetValue(AppFSMKeys.GameplayControlActive, gameplayControlActive);
            OnGameplayControlActive(gameplayControlActive);
        }

        // Original 0x0600057c/7d: selected uses the distinct original save fields.
        public string GetLastLevelUISelectedGUID(bool forceUseSavedValue = false)
        {
            if (!forceUseSavedValue && Storage.TryGetValue(AppFSMKeys.LastLevelUISelectedGUID, out string value)
                && !string.IsNullOrEmpty(value)) return value;
            if (m_saveManagerRef.IsValid())
            {
                SaveManager saveManager = m_saveManagerRef.Get();
                return saveManager.IsAnySaveOpen ? saveManager.CurrentSave.LastLevelSelectedGUID : null;
            }
            return null;
        }

        public void SetLastLevelUISelectedGUID(GameplayLevelDefinition gameplayLevel, bool updateInSave)
        {
            Storage.SetValue(AppFSMKeys.LastLevelUISelectedGUID, gameplayLevel.GetGUID());
            if (updateInSave)
            {
                SaveManager saveManager = m_saveManagerRef.Get();
                saveManager.CurrentSave.SetLastLevelSelected(gameplayLevel);
                saveManager.RequestSave();
            }
        }

        // Original 0x0600057e. Persist an empty capacity-one list before looking
        // up the transition manager, preserving the original partial-failure state.
        public void AddWaitTransitionHandle()
        {
            List<StackableDataHandle> handles = Storage.GetValue<List<StackableDataHandle>>(AppFSMKeys.TransitionMidpointWaitHandles, null, false);
            if (handles == null)
            {
                handles = new List<StackableDataHandle>(1);
                Storage.SetValue(AppFSMKeys.TransitionMidpointWaitHandles, handles);
            }
            handles.Add(m_screenTransitionManagerRef.Get().AddWaitForMidpointOverride());
        }

        // Original 0x0600057f: release the live list in order and clear only
        // after all releases succeed. Enumerator disposal survives exceptions.
        public void TryReleaseUIScreenTransition()
        {
            if (Storage.TryGetValue(AppFSMKeys.TransitionMidpointWaitHandles, out List<StackableDataHandle> handles)
                && handles != null && handles.Count > 0)
            {
                UIScreenTransitionManager screenTransitionManager = m_screenTransitionManagerRef.Get();
                foreach (StackableDataHandle handle in handles) screenTransitionManager.ReleaseWaitForMidpointHandle(handle);
                handles.Clear();
            }
        }

        // Original 0x06000580: the Apple provider excludes Siri Remote; every
        // other provider succeeds. Original Apple controller calls are preserved.
        public bool ValidateHasAtLeastOneController(HLInputModule hlInputModule)
        {
            if (hlInputModule.BaseControllerProvider is ApplePluginControllerProvider controllerProvider)
            {
                foreach (GCController controller in controllerProvider.GetControllers())
                    if (controller.Handle.GetControllerType() != GCControllerType.SiriRemote) return true;
                return false;
            }
            return true;
        }

        // Original 0x06000581: append cloud then version callbacks, in order.
        private void OnSaveManagerValid(SaveManager saveManager)
        {
            saveManager.OnCloudSaveResolved += OnCloudSaveResolved;
            saveManager.OnNewAppVersionRequired += OnNewAppVersionRequired;
        }

        // Original 0x06000582 uses storeDefault=true, unlike IsMetaGameActive.
        private void OnCloudSaveResolved()
        {
            if (Storage.GetValue(AppFSMKeys.MetaGameActive, false, true))
                Storage.SetValue(AppFSMKeys.CloudSaveChanged, true);
        }

        // Original 0x06000583.
        private void OnNewAppVersionRequired() => Storage.SetValue(AppFSMKeys.NewAppVersionRequired, true);
        // Original 0x06000584 constructor is represented by ordered initializers
        // above; its two empty lambda bodies are original 0x06000589/5a.
    }
}
