using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Hardlight.Utils;
using UnityEngine;

namespace Hardlight
{
    public partial class HLPropertyStore
    {
        public enum FileType { Primary = 0, Backup = 1 }
        private enum State { None = 0, Loading = 1, Saving = 2, Reset = 3 }
        public delegate void SaveHandler(HLPropertyList properties);
        public delegate bool ModifySaveHandler(HLPropertyList properties, ref bool conflictsResolved);
        public delegate void ResolveConflictHandler(HLPropertyList properties, bool overrideData, ref bool changesApplied);
        public delegate void SaveSuccessHandler(HLPropertyList properties);
        public delegate void AfterConflictsResolvedHandler(HLPropertyList properties);
        public delegate void LoadHandler(HLPropertyList properties, bool isNewFile);

        // Original backing field names; ordinary C# events generate the same
        // Interlocked.CompareExchange add/remove loops as the native accessors.
        private static event SaveHandler SaveHandlers;
        private static event ModifySaveHandler ModifySaveHandlers;
        private static event ResolveConflictHandler ResolveConflictHandlers;
        private static event SaveSuccessHandler SaveSuccessHandlers;
        private static event AfterConflictsResolvedHandler AfterConflictsResolvedHandlers;
        private static event LoadHandler LoadHandlers;

        private Coroutine m_delayedSaveCoroutine;
        private bool m_isLoaded;
        private State m_currentState;
        private IHLSaveMethod m_propertyFileStorage;
        private readonly int m_clientVersion;
        private const int m_unencryptedSaveVersion = 2;
        private readonly HLPropertyList ActiveProperties = new HLPropertyList();
        private readonly HLPropertyList TempProperties = new HLPropertyList();
        private string m_activeSaveIdentifier = DefaultSaveIdentifier;

        // 0x06000cee..0x06000cf6; arm64 0x1b0e1ec..0x1b0e674.
        public int UnencryptedSaveVersion => m_unencryptedSaveVersion;
        public bool CanSave => m_currentState == State.None;
        public bool IsLoaded => m_isLoaded;
        public static bool IsThereAnySaveHandler => SaveHandlers != null && SaveHandlers.GetInvocationList().Length != 0;
        public static bool IsThereAnyModifySaveHandler => ModifySaveHandlers != null && ModifySaveHandlers.GetInvocationList().Length != 0;
        public static bool IsThereAnyResolveConflictHandler => ResolveConflictHandlers != null && ResolveConflictHandlers.GetInvocationList().Length != 0;
        public static bool IsThereAnySaveSuccessHandler => SaveSuccessHandlers != null && SaveSuccessHandlers.GetInvocationList().Length != 0;
        public static bool IsThereAnyAfterConflictsResolvedHandler => AfterConflictsResolvedHandlers != null && AfterConflictsResolvedHandlers.GetInvocationList().Length != 0;
        public static bool IsThereAnyLoadHandler => LoadHandlers != null && LoadHandlers.GetInvocationList().Length != 0;

        // 0x06000d03; arm64 0x1b0f1b4. Construction does not load a save.
        // Publish the singleton before constructing the storage, as in the game.
        public HLPropertyStore(string key, int clientVersion, string outputFileName)
        {
            m_clientVersion = clientVersion;
            _ = Application.isPlaying;
            s_internalInstance = this;
#if PROJECT_LUCID_ORIGINAL_PROPERTY_STORAGE
            // Original storage selection, retained for research. Default port
            // builds use the separate offline provider below.
            m_propertyFileStorage = new HLSaveMethodEncrypted(key, outputFileName);
#else
            // Intentional port adaptation; preserve the original publication order.
            m_propertyFileStorage = ProjectLucid.Offline.OfflineProviders.CreatePropertyStorage(key, outputFileName);
#endif
        }

