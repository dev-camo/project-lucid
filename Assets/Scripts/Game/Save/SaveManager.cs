using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020007d5, including every original owner method.
    // The six iterators, four empty constructor callbacks, and deferred conflict
    // callback are expressed naturally. Their emitted shape remains unverified
    // until the genuine whole provider graph compiles. This is private research.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveManager : ISystem
    {
        public const int MaxSaves = 3;
        private const float RequestedSaveDelaySeconds = 0.5f;
        private const int IndexNoSaveOpen = -1;
        private const string PropertyKeySaveAppVersion = "SaveAppVersion";
        private const string PropertyKeyLastSaveIndex = "GameSave_LastSaveIndex";
        private const string PropertyKeyLastSavedTimestamp = "GameSave_LastSavedTimestamp";
        private const string PropertyKeySaveSettings = "GameSave_Settings";
        private const string PropertyKeySaveAnalytics = "GameSave_Analytics";
        private const string PropertyKeySaveNotifications = "GameSave_Notifications";
        private const string DebugMenuRoot = "Save Manager";
        private readonly string m_debugMenuCloud = "Save Manager/Cloud";
        private readonly string m_debugMenuGameSettings = "Save Manager/Game Settings";
        private readonly string m_debugMenuTimeTrials = "Save Manager/Time Trials";
        private const bool PreventSaveDowngrade = false;

        // 0x06002d0d/0e, 0f/10, 12/13, 14/15, 16/17.
        public int CurrentSaveIndex { get; private set; } = IndexNoSaveOpen;
        public DateTime LastSavedAtTime { get; private set; }
        public string[] RawSaves => m_rawSaves;
        public bool SystemReady { get; private set; }
        public bool GameIsPlaying { get; set; }
        public Action OnLoadCompleted = () => { };
        public Action OnSaveCompleted = () => { };
        public Action OnCloudSaveResolved = () => { };
        public Action OnNewAppVersionRequired = () => { };
        public bool NewAppVersionRequired { get; private set; }

        private readonly string[] m_rawSaves = new string[MaxSaves];
        private readonly string[] m_savePropertyNames = new string[MaxSaves];
        private int m_lastSaveIndex = IndexNoSaveOpen;
        private SaveDataGame m_currentSave;
        private string m_rawSaveSettings;
        private SaveDataSettings m_saveSettings;
        private string m_rawSaveAnalytics;
        private SaveDataAnalytics m_saveAnalytics;
        private string m_rawSaveNotifications;
        private SaveDataNotifications m_saveDataNotifications;
        private bool m_saveRequested;
        private readonly WaitForSeconds m_saveRequestDelay = new WaitForSeconds(RequestedSaveDelaySeconds);
        private Coroutine m_saveCoroutine;
        private readonly List<ISaveGameListener> m_saveGameListeners = new List<ISaveGameListener>();
        private readonly List<ISaveGameListener> m_dependantSaveGameListeners = new List<ISaveGameListener>();
        private HLPropertyStore m_hlPropertyStore;
        private HLCloud.Cloud m_cloud;
        private string m_chosenSaveIdentifierToLoad;
        private bool m_cloudReady;
        private bool m_hlPropertyStoreReady;
        private bool m_loadCompleted;
        private bool m_hasUnresolvedConflicts;
        private Coroutine m_cloudSyncCoroutine;
        private readonly HashSet<string> m_saveVersionsSeen = new HashSet<string>();

        // 0x06002d11/18/19: live references; open means the managed save exists.
        public SaveDataGame CurrentSave => m_currentSave;
        public bool IsAnySaveOpen => m_currentSave != null;

        // 0x06002d1a; empty callbacks naturally cover original 0x06002d66..6b.
        public SaveManager()
        {
            this.SubscribeToAction(SystemAction.Initialise, Initialise);
            this.SubscribeToAction(SystemAction.Shutdown, Shutdown);
            m_savePropertyNames[0] = string.Format("GameSave_{0}", 0);
            m_savePropertyNames[1] = string.Format("GameSave_{0}", 1);
            m_savePropertyNames[2] = string.Format("GameSave_{0}", 2);
        }

        // 0x06002d1b.
        private void Initialise(object context = null)
        {
            ProcessManager.GetSystemRef<HLPropertyStore>().InvokeOnValid(OnPropertyStoreValid);
            ProcessManager.GetSystemRef<HLCloud.Cloud>().InvokeOnValid(OnCloudValid);
        }

        // 0x06002d1c: handler registration precedes assignment, including replay.
        private void OnPropertyStoreValid(HLPropertyStore propertyStore)
        {
            HLPropertyStore.AddLoadHandler(OnPropertyStoreLoad, true);
            HLPropertyStore.AddSaveHandler(OnPropertyStoreSave);
            HLPropertyStore.AddResolveConflictHandler(OnPropertyStoreConflict);
            HLPropertyStore.AddAfterConflictsResolvedHandler(OnPropertyStoreAfterConflictsResolved);
            m_hlPropertyStore = propertyStore;
            m_hlPropertyStoreReady = true;
            SystemReady = m_cloudReady;
        }

        // 0x06002d1d.
        private void OnCloudValid(HLCloud.Cloud cloud)
        {
            m_cloud = cloud;
            m_cloudReady = true;
            SystemReady = m_hlPropertyStoreReady;
        }

        // 0x06002d1e: empty property names are allowed; only null is excluded.
        private void AddAllKeysToSync()
        {
            string identifier = m_hlPropertyStore.GetSaveIdentifier();
            if (string.IsNullOrWhiteSpace(identifier)) return;
            HLPropertyList properties = m_hlPropertyStore.GetPropertyListFromSave(identifier);
            AddPropertyNameOrInitialise(properties, m_savePropertyNames[0]);
            AddPropertyNameOrInitialise(properties, m_savePropertyNames[1]);
            AddPropertyNameOrInitialise(properties, m_savePropertyNames[2]);
            AddPropertyNameOrInitialise(properties, PropertyKeyLastSaveIndex);
            AddPropertyNameOrInitialise(properties, PropertyKeyLastSavedTimestamp);
            AddPropertyNameOrInitialise(properties, PropertyKeySaveAnalytics);
            AddPropertyNameOrInitialise(properties, PropertyKeySaveNotifications);
            foreach (HLPropertyList.Property property in properties.Properties)
                if (property.m_name != null) m_cloud.AddPropertyKeyToSync(property.m_name);
        }

        // 0x06002d1f: this stores existing raw strings without serializing again.
        private void OnPropertyStoreSave(HLPropertyList properties)
        {
            if (!SaveIdentifierValid()) return;
            TrySetPropertyData(properties, PropertyKeySaveAppVersion, Application.version);
            TrySetPropertyData(properties, m_savePropertyNames[0], m_rawSaves[0]);
            TrySetPropertyData(properties, m_savePropertyNames[1], m_rawSaves[1]);
            TrySetPropertyData(properties, m_savePropertyNames[2], m_rawSaves[2]);
            if (m_currentSave != null)
                TrySetPropertyData(properties, PropertyKeyLastSaveIndex, CurrentSaveIndex.ToString());
            TrySetPropertyData(properties, PropertyKeySaveSettings, m_rawSaveSettings);
            TrySetPropertyData(properties, PropertyKeySaveAnalytics, m_rawSaveAnalytics);
            TrySetPropertyData(properties, PropertyKeySaveNotifications, m_rawSaveNotifications);
        }

        // 0x06002d20.
        private static void AddPropertyNameOrInitialise(HLPropertyList propertyList, string propertyName)
        {
            if (propertyList.AssociateNameWithProperty(propertyName)) return;
            propertyList.AddProperty(propertyName, string.Empty);
        }

        // 0x06002d21: a null/whitespace incoming value preserves an existing non-whitespace one; otherwise normalize to empty.
        private void TrySetPropertyData(HLPropertyList propertyList, string propertyName, string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyValue))
            {
                HLPropertyList.Property oldProperty = propertyList.GetProperty(propertyName);
                if (oldProperty != null && !string.IsNullOrWhiteSpace(oldProperty.m_value)) return;
                propertyValue = string.Empty;
            }
            propertyList.AddProperty(propertyName, propertyValue);
        }

        // 0x06002d22: isNewFile is not read. No Initialise calls are present.
        private void OnPropertyStoreLoad(HLPropertyList properties, bool isNewFile)
        {
            if (!SaveIdentifierValid()) return;
            m_rawSaves[0] = properties.AsString(m_savePropertyNames[0], null);
            m_rawSaves[1] = properties.AsString(m_savePropertyNames[1], null);
            m_rawSaves[2] = properties.AsString(m_savePropertyNames[2], null);
            SetLastSaveIndex(properties.AsInt(PropertyKeyLastSaveIndex, IndexNoSaveOpen), false);
            LastSavedAtTime = TimeUtils.FromUnixTime(properties.AsLong(PropertyKeyLastSavedTimestamp, 0L));
            m_rawSaveSettings = properties.AsString(PropertyKeySaveSettings, null);
            m_rawSaveAnalytics = properties.AsString(PropertyKeySaveAnalytics, null);
            m_rawSaveNotifications = properties.AsString(PropertyKeySaveNotifications, null);
            if (string.IsNullOrEmpty(m_rawSaveSettings))
            {
                m_saveSettings = new SaveDataSettings();
                m_saveSettings.MarkDirty();
            }
            else m_saveSettings = JsonUtility.FromJson<SaveDataSettings>(m_rawSaveSettings);
            if (string.IsNullOrEmpty(m_rawSaveAnalytics))
            {
                m_saveAnalytics = new SaveDataAnalytics();
                m_saveAnalytics.MarkDirty();
            }
            else m_saveAnalytics = JsonUtility.FromJson<SaveDataAnalytics>(m_rawSaveAnalytics);
            if (string.IsNullOrEmpty(m_rawSaveNotifications))
            {
                m_saveDataNotifications = new SaveDataNotifications();
                m_saveDataNotifications.MarkDirty();
            }
            else m_saveDataNotifications = JsonUtility.FromJson<SaveDataNotifications>(m_rawSaveNotifications);
            m_loadCompleted = true;
            OnLoadCompleted();
        }

        // 0x06002d23: overrideData is unused; callback exceptions are not caught.
        private void OnPropertyStoreConflict(HLPropertyList cloudProperties, bool overrideData, ref bool conflictsResolved)
        {
            if (!SaveIdentifierValid()) return;
            long localTimestamp = TimeUtils.ToUnixTimeMs(LastSavedAtTime);
            long cloudTimestamp = cloudProperties.AsLong(PropertyKeyLastSavedTimestamp, 0L);
            cloudProperties.AddProperty(PropertyKeySaveSettings, m_rawSaveSettings);
            bool gameChanged = ResolveSaveDataGame(cloudProperties, cloudTimestamp > localTimestamp);
            ResolveSaveDataAnalytics(cloudProperties);
            ResolveSaveDataNotifications(cloudProperties);
            conflictsResolved = true;
            if (gameChanged) OnCloudSaveResolved();
        }

        // 0x06002d24.
        private void ResolveSaveDataAnalytics(HLPropertyList properties) =>
            ResolveSaveDataResolvableItem(m_saveAnalytics, properties, PropertyKeySaveAnalytics,
                m_rawSaveAnalytics, HasSaveDataAnalytics);

        // 0x06002d25.
        private void ResolveSaveDataNotifications(HLPropertyList properties) =>
            ResolveSaveDataResolvableItem(m_saveDataNotifications, properties, PropertyKeySaveNotifications,
                m_rawSaveNotifications, HasSaveDataNotifications);

        // 0x06002d26: both genuine generic contexts recover this single method.
        private static bool ResolveSaveDataResolvableItem<T>(SaveDataResolvableItem<T> saveDataResolvableItem,
            HLPropertyList properties, string propertyKey, string rawSaveString, Func<bool> hasSaveDataFunc)
        {
            string newRaw = properties.GetProperty(propertyKey).m_value;
            bool oldEmpty = string.IsNullOrEmpty(rawSaveString);
            bool newEmpty = string.IsNullOrEmpty(newRaw);
            if (oldEmpty && newEmpty) return false;
            if (oldEmpty && !newEmpty) return true;
            if (!oldEmpty && newEmpty)
            {
                properties.AddProperty(propertyKey, JsonUtility.ToJson(rawSaveString));
                return true;
            }
            if (newRaw == rawSaveString) return false;
            T newItem = JsonUtility.FromJson<T>(newRaw);
            if (hasSaveDataFunc()) saveDataResolvableItem.ResolveNewData(newItem);
            else saveDataResolvableItem = newItem as SaveDataResolvableItem<T>;
            properties.AddProperty(propertyKey, JsonUtility.ToJson(saveDataResolvableItem));
            return true;
        }

        // 0x06002d27: deleting the property does not set the changed flag.
        private bool ResolveSaveDataGame(HLPropertyList resolvingProperties, bool cloudIsLatest)
        {
            bool gameChanged = false;
            for (int slot = 0; slot < MaxSaves; ++slot)
            {
                string oldRaw = m_rawSaves[slot];
                string newRaw = resolvingProperties.GetProperty(m_savePropertyNames[slot]).m_value;
                bool oldEmpty = string.IsNullOrEmpty(oldRaw);
                bool newEmpty = string.IsNullOrEmpty(newRaw);
                if (oldEmpty && newEmpty) continue;
                if (oldRaw == newRaw) continue;
                SaveDataGame local;
                if (oldEmpty)
                {
                    local = new SaveDataGame();
                    local.CutsceneSeenSaveMaintenance = true;
                    local.Initialise();
                }
                else local = JsonUtility.FromJson<SaveDataGame>(oldRaw);
                if (newEmpty)
                {
                    resolvingProperties.AddProperty(m_savePropertyNames[slot], cloudIsLatest ? string.Empty : oldRaw);
                    continue;
                }
                SaveDataGame incoming = JsonUtility.FromJson<SaveDataGame>(newRaw);
                if (incoming.LastDeletionTimestamp > local.LastDeletionTimestamp)
                {
                    HLOutput.LogError(string.Format(
                        "[{0}] Cloud was deleted more recently than local save for slot {1}", "SaveManager", slot));
                    resolvingProperties.AddProperty(m_savePropertyNames[slot], JsonUtility.ToJson(incoming));
                }
                else
                {
                    local.ResolveNewData(incoming, cloudIsLatest);
                    resolvingProperties.AddProperty(m_savePropertyNames[slot], JsonUtility.ToJson(local));
                }
                gameChanged = true;
            }
            m_hasUnresolvedConflicts = gameChanged;
            return gameChanged;
        }

        // 0x06002d28 and original instance callback 0x06002d65.
        private void OnPropertyStoreAfterConflictsResolved(HLPropertyList properties)
        {
            if (m_hasUnresolvedConflicts)
                Hardlight.Utils.CoroutineUtils.OnNextFrame(() =>
                {
                    // Native captures one property-store receiver; original source form and faults remain held.
                    HLPropertyStore propertyStore = m_hlPropertyStore;
                    OnPropertyStoreLoad(propertyStore.GetPropertyListFromSave(
                        propertyStore.GetSaveIdentifier()), false);
                    int index = CurrentSaveIndex;
                    CloseSave();
                    Hardlight.Utils.CoroutineUtils.RunCoroutine(OpenSave(index));
                    m_hasUnresolvedConflicts = false;
                });
            LastSavedAtTime = DateTime.UtcNow;
            properties.AddProperty(PropertyKeyLastSavedTimestamp, TimeUtils.ToUnixTimeMs(LastSavedAtTime));
            if (IsCloudSavingActive())
                m_cloud.SyncPropertyValueFromSave("HL_Default_Save_ID", PropertyKeyLastSavedTimestamp);
        }

        // 0x06002d29: the after-conflicts handler is deliberately not removed.
        private void Shutdown(object context = null)
        {
            if (m_saveAnalytics != null)
            {
                m_saveAnalytics.SessionSuspendedTimestamp = TimeUtils.ToUnixTimeMs(DateTime.UtcNow);
                RequestSaveAnalytics();
            }
            if (CurrentSave != null) CloseSave();
            HLPropertyStore.RemoveResolveConflictHandler(OnPropertyStoreConflict);
            HLPropertyStore.RemoveLoadHandler(OnPropertyStoreLoad);
            HLPropertyStore.RemoveSaveHandler(OnPropertyStoreSave);
            SystemReady = false;
        }

        // 0x06002d2a and complete original iterator 0x020007db.
        private IEnumerator SaveImmediate(bool synchroniseWithCloud, bool pushChanges = false)
        {
            if (!IsAnySaveOpen) yield break;
            CancelRequestedSaveInProgress();
            if (GameIsPlaying) m_currentSave.SetLastSavedTime(TimeUtils.ToUnixTimeMs(DateTime.UtcNow));
            yield return Save(synchroniseWithCloud);
            if (pushChanges && IsCloudSavingActive())
            {
                // Both native flag read and call use this receiver; original local syntax remains inferred.
                HLCloud.Cloud cloud = m_cloud;
                if (cloud.Synchronised) cloud.Synchronise();
            }
        }

        // 0x06002d2b and complete original iterator 0x020007da.
        private IEnumerator Save(bool synchroniseWithCloud)
        {
            if (!SaveSynchronous()) yield break;
            if (synchroniseWithCloud && IsCloudSavingActive())
            {
                if (m_cloud.Synchronised) yield return SyncCloudSave();
                else LoadFromCloud();
            }
        }

        // 0x06002d2c: the current save is dereferenced without an open-save guard.
        private bool SaveSynchronous(bool canResolveConflicts = true)
        {
            if (!m_loadCompleted) return false;
            m_currentSave.GameVersion = Application.version;
            m_rawSaves[CurrentSaveIndex] = JsonUtility.ToJson(m_currentSave);
            m_currentSave.MarkSaved();
            HLPropertyStore.SaveImmediate(canResolveConflicts);
            OnSaveCompleted();
            return true;
        }

        // 0x06002d2d / iterator 0x020007d7: genuinely returns without yielding.
        public IEnumerator InitialiseSaveData()
        {
            m_hlPropertyStore.LoadFromIdentifier("HL_Default_Save_ID");
            AddAllKeysToSync();
            if (IsCloudSavingActive()) LoadFromCloud();
            yield break;
        }

        // 0x06002d2e.
        private void LoadFromCloud()
        {
            m_cloud.Synchronise();
            if (m_cloud.Synchronised) m_cloud.MarkPropertyStoreToSynchronize();
        }

        // 0x06002d2f / iterator 0x020007d8. Cancellation precedes validation.
        public IEnumerator OpenSave(int saveFileIndex = IndexNoSaveOpen)
        {
            if (IsAnySaveOpen) yield break;
            CancelRequestedSaveInProgress();
            if (saveFileIndex == IndexNoSaveOpen)
                saveFileIndex = m_lastSaveIndex != IndexNoSaveOpen ? m_lastSaveIndex : 0;
            if (saveFileIndex < 0 || saveFileIndex >= MaxSaves) yield break;
            string raw = m_rawSaves[saveFileIndex];
            bool newFile = string.IsNullOrWhiteSpace(raw);
            if (newFile)
            {
                m_currentSave = new SaveDataGame();
                m_currentSave.CutsceneSeenSaveMaintenance = true;
            }
            else m_currentSave = JsonUtility.FromJson<SaveDataGame>(raw);
            m_currentSave.Initialise();
            CurrentSaveIndex = saveFileIndex;
            SetLastSaveIndex(saveFileIndex);
            foreach (ISaveGameListener listener in m_saveGameListeners) listener.OnSaveGameOpen(m_currentSave);
            foreach (ISaveGameListener listener in m_dependantSaveGameListeners) listener.OnSaveGameOpen(m_currentSave);
            if (newFile) yield return SaveImmediate(true);
        }

        // 0x06002d30.
        public void CloseSave()
        {
            if (!IsAnySaveOpen) return;
            CancelRequestedSaveInProgress();
            foreach (ISaveGameListener listener in m_saveGameListeners) listener.OnSaveGameClose(m_currentSave);
            foreach (ISaveGameListener listener in m_dependantSaveGameListeners) listener.OnSaveGameClose(m_currentSave);
            m_currentSave = null;
            CurrentSaveIndex = IndexNoSaveOpen;
            LastSavedAtTime = default;
        }

        // 0x06002d31.
        public void RequestSave()
        {
            if (m_saveRequested || m_saveCoroutine != null) return;
            m_saveCoroutine = Hardlight.Utils.CoroutineUtils.RunCoroutine(RequestSaveCoroutine());
        }

        // 0x06002d32 / iterator 0x020007d9. No terminal flag/handle clearing.
        private IEnumerator RequestSaveCoroutine()
        {
            m_saveRequested = true;
            yield return m_saveRequestDelay;
            if (m_saveRequested) yield return SaveImmediate(false);
        }

        // 0x06002d33.
        private bool CancelRequestedSaveInProgress()
        {
            m_saveRequested = false;
            bool running = m_saveCoroutine != null;
            if (running) Hardlight.Utils.CoroutineUtils.StopUtilCoroutine(ref m_saveCoroutine);
            return running;
        }

        // 0x06002d34 / iterator 0x020007dc.
        private IEnumerator SyncCloudSave()
        {
            // Native reads the flag and requests synchronization on one captured receiver.
            HLCloud.Cloud cloud = m_cloud;
            if (cloud.Synchronised) yield return cloud.SyncAllSaveIdentifiers("HL_Default_Save_ID");
        }

        // 0x06002d35.
        public void CopySave(int fromSlotIndex, int toSlotIndex)
        {
            if (CurrentSaveIndex == toSlotIndex) return;
            m_rawSaves[toSlotIndex] = m_rawSaves[fromSlotIndex];
            RequestSave();
        }

        // 0x06002d36.
        public void DeleteSave(int saveSlotIndex)
        {
            if (CurrentSaveIndex == saveSlotIndex) return;
            Hardlight.Utils.CoroutineUtils.StopUtilCoroutine(ref m_saveCoroutine);
            DeleteSaveSlot(saveSlotIndex);
            m_saveCoroutine = Hardlight.Utils.CoroutineUtils.RunCoroutine(SaveImmediate(true));
        }

        // 0x06002d37.
        private void DeleteSaveSlot(int saveSlotIndex)
        {
            var save = new SaveDataGame();
            save.CutsceneSeenSaveMaintenance = true;
            save.LastDeletionTimestamp = TimeUtils.ToUnixTimeMs(DateTime.UtcNow);
            save.Initialise();
            m_rawSaves[saveSlotIndex] = JsonUtility.ToJson(save);
        }

        // 0x06002d38.
        public void DeleteAllSaves()
        {
            if (CurrentSaveIndex != IndexNoSaveOpen) return;
            DeleteSaveSlot(0);
            DeleteSaveSlot(1);
            DeleteSaveSlot(2);
            RequestSave();
        }

        // 0x06002d39..3c: duplicate listeners can still get immediate callbacks.
        public void AddListener(ISaveGameListener listener, bool fireIfOpen = true)
        {
            m_saveGameListeners.AddUnique(listener);
            if (IsAnySaveOpen && fireIfOpen) listener.OnSaveGameOpen(m_currentSave);
        }
        public void RemoveListener(ISaveGameListener listener) => m_saveGameListeners.Remove(listener);
        public void AddDependantListener(ISaveGameListener listener, bool fireIfOpen = true)
        {
            m_dependantSaveGameListeners.AddUnique(listener);
            if (IsAnySaveOpen && fireIfOpen) listener.OnSaveGameOpen(m_currentSave);
        }
        public void RemoveDependantListener(ISaveGameListener listener) => m_dependantSaveGameListeners.Remove(listener);

        // 0x06002d3d: both architectures write the OLD index before assignment.
        public void SetLastSaveIndex(int slotIndex, bool saveToPropertyStore = true)
        {
            if (saveToPropertyStore)
            {
                ProcessManager.GetSystem<HLPropertyStore>().AddProperty(PropertyKeyLastSaveIndex, m_lastSaveIndex);
                m_cloud.SyncPropertyValueFromSave("HL_Default_Save_ID", PropertyKeyLastSaveIndex);
            }
            m_lastSaveIndex = slotIndex;
        }

        // 0x06002d3e/3f.
        public SaveDataSettings GetSaveDataSettings() => m_saveSettings;
        public bool HasSaveDataSettings() => m_saveSettings != null;

        // 0x06002d40.
        public void RequestSaveSettings()
        {
            m_rawSaveSettings = JsonUtility.ToJson(m_saveSettings);
            m_saveSettings.MarkSaved();
            SaveSynchronous();
        }

        // 0x06002d41.
        private void DeleteSaveSettings()
        {
            m_rawSaveSettings = null;
            m_saveSettings = new SaveDataSettings();
            SaveSynchronous();
        }

        // 0x06002d42: this does not reserialize the settings string.
        private void ResetWhatsNew()
        {
            if (string.IsNullOrWhiteSpace(m_rawSaveSettings) || m_saveSettings == null) return;
            m_saveSettings.LastWhatsNewVersion = string.Empty;
            SaveSynchronous();
        }

        // 0x06002d43: malformed/JSON-null strings receive no additional guard.
        public bool HasAnySave()
        {
            foreach (string raw in m_rawSaves)
                if (!string.IsNullOrWhiteSpace(raw) &&
                    !string.IsNullOrWhiteSpace(JsonUtility.FromJson<SaveDataGame>(raw).LastLevelVisitedGUID))
                    return true;
            return false;
        }

        // 0x06002d44..47.
        public bool HasSaveDataAnalytics() => m_saveAnalytics != null;
        public bool HasSaveDataNotifications() => m_saveDataNotifications != null;
        public SaveDataAnalytics GetSaveDataAnalytics() => m_saveAnalytics;
        public SaveDataNotifications GetSaveDataNotifications() => m_saveDataNotifications;

        // 0x06002d48.
        public void RequestSaveNotifications()
        {
            m_rawSaveNotifications = JsonUtility.ToJson(m_saveDataNotifications);
            m_saveDataNotifications.MarkSaved();
            SaveSynchronous();
        }

        // 0x06002d49: no synchronous save is requested by this original body.
        public void RequestSaveAnalytics()
        {
            m_rawSaveAnalytics = JsonUtility.ToJson(m_saveAnalytics);
            m_saveAnalytics.MarkSaved();
        }

        // 0x06002d4a.
        public void DeleteSaveAnalytics()
        {
            m_rawSaveAnalytics = null;
            m_saveAnalytics = new SaveDataAnalytics();
            SaveSynchronous();
        }

        // 0x06002d4b: shipped native builds labels then discards the menu paths.
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        private void AddDebugButtons()
        {
            for (int index = 0; index < MaxSaves; ++index)
            {
                string label = "Slot " + index.ToString();
                if (string.IsNullOrWhiteSpace(m_rawSaves[index])) label += " (empty)";
                if (CurrentSaveIndex == index) label = string.Concat(label, " (", "open", ")");
                else if (CurrentSaveIndex == IndexNoSaveOpen && m_lastSaveIndex == index) label = string.Concat(label, " (", "last", ")");
                _ = DebugMenuRoot + "/" + label;
            }
        }

        // 0x06002d4c..52.
        private string GetSynchronised() => string.Format("Synchronised: {0}", m_cloud.Synchronised);
        private string GetChangeReason() => string.Format("Change reason: {0}", m_cloud.CloudChangeInformation.ChangeReason);
        private string GetLocalVersion() => string.Format("Local version: {0}", m_cloud.CloudChangeInformation.LocalVersion);
        private string GetCloudVersion() => string.Format("Cloud version: {0}", m_cloud.CloudChangeInformation.CloudVersion);
        private string GetApplicationVersion() => "Application version: " + Application.version;
        private string GetApplicationVersionDefinitionState() => "Has version data: " +
            (ProcessManager.GetSystem<DataManager>().ApplicationVersionDefinitions.ContainsKey(Application.version) ? "Yes" : "No");
        private string GetLastSeenVersion() => "Last seen \"What's New\" version: " +
            (m_saveSettings != null && !string.IsNullOrWhiteSpace(m_saveSettings.LastWhatsNewVersion)
                ? m_saveSettings.LastWhatsNewVersion : "Not seen");

        // 0x06002d53.
        public void CompleteNextMission()
        {
            SaveDataLevelMission next = GetNextMission();
            if (next == null) return;
            if (!next.Complete) next.MarkComplete(1);
            RequestSave();
        }

        // 0x06002d54: preserves list order and each foreach disposal.
        public SaveDataLevelMission GetNextMission()
        {
            LevelManager levelManager = ProcessManager.GetSystem<LevelManager>();
            foreach (SaveDataLevel saveLevel in m_currentSave.Levels)
            {
                GameplayLevelDefinition definition = levelManager.GetProductionLevelByGUID(saveLevel.GUID);
                foreach (MissionGroup group in definition.MissionList.Groups)
                {
                    if (group.HasAlwaysHiddenRequirement()) continue;
                    foreach (MissionDefinition mission in group.Definitions)
                    {
                        SaveDataLevelMission saveMission = saveLevel.GetOrCreateMissionData(mission.GetGUID());
                        if (!saveMission.Complete) return saveMission;
                    }
                }
            }
            return null;
        }

        // 0x06002d55.
        public bool IsMissionCompletedInAnyLevel(string missionGuid)
        {
            foreach (SaveDataLevel level in m_currentSave.Levels)
                foreach (SaveDataLevelMission mission in level.Missions)
                    if (mission.Complete && mission.GUID == missionGuid) return true;
            return false;
        }

        // 0x06002d56.
        public void OnApplicationFocus() => Hardlight.Utils.CoroutineUtils.RunCoroutine(SaveImmediate(true, true));

        // 0x06002d57 is genuinely a single RET in both original architectures.
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        private void RemoveDebugButtons() { }

        // 0x06002d58: one getter call; the IndexOf result is deliberately unused.
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        private void ResetDebugButtons()
        {
            string path = DebugMenu.CurrentMenuPath;
            if (!string.IsNullOrEmpty(path)) _ = path.IndexOf(DebugMenuRoot);
        }

        // 0x06002d59: null IDs are rejected, empty IDs are accepted.
        public void SaveAnalyticsConsentData(string gameCenterAccountId, AnalyticsConsentState consentState,
            long updatedTimestamp = 0L)
        {
            if (gameCenterAccountId == null) return;
            m_saveAnalytics.SetConsentData(gameCenterAccountId, consentState, updatedTimestamp);
            RequestSaveAnalytics();
        }

        // 0x06002d5a.
        public void SaveAnalyticsSessionData(string sessionId, int sessionNumber, long sessionSuspendedTimestamp,
            long sessionStartTimestamp, float currentSessionLengthSeconds, bool saveImmediately)
        {
            m_saveAnalytics.SessionID = sessionId;
            m_saveAnalytics.SessionNumber = sessionNumber;
            m_saveAnalytics.SessionSuspendedTimestamp = sessionSuspendedTimestamp;
            m_saveAnalytics.SessionStartTimestamp = sessionStartTimestamp;
            m_saveAnalytics.CurrentSessionLengthSeconds = currentSessionLengthSeconds;
            if (saveImmediately) RequestSaveAnalytics();
        }

        // 0x06002d5b.
        public void OnAnalyticsNewInstall()
        {
            m_saveAnalytics.InstallDate = DateTime.Today.ToString("yyyy/MM/dd");
            RequestSaveAnalytics();
        }

        // 0x06002d5c.
        private bool SaveIdentifierValid() => m_hlPropertyStore.GetSaveIdentifier() == "HL_Default_Save_ID";

        // 0x06002d5d: preserves registry/message/action order.
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        private void ToggleCloudSave()
        {
            if (IsCloudSavingActive())
            {
                ProcessManager.ProcessSystemAction(m_cloud, SystemAction.Shutdown, null);
                ProcessManager.UnregisterSystem(m_cloud);
            }
            else
            {
                ProcessManager.RegisterSystem(m_cloud, null, false, false);
                m_cloud.SubscribeToSystemMessages();
                ProcessManager.ProcessSystemAction(m_cloud, SystemAction.Initialise, null);
                m_cloud.MarkPropertyStoreToSynchronize();
                HLPropertyStore.SaveImmediate(true);
            }
        }

        // 0x06002d5e.
        private static bool IsCloudSavingActive() => ProcessManager.IsSystemValid<HLCloud.Cloud>();

        // 0x06002d5f: native constructs labels but registers no debug buttons.
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        private void AddTimeTrialDebugOptions()
        {
            foreach (GameplayLevelDefinition level in ProcessManager.GetSystem<LevelManager>().GameLevels.GetLevels())
            {
                if (level.MissionList == null) continue;
                foreach (MissionDefinition mission in level.MissionList.GetMissions(false))
                {
                    SaveDataLevelMission saved = m_currentSave.GetOrCreateLevelData(level.GetGUID()).GetOrCreateMissionData(mission.GetGUID());
                    // Both shipped CPUs continue for unordered NaN; original source spelling/optimizer history is unavailable.
                    if (!(saved.BestTimeSeconds <= 0f))
                    {
                        string timer = MissionStringsUtil.GetMissionTimerFormat(saved.BestTimeSeconds, Strings.MISSION_TIMER_ELAPSED_FORMAT);
                        _ = mission.name + " - " + timer;
                    }
                }
            }
        }

        // 0x06002d60: the second parameter is unused. RequestSave is the genuine
        // SaveDataItem provider method; it is absent from the maintained class.
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        private void ResetMissionTimeTrial(SaveDataLevelMission missionSaveData, string buttonText)
        {
            missionSaveData.BestTimeSeconds = 0f;
            missionSaveData.RequestSave();
        }

        // 0x06002d61: skipAllCutscenes is never read; all cutscenes are marked.
        [System.Diagnostics.Conditional("BUILD_DEVELOPMENT")]
        private void DebugCompleteGame(string slotMenuPath, string buttonText, bool withSRank = false, bool skipAllCutscenes = false)
        {
            LevelManager levelManager = ProcessManager.GetSystem<LevelManager>();
            GameplayLevelDefinition level = levelManager.FinalBossLevelDefinition;
            if (level.MeetsAllRequirements()) return;
            MissionManager missionManager = ProcessManager.GetSystem<MissionManager>();
            SaveDataLevelMission next = GetNextMission();
            MissionDefinition mission = missionManager.GetMissionDefinition(next.GUID);
            while (mission != null)
            {
                // A failed rank lookup skips processing; no inferred repair.
                if (!next.Complete || (withSRank &&
                    mission.TryGetRankForTimeSeconds(next.BestTimeSeconds, out MissionRank rank) && rank.RankType != RankType.S))
                {
                    if (mission.Ranks != null) next.BestTimeSeconds = mission.GetBestRank().TimeInSeconds - 0.1f;
                    next.MarkComplete(1);
                    if (mission.UnlocksCharacterArchetype != 0)
                        m_currentSave.UnlockCharacterArchetype(mission.UnlocksCharacterArchetype);
                    if (level.MeetsAllRequirements()) break;
                }
                next = GetNextMission();
                mission = missionManager.GetMissionDefinition(next.GUID);
            }
            missionManager.PurgeMissionStates(false);
            m_currentSave.SetLastLevelVisited(level);
            ProcessManager.GetSystem<ProgressionManager>().OnSaveGameOpen(m_currentSave);
            if (ProcessManager.GetSystemRef<CharacterManager>().TryGet(out CharacterManager characterManager))
                characterManager.UpdateUnlocks();
            levelManager.UpdateUnlocks();
            missionManager.UpdateUnlocks();
            foreach (KeyValuePair<HLCutsceneIdentifier, CutsceneDefinition> pair in ProcessManager.GetSystem<DataManager>().CutsceneDefinitions)
                m_currentSave.AddSeenCutsceneGuid(pair.Value.GetGUID());
            List<IMetaGameUnlock> unlocks = ProcessManager.GetSystem<App>().Storage.GetValue<List<IMetaGameUnlock>>(
                AppFSMKeys.MetaGameUnlocks, null, true);
            for (int index = unlocks.Count - 1; index >= 0; --index) unlocks[index].Save(this);
            unlocks.Clear();
            Hardlight.Utils.CoroutineUtils.RunCoroutine(SaveImmediate(false));
        }

        // 0x06002d62 and 64 genuinely return false in both native architectures.
        private bool TryPreventSaveDowngrade(HLPropertyList properties) => false;

        // 0x06002d63.
        private void DoNewAppVersionRequired()
        {
            NewAppVersionRequired = true;
            OnNewAppVersionRequired();
        }
        private bool CanPreventSaveDowngrade() => false;
    }
}
