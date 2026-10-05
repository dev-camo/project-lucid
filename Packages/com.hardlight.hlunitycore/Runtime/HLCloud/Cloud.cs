using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using HLCloud.Plugin;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HLCloud
{
    // Original HLUnityCore.Runtime type0x0200000c. Authored callers retain this
    // facade; NativePluginInstance supplies the intentional local storage adapter.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class Cloud : MonoBehaviour, ISystem, ICloud
    {
        [SerializeField, Tooltip("If not set it will use the teamPlayerID.")]
        private bool m_useGamePlayerID;
        [SerializeField, Tooltip("Tick if you don't want to manually register the system.")]
        private bool m_registerSelfOnAwake;
        [Tooltip("If true, will use GameObject.Find to publish messages to this MonoBehaviour, instead of C# Actions. Note that this is legacy behaviour and is here for compatibility purposes, prefer turning this off when possible.")]
        [SerializeField]
        private bool m_useFindGameObject = true;
        [SerializeField, Tooltip("For supported platforms, prevent synchronisation from occurring more frequently than this.")]
        private float m_synchronisationCooldownSeconds;

        public CloudChangeInformation CloudChangeInformation { get; private set; }
        public bool Synchronised { get; private set; }
        private static Plugin.Cloud m_plugin;
        private readonly HashSet<string> KeysToSync = new HashSet<string>();
        private bool m_cacheChangedSave;
        private bool m_cacheChangedVersion;
        private string m_cacheCloudDidChangeMessage = string.Empty;
        private readonly Dictionary<string, string> ChangedCloudKeys = new Dictionary<string, string>();
        private readonly List<string> AllCloudSaveIdentifiers = new List<string>();
        private readonly List<string> AllSaveIdentifiers = new List<string>();
        private Coroutine m_cloudChangeCoroutine;
        private const string AllCloudKeysSeparator = "|";
        private bool m_isInitialised;

        // 0x06000032..36: dirty is pending dictionary count, not the cached flag.
        public bool IsDirty => ChangedCloudKeys.Count > 0;

        // 0x06000037: only the first instance initializes/subscribes. A failure
        // after plugin assignment retains it and later Awake calls short-circuit.
        private void Awake()
        {
            if (m_plugin != null) return;
            m_plugin = Plugin.Cloud.NativePluginInstance(this, m_useFindGameObject);
            if (m_useFindGameObject) Plugin.Cloud.SubscribeOnConnect(Native_CloudDidChange);
            m_plugin.InitializeWithGameObjectName(name, m_useGamePlayerID);
            m_plugin.SetSynchronisationCooldown(m_synchronisationCooldownSeconds);
            if (m_registerSelfOnAwake) ProcessManager.RegisterSystem(this);
            SubscribeToSystemMessages();
        }

        public void SubscribeToSystemMessages() // 0x06000038
        {
            this.SubscribeToAction(SystemAction.Initialise, OnInitialise);
            this.SubscribeToAction(SystemAction.Update, OnUpdate);
            this.SubscribeToAction(SystemAction.Shutdown, OnShutdown);
        }

        // 0x06000039: immediate load callbacks observe initialized=true, even
        // if subsequent subscription work fails. There is no rollback.
        private void OnInitialise(object context = null)
        {
            if (m_isInitialised) return;
            m_isInitialised = true;
            HLPropertyStore.AddLoadHandler(OnPropertyStoreLoad, true);
            HLPropertyStore.AddAfterConflictsResolvedHandler(OnPropertyStoreSave);
            HLPropertyStore.AddModifySaveHandler(ModifySaveHandler);
            HLPropertyStore.AddSaveSuccessHandler(SaveSuccessHandler);
        }

        private void OnUpdate(object context = null) { ConsumeCloudDidChangeMessage(); } // 0x0600003a

        // 0x0600003b: the original removes SaveHandler rather than its registered
        // AfterConflictsResolvedHandler. Preserve that lifecycle distinction.
        private void OnShutdown(object context = null)
        {
            if (m_useFindGameObject) Plugin.Cloud.UnsubscribeOnConnect(Native_CloudDidChange);
            if (!m_isInitialised) return;
            m_isInitialised = false;
            HLPropertyStore.RemoveLoadHandler(OnPropertyStoreLoad);
            HLPropertyStore.RemoveSaveHandler(OnPropertyStoreSave);
            HLPropertyStore.RemoveModifySaveHandler(ModifySaveHandler);
            HLPropertyStore.RemoveSaveSuccessHandler(SaveSuccessHandler);
            if (m_cloudChangeCoroutine != null)
            {
                StopCoroutine(m_cloudChangeCoroutine);
                m_cloudChangeCoroutine = null;
            }
        }

        private void OnPropertyStoreLoad(HLPropertyList propertyList, bool isNewFile) // 0x0600003c
        {
            string id = ProcessManager.GetSystemSafe<HLPropertyStore>().GetSaveIdentifier();
            string defaultVersion = DateTime.Now.Ticks.ToString();
            SetCloudValueFromProperty(propertyList.AsString(id, defaultVersion), id, id);
            KeysToSync.Add(id);
            List<HLPropertyList.Property> properties = propertyList.Properties;
            int count = properties.Count;
            for (int i = 0; i < count; ++i)
            {
                HLPropertyList.Property property = properties[i];
                if (KeysToSync.Contains(property.m_name))
                {
                    string cloudKey = GetCloudKey(property.m_nameCRC, id);
                    ChangedCloudKeys[cloudKey] = m_plugin.StringForKey(cloudKey);
                }
            }
        }

        private void OnPropertyStoreSave(HLPropertyList propertyList) // 0x0600003d
        {
            string id = ProcessManager.GetSystemSafe<HLPropertyStore>().GetSaveIdentifier();
            propertyList.AddProperty(id, DateTime.Now.Ticks.ToString());
        }

        public void MarkPropertyStoreToSynchronize() // 0x0600003e
        {
            string id = ProcessManager.GetSystemSafe<HLPropertyStore>().GetSaveIdentifier();
            foreach (string propertyKey in KeysToSync)
            {
                string value = GetCloudValueFromProperty(propertyKey, id, out string cloudKey);
                ChangedCloudKeys[cloudKey] = value;
            }
        }

        // 0x0600003f: lookup occurs before the sync gate. Reading another save
        // uses the store's direct path and retains the active identifier.
        public void SyncPropertyValueFromSave(string saveIdentifier, string propertyKey)
        {
            HLPropertyStore store = ProcessManager.GetSystemSafe<HLPropertyStore>();
            if (!Synchronised) return;
            SetCloudValueFromProperty(store.GetPropertyValueFromSave(saveIdentifier, propertyKey), propertyKey, saveIdentifier);
            Synchronised = m_plugin.Synchronize();
        }

        private string GetCloudKey(uint propertyKeyCrc, string saveIdentifier) // 0x06000040
        {
            OpString builder = OpString.i;
            builder += saveIdentifier;
            builder += propertyKeyCrc;
            return builder.ToString();
        }

        // 0x06000041: CRC text matches anywhere and every occurrence is removed.
        private bool TryGetCloudSaveIdentifier(string cloudKey, string propertyKeyCrcString, out string saveIdentifier)
        {
            saveIdentifier = HLPropertyStore.DefaultSaveIdentifier;
            bool found = cloudKey.Contains(propertyKeyCrcString);
            if (found) saveIdentifier = cloudKey.Replace(propertyKeyCrcString, string.Empty);
            return found;
        }

        private IReadOnlyList<string> GetAllSaveIdentifiers(string propertyKeyToGetAllCloudSaveIdentifiers) // 0x06000042
        {
            AllCloudSaveIdentifiers.Clear();
            HashSet<string> cloudKeys = m_plugin.RetrieveAllCloudKeys(AllCloudKeysSeparator);
            if (cloudKeys != null && cloudKeys.Count > 0)
            {
                string crc = HLPropertyStore.GetCRC(propertyKeyToGetAllCloudSaveIdentifiers).ToString();
                foreach (string key in cloudKeys)
                    if (TryGetCloudSaveIdentifier(key, crc, out string id) && !AllCloudSaveIdentifiers.Contains(id))
                        AllCloudSaveIdentifiers.Add(id);
            }
            return AllCloudSaveIdentifiers;
        }

        // 0x06000043 and original iterator MoveNext: one readiness child yield,
        // then synchronous per-ID loads/saves. The final selected ID is retained.
        public IEnumerator SyncAllSaveIdentifiers(string propertyKeyToGetAllCloudSaveIdentifiers)
        {
            yield return WaitUntilReady();
            HLPropertyStore store = ProcessManager.GetSystemSafe<HLPropertyStore>();
            IReadOnlyList<string> localIds = store.GetAllSaveIdentifiers();
            IReadOnlyList<string> cloudIds = GetAllSaveIdentifiers(propertyKeyToGetAllCloudSaveIdentifiers);
            AllSaveIdentifiers.Clear();
            if (localIds != null) AllSaveIdentifiers.AddRange(localIds);
            if (cloudIds != null)
            {
                int cloudCount = cloudIds.Count;
                for (int i = 0; i < cloudCount; ++i) AllSaveIdentifiers.AddUnique(cloudIds[i]);
            }
            int count = AllSaveIdentifiers.Count;
            for (int i = 0; i < count; ++i)
            {
                store.LoadFromIdentifier(AllSaveIdentifiers[i]);
                MarkPropertyStoreToSynchronize();
                HLPropertyStore.SaveImmediate(true);
            }
        }

        private IEnumerator WaitUntilReady() // 0x06000044
        {
            while (ProcessManager.IsSystemNull<HLPropertyStore>() || !m_isInitialised) yield return null;
        }

        // 0x06000045: mutate only existing named subscribed properties. Both
        // native comparisons use InvariantCulture(2); empty changes are skipped.
        private bool ModifySaveHandler(HLPropertyList propertyList, ref bool conflictsResolved)
        {
            if (m_cacheChangedSave || ChangedCloudKeys.Count == 0) return false;
            string id = ProcessManager.GetSystemSafe<HLPropertyStore>().GetSaveIdentifier();
            bool modified = false;
            foreach (KeyValuePair<string, string> entry in ChangedCloudKeys)
            {
                if (string.IsNullOrEmpty(entry.Value)) continue;
                HLPropertyList.Property property = propertyList.Properties.Find(
                    p => GetCloudKey(p.m_nameCRC, id).Equals(entry.Key, StringComparison.InvariantCulture));
                if (property == null || !KeysToSync.Contains(property.m_name)) continue;
                if (!string.Equals(property.m_value, entry.Value, StringComparison.InvariantCulture))
                {
                    property.m_value = entry.Value;
                    modified = true;
                }
            }
            if (m_cacheChangedVersion) { conflictsResolved = true; m_cacheChangedVersion = false; }
            ChangedCloudKeys.Clear();
            return modified;
        }

        private void SaveSuccessHandler(HLPropertyList propertyList) // 0x06000046
        {
            if (IsDirty) return;
            HLPropertyStore store = ProcessManager.GetSystemSafe<HLPropertyStore>();
            List<HLPropertyList.Property> properties = propertyList.Properties;
            int count = properties.Count;
            if (count > 0)
            {
                string id = store.GetSaveIdentifier();
                for (int i = 0; i < count; ++i)
                {
                    HLPropertyList.Property property = properties[i];
                    if (KeysToSync.Contains(property.m_name)) SetCloudValueFromProperty(property.m_value, property.m_nameCRC, id);
                }
            }
            Synchronised = m_plugin.Synchronize();
        }

        public void ResetData() // 0x06000047
        {
            ResetData(ProcessManager.GetSystemSafe<HLPropertyStore>().GetSaveIdentifier());
        }
        public void ResetData(string saveIdentifier) // 0x06000048
        {
            foreach (string key in m_plugin.RetrieveAllCloudKeys(AllCloudKeysSeparator))
                if (key.Contains(saveIdentifier)) m_plugin.RemoveForKey(key);
        }
        public void AddPropertyKeyToSync(string propertyKey) { KeysToSync.Add(propertyKey); } // 0x06000049
        private void InternalAddKeyToSync(string key) { KeysToSync.Add(key); } // 0x0600004a
        public bool ApplySaveChange() // 0x0600004b
        {
            bool changed = m_cacheChangedSave;
            if (changed) m_cacheChangedSave = false;
            return changed;
        }

        private string GetCloudValueFromProperty(string propertyKey, string saveIdentifier) // 0x0600004c
        {
            return m_plugin.StringForKey(GetCloudKey(HLPropertyStore.GetCRC(propertyKey), saveIdentifier));
        }
        private string GetCloudValueFromProperty(string propertyKey, string saveIdentifier, out string cloudKey) // 0x0600004d
        {
            cloudKey = GetCloudKey(HLPropertyStore.GetCRC(propertyKey), saveIdentifier);
            return m_plugin.StringForKey(cloudKey);
        }
        private string GetCloudValue(string cloudKey) { return m_plugin.StringForKey(cloudKey); } // 0x0600004e
        private bool TryGetCloudSaveVersion(string saveIdentifier, out long cloudVersion) // 0x0600004f
        {
            return long.TryParse(m_plugin.StringForKey(GetCloudKey(HLPropertyStore.GetCRC(saveIdentifier), saveIdentifier)), out cloudVersion);
        }

        // 0x06000050/51: construct/hash the key even when not yet synchronized.
        private void SetCloudValueFromProperty(string cloudValue, string propertyKey, string saveIdentifier)
        {
            string key = GetCloudKey(HLPropertyStore.GetCRC(propertyKey), saveIdentifier);
            if (Synchronised) m_plugin.SetStringForKey(cloudValue, key);
        }
        private void SetCloudValueFromProperty(string cloudValue, uint propertyKeyCrc, string saveIdentifier)
        {
            string key = GetCloudKey(propertyKeyCrc, saveIdentifier);
            if (Synchronised) m_plugin.SetStringForKey(cloudValue, key);
        }
        private void SetCloudValue(string cloudKey, string cloudVersion) // 0x06000052
        {
            if (Synchronised) m_plugin.SetStringForKey(cloudVersion, cloudKey);
        }

        private void ConsumeCloudDidChangeMessage() // 0x06000053
        {
            if (m_useFindGameObject || string.IsNullOrWhiteSpace(m_cacheCloudDidChangeMessage)) return;
            m_plugin.CloudDidChange(m_cacheCloudDidChangeMessage);
            m_cacheCloudDidChangeMessage = string.Empty;
        }
        public void Native_CloudDidChange(string message) // 0x06000054
        {
            if (m_useFindGameObject) m_plugin.CloudDidChange(message);
            else m_cacheCloudDidChangeMessage = message;
        }

        // 0x06000055: do not cancel the previous coroutine. An older completion
        // can clear the handle after a newer callback has assigned it.
        void ICloud.OnCloudChange(string[] changedKeys, ChangeReason changeReason)
        {
            m_cloudChangeCoroutine = StartCoroutine(OnCloudChangeWaitForRequiredSystems(changedKeys, changeReason));
        }
        private IEnumerator OnCloudChangeWaitForRequiredSystems(string[] changedCloudKeys, ChangeReason changeReason) // 0x06000056
        {
            yield return WaitUntilReady();
            HLPropertyStore store = ProcessManager.GetSystemSafe<HLPropertyStore>();
            string id = store.GetSaveIdentifier();
            int count = changedCloudKeys.Length;
            for (int i = 0; i < count; ++i)
            {
                string key = changedCloudKeys[i];
                ChangedCloudKeys[key] = m_plugin.StringForKey(key);
            }
            KeysToSync.Add(id);
            store.TryGetSaveVersion(id, out long localVersion);
            TryGetCloudSaveVersion(id, out long cloudVersion);
            m_cacheChangedVersion = cloudVersion > localVersion;
            CloudChangeInformation = new CloudChangeInformation(changeReason, localVersion, cloudVersion, ChangedCloudKeys);
            m_cloudChangeCoroutine = null;
        }

        public void Synchronise() { Synchronised = m_plugin.Synchronize(); } // 0x06000057
        // Field initializers above retain the original constructor0x06000058.
    }
}