        // 0x06000d04..0x06000d0e: direct original subscription wrappers.
        public static void AddSaveHandler(SaveHandler handler) { SaveHandlers += handler; }
        public static void AddAfterConflictsResolvedHandler(AfterConflictsResolvedHandler handler) { AfterConflictsResolvedHandlers += handler; }
        public static void RemoveSaveHandler(SaveHandler handler) { SaveHandlers -= handler; }
        public static void AddModifySaveHandler(ModifySaveHandler handler) { ModifySaveHandlers += handler; }
        public static void RemoveModifySaveHandler(ModifySaveHandler handler) { ModifySaveHandlers -= handler; }
        public static void AddResolveConflictHandler(ResolveConflictHandler handler) { ResolveConflictHandlers += handler; }
        public static void RemoveResolveConflictHandler(ResolveConflictHandler handler) { ResolveConflictHandlers -= handler; }
        public static void AddSaveSuccessHandler(SaveSuccessHandler handler) { SaveSuccessHandlers += handler; }
        public static void RemoveSaveSuccessHandler(SaveSuccessHandler handler) { SaveSuccessHandlers -= handler; }
        public static void AddLoadHandler(LoadHandler handler, bool callIfLoaded = true)
        {
            LoadHandlers += handler;
            if (callIfLoaded && s_internalInstance != null) s_internalInstance.TriggerImmediateLoad(handler);
        }
        public static void RemoveLoadHandler(LoadHandler handler) { LoadHandlers -= handler; }

        // 0x06000d0f; arm64 0x1b0fbe0. Only the static entry point requires a
        // loaded store; the private writer independently guards current state.
        public static bool SaveImmediate(bool canResolveConflicts = false)
        {
            return s_internalInstance != null && s_internalInstance.IsLoaded &&
                s_internalInstance.SaveInternal(canResolveConflicts);
        }

        // 0x06000d10; arm64 0x1b0ff78. Repeated requests retain the first
        // iterator's captured conflict flag; there is no State.None guard here.
        public static bool SaveDelayed(bool canResolveConflicts = false)
        {
            if (s_internalInstance == null || !s_internalInstance.IsLoaded) return false;
            if (s_internalInstance.m_delayedSaveCoroutine != null) return true;
            s_internalInstance.m_delayedSaveCoroutine = CoroutineUtils.RunCoroutine(s_internalInstance.SaveAtEndOfFrame(canResolveConflicts));
            return true;
        }

        // 0x06000d11; original iterator MoveNext yields once before the write.
        private IEnumerator SaveAtEndOfFrame(bool canResolveConflicts)
        {
            yield return s_waitForEndOfFrame;
            if (s_internalInstance != null)
            {
                s_internalInstance.SaveInternal(canResolveConflicts);
                m_delayedSaveCoroutine = null;
            }
        }

        // 0x06000d12; arm64 0x1b101c4. Retail Save always saves immediately.
        public static bool Save(bool canResolveConflicts = false) { return SaveImmediate(canResolveConflicts); }

        // 0x06000d13; arm64 0x1b10244. No storage work without an instance.
        public static void Load()
        {
            if (s_internalInstance != null)
                s_internalInstance.LoadInternal(s_internalInstance.ActiveProperties, s_internalInstance.m_activeSaveIdentifier);
        }

        // 0x06000d14 / 0x06000d15; arm64 0x1b10454 / 0x1b104a4.
        public void LoadFromIdentifier(string id)
        {
            if (m_propertyFileStorage == null) return;
            m_activeSaveIdentifier = id;
            LoadInternal(ActiveProperties, m_activeSaveIdentifier);
        }
        public string GetSaveIdentifier() { return m_activeSaveIdentifier; }

        // 0x06000d17 / 0x06000d18; arm64 0x1b104ac / 0x1b10570.
        public IReadOnlyList<string> GetAllSaveIdentifiers() { return m_propertyFileStorage?.GetAllSaveIdentifiers(); }
        public void ClearSaveIdentifier()
        {
            if (m_propertyFileStorage == null) return;
            m_activeSaveIdentifier = DefaultSaveIdentifier;
            LoadInternal(ActiveProperties, m_activeSaveIdentifier);
        }

        // 0x06000d19; arm64 0x1b0fcdc. Callbacks observe State.Saving. A failed
        // callback leaves that state set: the original has no catch/finally.
        private bool SaveInternal(bool canResolveConflicts)
        {
            if (m_currentState != State.None) return false;
            m_currentState = State.Saving;
            SaveHandlers?.Invoke(ActiveProperties);
            bool conflictsResolved = false, changesApplied = false, resolverRan = false;
            if (canResolveConflicts && ModifySaveHandlers != null &&
                ModifySaveHandlers(ActiveProperties, ref conflictsResolved) && ResolveConflictHandlers != null)
            {
                ResolveConflictHandlers(ActiveProperties, conflictsResolved, ref changesApplied);
                resolverRan = true;
            }
            AfterConflictsResolvedHandlers?.Invoke(ActiveProperties);
            bool written = SavePropertyData(m_activeSaveIdentifier, ActiveProperties);
            m_currentState = State.None;
            if (!written || (canResolveConflicts && resolverRan && !conflictsResolved && !changesApplied)) return false;
            SaveSuccessHandlers?.Invoke(ActiveProperties);
            return true;
        }

        // 0x06000d1a; arm64 0x1b102f0. A temporary-slot read sets IsLoaded too.
        // Handlers run before changing IsLoaded and returning to State.None.
        private void LoadInternal(HLPropertyList propertyList, string saveIdentifier, bool notifyHandlers = true)
        {
            if (m_currentState != State.None) return;
            m_currentState = State.Loading;
            bool isNewFile = false;
            if (!LoadPropertyData(FileType.Primary, propertyList, saveIdentifier) &&
                !LoadPropertyData(FileType.Backup, propertyList, saveIdentifier))
            {
                propertyList.Properties.Clear();
                isNewFile = true;
            }
            if (notifyHandlers) LoadHandlers?.Invoke(propertyList, isNewFile);
            m_currentState = State.None;
            m_isLoaded = true;
        }

        // 0x06000d1b; arm64 0x1b10624. Names are not serialized. Values are not
        // escaped; keep the shipped two-line-per-property framing and list order.
        private bool SavePropertyData(string saveIdentifier, HLPropertyList propertyList)
        {
            var builder = new StringBuilder(string.Format("{0}\n{1}\n", m_clientVersion, propertyList.Properties.Count));
            List<HLPropertyList.Property> properties = propertyList.Properties;
            for (int i = 0; i < properties.Count; ++i)
            {
                HLPropertyList.Property property = properties[i];
                builder.AppendLine(property.m_nameCRC.ToString());
                builder.AppendLine(property.m_value);
            }
            if (!m_propertyFileStorage.SaveData(FileType.Primary, builder, saveIdentifier))
            {
                HLOutput.LogError("Unable to save HLPropertyStore data of save identifier '" + saveIdentifier + "'.");
                return false;
            }
            if (!m_propertyFileStorage.BackupData(saveIdentifier))
            {
                HLOutput.LogError("Unable to backup HLPropertyStore data of save identifier '" + saveIdentifier + "'.");
                return false;
            }
            return true;
        }

        // 0x06000d1c; arm64 0x1b10928. Header failures retain the destination;
        // a body parse failure can leave it partly populated. Trailing lines are
        // ignored and repeated CRCs update the first entry through AddProperty.
        private bool LoadPropertyData(FileType fileType, HLPropertyList propertyList, string saveIdentifier)
        {
            string content = m_propertyFileStorage.LoadData(fileType, saveIdentifier);
            if (string.IsNullOrEmpty(content)) return false;
            int first = content.IndexOf('\n', 0);
            if (first == -1 || !int.TryParse(content.Substring(0, first), out int version) || version != m_clientVersion) return false;
            int start = first + 1;
            int second = content.IndexOf('\n', start);
            if (second == -1 || !int.TryParse(content.Substring(start, second - start), out int count)) return false;
            string[] lines = content.Substring(second + 1).Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.None);
            propertyList.Properties.Clear();
            propertyList.Properties.Capacity = count;
            for (int i = 0; i < count; ++i)
            {
                string crc = lines[i * 2], value = lines[i * 2 + 1];
                if (crc == null || value == null || !uint.TryParse(crc, out uint propertyCRC)) return false;
                propertyList.AddProperty<string>(null, propertyCRC, value);
            }
            return true;
        }

        // 0x06000d1d; arm64 0x1b0fb38. A null handler on a loaded store throws.
        private void TriggerImmediateLoad(LoadHandler handler) { if (m_isLoaded) handler(ActiveProperties, false); }

        // 0x06000d1e; arm64 0x1b10c1c. Reset clears memory, sends a new-file
        // notification and leaves IsLoaded false; it never deletes disk data.
        public void Reset()
        {
            if (m_currentState != State.None) return;
            m_isLoaded = false;
            m_currentState = State.Reset;
            ActiveProperties.Properties.Clear();
            LoadHandlers?.Invoke(ActiveProperties, true);
            m_currentState = State.None;
        }

        // 0x06000d1f..0x06000d21: these delegate only, with no notifications.
        public void WipeSaveFile() { m_propertyFileStorage.WipeSaveFile(m_activeSaveIdentifier); }
        public void WipeSaveFile(string saveIdentifier) { m_propertyFileStorage.WipeSaveFile(saveIdentifier); }
        public void WipeAllSaveFiles() { m_propertyFileStorage.WipeAllSaveFiles(); }

        // 0x06000d22; arm64 0x1b10f30. ID comparisons use InvariantCulture (2).
        public HLPropertyList GetPropertyListFromSave(string saveIdentifier)
        {
            if (string.Equals(saveIdentifier, m_activeSaveIdentifier, StringComparison.InvariantCulture)) return ActiveProperties;
            var result = new HLPropertyList();
            LoadInternal(result, saveIdentifier, false);
            return result;
        }

        // 0x06000d23 / 0x06000d24 / 0x06000d25.
        public string GetPropertyValue(string key) { return ActiveProperties.AsString(key, DefaultPropertyValue); }
        public bool RemoveProperty(string key) { return ActiveProperties.RemoveProperty(key); }
        public void AddProperty<T>(string key, T value) { ActiveProperties.AddProperty(key, value); }

        // 0x06000d26; arm64 0x1b110e8. Nonactive reads reuse the private list
        // and do not change the active identifier or notify load handlers.
        public string GetPropertyValueFromSave(string saveIdentifier, string key)
        {
            if (string.Equals(saveIdentifier, m_activeSaveIdentifier, StringComparison.InvariantCulture)) return GetPropertyValue(key);
            TempProperties.Properties.Clear();
            LoadInternal(TempProperties, saveIdentifier, false);
            return TempProperties.AsString(key, DefaultPropertyValue);
        }

        // 0x06000d27; shared arm64 0x9121c8. This direct write bypasses all save
        // callbacks and state gates. RGCTX forwards HLPropertyList.AddProperty<T>.
        public bool TrySetPropertyValueOnSave<T>(string saveIdentifier, string key, T value)
        {
            if (string.Equals(saveIdentifier, m_activeSaveIdentifier, StringComparison.InvariantCulture))
            {
                ActiveProperties.AddProperty(key, value);
                return SavePropertyData(m_activeSaveIdentifier, ActiveProperties);
            }
            TempProperties.Properties.Clear();
            LoadInternal(TempProperties, saveIdentifier, false);
            TempProperties.AddProperty(key, value);
            return SavePropertyData(saveIdentifier, TempProperties);
        }

        // 0x06000d28; arm64 0x1b11214. The fallback deliberately asks storage
        // for the ACTIVE slot, even when the caller requested a different slot.
        public bool TryGetSaveVersion(string saveIdentifier, out long version)
        {
            string value = GetPropertyValueFromSave(saveIdentifier, saveIdentifier);
            if (!string.Equals(value, DefaultPropertyValue, StringComparison.InvariantCulture)) return long.TryParse(value, out version);
            version = 0L;
            return m_propertyFileStorage != null && m_propertyFileStorage.TryGetSaveVersion(m_activeSaveIdentifier, out version);
        }

        // 0x06000d29 / 0x06000d2a; arm64 0x1b1136c / 0x1b11430.
        // Ties/nonpositive versions select the first item; an empty non-null
        // collection indexes item zero and throws, matching the supplied release.
        public string GetLastSaveIdentifier() { return GetLastSaveIdentifier(GetAllSaveIdentifiers()); }
        public string GetLastSaveIdentifier(IReadOnlyList<string> allSaveIdentifiers)
        {
            if (allSaveIdentifiers == null) return DefaultSaveIdentifier;
            long bestVersion = 0L;
            int bestIndex = 0;
            int count = allSaveIdentifiers.Count;
            for (int i = 0; i < count; ++i)
                if (TryGetSaveVersion(allSaveIdentifiers[i], out long version) && version > bestVersion)
                { bestVersion = version; bestIndex = i; }
            return allSaveIdentifiers[bestIndex];
        }

        // 0x06000d2b; arm64 0x1b11650. AfterConflictsResolvedHandlers is
        // intentionally retained by the original. Other instance state is kept.
        public void Shutdown()
        {
            s_internalInstance = null;
            LoadHandlers = null;
            SaveHandlers = null;
            ModifySaveHandlers = null;
            SaveSuccessHandlers = null;
            ResolveConflictHandlers = null;
            if (m_delayedSaveCoroutine != null)
                CoroutineUtils.StopUtilCoroutine(ref m_delayedSaveCoroutine);
        }

        // 0x06000d2c is a single RET in this retail player.
        [Conditional("BUILD_DEVELOPMENT")]
        private void ValidateHandlers(string handlersName, Delegate[] delegates) { }
    }
}
