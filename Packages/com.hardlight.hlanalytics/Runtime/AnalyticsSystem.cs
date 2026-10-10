using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hardlight.JSON;
using Hardlight.Networking;
using Hardlight.Pooling;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original HLAnalytics.Runtime 0x02000005: all 48 own methods and the original
    // callback/three iterator flows. Natural generated layout and native binding
    // remain separately unverified; no authored replacement nested classes.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AnalyticsSystem : IAnalytics
    {
        private readonly Queue<AnalyticsEventDataHolder> m_analyticsEventDataHolderQueue = new Queue<AnalyticsEventDataHolder>(10);
        private readonly Dictionary<int, List<AnalyticsEventDataHolder>> m_analyticsEventDataHolderManualListPerCategory = new Dictionary<int, List<AnalyticsEventDataHolder>>(10);
        private bool m_postInProgress;
        private bool m_isInitialised;
        private IAnalyticsSettings m_analyticsSettings;
        private int m_eventIndex;
        private readonly string m_manualEventsLogPrefix = string.Format("manual{0}events{1}", '_', '_');
        private readonly bool m_sendData;
        private readonly string m_path;
        private readonly string m_pendingEventsFilePath;
        private readonly string m_manualEventsFilePathPrefix;
        private readonly string m_manualEventsFilePathPattern;
        private readonly string m_manualEventsFilePathSuffix;
        private readonly string m_failedEventsFilePath;
        private readonly string m_persistentDataFilePath;
        private readonly List<string> m_stringListCache = new List<string>(10);
        private Coroutine m_waitAnalyticsEventsSendingAllowance;
        private Coroutine m_waitAnalyticsEventsCollectionNotAllowed;
        private readonly Coroutine[] m_activeCoroutines = new Coroutine[10];
        private readonly List<int> m_activeCoroutineIndexes = new List<int>(10);
        private readonly string m_categoryErrorMessage = string.Format("You can't use category {0}, it's a special category used internally", 0);
        private readonly Action<IAnalyticsEvent> m_internalPushAnalyticsEventToAutomaticQueueAction;
        private readonly Action<IAnalyticsEvent> m_internalProcessAnalyticsEventImmediatelyAction;

        // 0x06000021: genuine original constant, not a replacement service verdict.
        public bool IsSupported => true;
        // 0x06000022: original telemetry paths and actions remain preserved source.
        public AnalyticsSystem()
        {
            m_path = Application.persistentDataPath + "/telemetry_events.log";
            m_pendingEventsFilePath = Application.persistentDataPath + "/pending_events.log";
            m_manualEventsFilePathPrefix = Application.persistentDataPath + "/" + m_manualEventsLogPrefix;
            m_manualEventsFilePathSuffix = ".log";
            m_manualEventsFilePathPattern = m_manualEventsLogPrefix + "*.log";
            m_failedEventsFilePath = Application.persistentDataPath + "/failed_events.log";
            m_persistentDataFilePath = Application.persistentDataPath + "/persistent_data.log";
            m_sendData = true;
            m_internalPushAnalyticsEventToAutomaticQueueAction = InternalPushAnalyticsEventToAutomaticQueue;
            m_internalProcessAnalyticsEventImmediatelyAction = InternalProcessAnalyticsEventImmediately;
        }
        // 0x06000023: invalid setting values are logged; only the first two checks return.
        public void Initialise(IAnalyticsSettings analyticsSettings)
        {
            if (IsInitialised(false)) { HLOutput.LogError("Analytics has already been initialised!"); return; }
            if (analyticsSettings == null) { HLOutput.LogError("You cannot set analytics settings to null"); return; }
            HLOutput.LogError(string.IsNullOrEmpty(analyticsSettings.GameID), "GameID in analytics settings cannot be empty or null!");
            HLOutput.LogError(string.IsNullOrEmpty(analyticsSettings.Environment), "Environment in analytics settings cannot be empty or null!");
            HLOutput.LogError(analyticsSettings.ServerBaseURL == null, "ServerBaseURL in analytics settings cannot be null!");
            HLOutput.LogError(analyticsSettings.MaxQueueLength <= 0, "MaxQueueLength in analytics settings cannot be 0 or negative!");
            HLOutput.LogError(analyticsSettings.MaxSendAttempts <= 0, "MaxSendAttempts in analytics settings cannot be 0 or negative!");
            m_isInitialised = true;
            m_analyticsSettings = analyticsSettings;
            RestoreAnalyticsEvents();
            ResendFailedEventsFromDisk();
        }
        // 0x06000024
        public void ResetEventIndex() { if (IsInitialised()) m_eventIndex = 0; }
        // 0x06000025
        public void ProcessManualQueue(int category) { if (IsInitialised()) InternalProcessManualQueue(category); }
        // 0x06000026: dictionary entry is removed before the send call.
        private void InternalProcessManualQueue(int category, bool checkCategory = true)
        {
            if (!IsInitialised() || !CanProcessManualQueue(category)) return;
            if (category == 0 && checkCategory) { HLOutput.LogError(m_categoryErrorMessage); return; }
            List<AnalyticsEventDataHolder> events = m_analyticsEventDataHolderManualListPerCategory[category];
            m_analyticsEventDataHolderManualListPerCategory.Remove(category);
            SendAnalyticsEvents(events, true);
        }
        // 0x06000027
        private void InternalProcessAnalyticsEventImmediately(IAnalyticsEvent analyticsEvent)
        {
            JSONHashtable data = analyticsEvent.CreateData();
            analyticsEvent.ReleaseEvent();
            AnalyticsEventDataHolder holder = AnalyticsEventDataHolder.Create(data);
            List<AnalyticsEventDataHolder> events = SpawnAnalyticsEventDataHolderList();
            events.Add(holder);
            SendAnalyticsEvents(events, true);
        }
        // 0x06000028
        private void InternalPushAnalyticsEventToAutomaticQueue(IAnalyticsEvent analyticsEvent)
        {
            JSONHashtable data = analyticsEvent.CreateData();
            analyticsEvent.ReleaseEvent();
            EnqueueAnalyticsEventToQueue(AnalyticsEventDataHolder.Create(data));
            TryPumpEventQueue();
        }
        // 0x06000029: forbidden category returns without releasing the event.
        public void PushAnalyticsEventToManualQueue(IAnalyticsEvent analyticsEvent, int category)
        {
            if (!IsInitialised()) return;
            if (category == 0) { HLOutput.LogError(m_categoryErrorMessage); return; }
            if (!m_analyticsSettings.IsAnalyticsEventToManualQueueCollectionEnabled(category))
            {
                analyticsEvent.ReleaseEvent();
                return;
            }
            if (CanPushAnalyticsEventToManualQueue(analyticsEvent, category)) InternalPushAnalyticsEventToManualQueue(analyticsEvent, category);
        }
        // 0x0600002a
        private void InternalPushAnalyticsEventToManualQueue(IAnalyticsEvent analyticsEvent, int category)
        {
            JSONHashtable data = analyticsEvent.CreateData();
            analyticsEvent.ReleaseEvent();
            InternalPushAnalyticsEventDataHolderToManualQueue(category, AnalyticsEventDataHolder.Create(data));
        }
        // 0x0600002b: genuine Dictionary.Add, retaining duplicate-key faults.
        private void InternalPushAnalyticsEventDataHolderToManualQueue(int category, AnalyticsEventDataHolder eventDataHolder)
        {
            List<AnalyticsEventDataHolder> events;
            if (!m_analyticsEventDataHolderManualListPerCategory.TryGetValue(category, out events))
            {
                events = SpawnAnalyticsEventDataHolderList();
                m_analyticsEventDataHolderManualListPerCategory.Add(category, events);
            }
            events.Add(eventDataHolder);
        }
        // 0x0600002c
        public IAnalyticsSettings GetAnalyticsSettings() => IsInitialised() ? m_analyticsSettings : null;
        // 0x0600002d: original post-increment, including unchecked wrap.
        public int GetEventIndex() => IsInitialised() ? unchecked(m_eventIndex++) : -1;
        // 0x0600002e
        public void ApplicationPause(bool paused)
        {
            if (!IsInitialised()) return;
            if (paused) StoreAnalyticsEvents(); else RestoreAnalyticsEvents();
        }
        // 0x0600002f: post-in-progress is not reset by this original method.
        public void Shutdown()
        {
            if (!IsInitialised()) return;
            StoreAnalyticsEvents();
            StopAllLateDataCoroutines();
            CoroutineUtils.StopUtilCoroutine(ref m_waitAnalyticsEventsSendingAllowance);
            CoroutineUtils.StopUtilCoroutine(ref m_waitAnalyticsEventsCollectionNotAllowed);
            m_analyticsSettings = null;
            m_eventIndex = 0;
            m_isInitialised = false;
        }
        // 0x06000030: SaveAnalyticsEvents releases holders; manual cleanup retains its original behavior.
        private void StoreAnalyticsEvents()
        {
            List<AnalyticsEventDataHolder> events = SpawnAnalyticsEventDataHolderList();
            DequeueAnalyticsEventsFromQueue(ref events, m_analyticsEventDataHolderQueue.Count);
            SaveAnalyticsEvents(events, m_pendingEventsFilePath);
            foreach (KeyValuePair<int, List<AnalyticsEventDataHolder>> entry in m_analyticsEventDataHolderManualListPerCategory)
                SaveAnalyticsEvents(entry.Value, m_manualEventsFilePathPrefix + entry.Key.ToString() + m_manualEventsFilePathSuffix);
            ClearManualAnalyticsEventsCached(false);
            if (m_analyticsSettings.PersistentEventIndex) SaveToPersistentData("EventIndexKey", m_eventIndex.ToString());
        }
        // 0x06000031: original HTTP post result is ignored; no substitute transport.
        private void SendAnalyticsEvents(List<AnalyticsEventDataHolder> eventDataHolderList, bool shouldWriteBackToDisk, bool isPostInProgress = false)
        {
            if (!m_analyticsSettings.IsAnalyticsEventsSendingAllowed)
            {
                int count = eventDataHolderList.Count;
                for (int i = 0; i < count; ++i) InternalPushAnalyticsEventDataHolderToManualQueue(0, eventDataHolderList[i]);
                if (isPostInProgress) m_postInProgress = false;
                DespawnAnalyticsEventDataHolderList(eventDataHolderList);
                if (m_waitAnalyticsEventsSendingAllowance == null)
                    m_waitAnalyticsEventsSendingAllowance = CoroutineUtils.RunCoroutine(WaitAnalyticsEventsSendingAllowance());
                return;
            }
            if (m_sendData && m_analyticsSettings.ServerBaseURL != null)
                HTTPRequestBehaviour.post(m_analyticsSettings.ServerBaseURL, eventDataHolderList, AnalyticsServerCallback(eventDataHolderList, shouldWriteBackToDisk, isPostInProgress), 10f);
            else SaveAnalyticsEvents(eventDataHolderList, m_path);
        }
        // 0x06000032 / natural 0x06000059..5e: original null-yield wait, no finally cleanup.
        private IEnumerator WaitAnalyticsEventsSendingAllowance()
        {
            while (!m_analyticsSettings.IsAnalyticsEventsSendingAllowed) yield return null;
            InternalProcessManualQueue(0, false);
            m_waitAnalyticsEventsSendingAllowance = null;
        }
        // 0x06000033
        private void ResendFailedEventsFromDisk()
        {
            List<AnalyticsEventDataHolder> events;
            if (LoadAnalyticsEvents(out events, m_failedEventsFilePath) && events.Count > 0) SendAnalyticsEvents(events, false);
        }
        // 0x06000034: restores ownership before returning the temporary list to its pool.
        private void RestoreAnalyticsEvents()
        {
            List<AnalyticsEventDataHolder> events;
            if (LoadAnalyticsEvents(out events, m_pendingEventsFilePath))
                foreach (AnalyticsEventDataHolder holder in events) EnqueueAnalyticsEventToQueue(holder);
            DespawnAnalyticsEventDataHolderList(events);
            RestoreManualQueues();
            List<AnalyticsEventDataHolder> automaticEvents;
            m_analyticsEventDataHolderManualListPerCategory.TryGetValue(0, out automaticEvents);
            if (automaticEvents != null && m_waitAnalyticsEventsSendingAllowance == null && automaticEvents.Count > 0)
                m_waitAnalyticsEventsSendingAllowance = CoroutineUtils.RunCoroutine(WaitAnalyticsEventsSendingAllowance());
            if (m_analyticsSettings.PersistentEventIndex)
            {
                string stored;
                if (LoadFromPersistentData("EventIndexKey", out stored))
                {
                    int eventIndex;
                    if (int.TryParse(stored, out eventIndex)) m_eventIndex = Math.Max(m_eventIndex, eventIndex);
                    else HLOutput.LogError("Unable to load EventIndexKey from file");
                }
            }
        }
        // 0x06000035: original broad catch logs after the foreach enumerator is disposed.
        private void RestoreManualQueues()
        {
            try
            {
                string[] files = Directory.GetFiles(Application.persistentDataPath, m_manualEventsFilePathPattern);
                for (int i = 0; i < files.Length; ++i)
                {
                    string path = files[i];
                    string[] parts = Path.GetFileNameWithoutExtension(path).Split('_');
                    string categoryText = parts[parts.Length - 1];
                    int category;
                    if (int.TryParse(categoryText, out category))
                    {
                        List<AnalyticsEventDataHolder> events;
                        if (LoadAnalyticsEvents(out events, path))
                            foreach (AnalyticsEventDataHolder holder in events) InternalPushAnalyticsEventDataHolderToManualQueue(category, holder);
                        DespawnAnalyticsEventDataHolderList(events);
                    }
                    else HLOutput.LogError("Category '" + categoryText + "' of saved Manual Analytics Event Queue is not a valid integer.");
                }
            }
            catch (Exception exception) { HLOutput.LogError(string.Format("Failed to restore the manual queues with exception: {0}.", exception)); }
        }
        // 0x06000036
        private void EnqueueAnalyticsEventToQueue(AnalyticsEventDataHolder analyticEvent) { m_analyticsEventDataHolderQueue.Enqueue(analyticEvent); }
        // 0x06000037: ref is not assigned; original zero count uses MaxQueueLength.
        private void DequeueAnalyticsEventsFromQueue(ref List<AnalyticsEventDataHolder> dequeuedEvents, int numEvents = 0)
        {
            int count = Math.Min(numEvents > 0 ? numEvents : m_analyticsSettings.MaxQueueLength, m_analyticsEventDataHolderQueue.Count);
            for (int i = 0; i < count; ++i) dequeuedEvents.Add(m_analyticsEventDataHolderQueue.Dequeue());
        }
        // 0x06000038: collection-monitor coroutine is deliberately not stopped here.
        public void ClearAnalyticsEventsCached()
        {
            while (m_analyticsEventDataHolderQueue.Count > 0) m_analyticsEventDataHolderQueue.Dequeue().Release();
            m_analyticsEventDataHolderQueue.Clear();
            ClearManualAnalyticsEventsCached();
            StopAllLateDataCoroutines();
            CoroutineUtils.StopUtilCoroutine(ref m_waitAnalyticsEventsSendingAllowance);
        }
        // 0x06000039: snapshots keys before deleting entries.
        private void ClearManualAnalyticsEventsCached(bool despawnEventHolders = true)
        {
            foreach (int category in new List<int>(m_analyticsEventDataHolderManualListPerCategory.Keys))
                InternalClearManualAnalyticsEventsCachedForCategory(category, despawnEventHolders);
            m_analyticsEventDataHolderManualListPerCategory.Clear();
        }
        // 0x0600003a: holder release is unconditional; flag controls list pool return only.
        private void InternalClearManualAnalyticsEventsCachedForCategory(int category, bool despawnEventHolders = true)
        {
            List<AnalyticsEventDataHolder> events = m_analyticsEventDataHolderManualListPerCategory[category];
            foreach (AnalyticsEventDataHolder holder in events) holder.Release();
            events.Clear();
            if (despawnEventHolders) DespawnAnalyticsEventDataHolderList(events);
            m_analyticsEventDataHolderManualListPerCategory.Remove(category);
        }
        // 0x0600003b / natural 0x06000051..52: responseData is originally unused.
        private HTTPRequestBehaviour.JSONResponseCallback AnalyticsServerCallback(List<AnalyticsEventDataHolder> eventDataHolderList, bool shouldWriteFailedEvents, bool isPostInProgress)
        {
            return (status, responseData) =>
            {
                _ = this;
                if (status == HTTPRequestBehaviour.Status.Success)
                {
                    int count = eventDataHolderList.Count;
                    for (int i = 0; i < count; ++i)
                    {
                        AnalyticsEventDataHolder holder = eventDataHolderList[i];
                        if (holder != null) holder.Release();
                    }
                }
                else
                {
                    List<AnalyticsEventDataHolder> failedEvents = SpawnAnalyticsEventDataHolderList();
                    foreach (AnalyticsEventDataHolder holder in eventDataHolderList)
                    {
                        if (holder == null || !holder.CheckIsValid()) continue;
                        holder.IncreaseEventAttempts();
                        if (holder.EventAttempts > m_analyticsSettings.MaxSendAttempts) failedEvents.Add(holder);
                        else EnqueueAnalyticsEventToQueue(holder);
                    }
                    if (failedEvents.Count > 0 && shouldWriteFailedEvents) SaveAnalyticsEvents(failedEvents, m_failedEventsFilePath);
                    else DespawnAnalyticsEventDataHolderList(failedEvents, true);
                }
                DespawnEventDataHolderListAndTryResetPostInProgress(eventDataHolderList, isPostInProgress);
                TryPumpEventQueue();
            };
        }
        // 0x0600003c
        private void DespawnEventDataHolderListAndTryResetPostInProgress(List<AnalyticsEventDataHolder> eventDataHolderList, bool isPostInProgress)
        {
            if (isPostInProgress) m_postInProgress = false;
            DespawnAnalyticsEventDataHolderList(eventDataHolderList);
        }
        // 0x0600003d: original force can send an empty queue; post flag follows m_sendData.
        private void TryPumpEventQueue(bool force = false)
        {
            if (m_postInProgress) return;
            if (m_analyticsEventDataHolderQueue.Count < m_analyticsSettings.MaxQueueLength && !force) return;
            m_postInProgress = m_sendData;
            List<AnalyticsEventDataHolder> events = SpawnAnalyticsEventDataHolderList();
            DequeueAnalyticsEventsFromQueue(ref events);
            SendAnalyticsEvents(events, true, true);
        }
        // 0x0600003e: both native bodies preserve the unescaped original JSON format.
        private void SaveAnalyticsEvents(List<AnalyticsEventDataHolder> eventsToWrite, string path)
        {
            m_stringListCache.Clear();
            foreach (AnalyticsEventDataHolder holder in eventsToWrite)
            {
                if (holder == null || !holder.CheckIsValid()) continue;
                string data = JSONSerializer.Encode(holder, BindingFlags.Instance | BindingFlags.Public, JSONSerializer.EncodeOptions.None);
                m_stringListCache.Add(string.Format("{{\"data\":{0}, \"event_attempts\":{1}}}", data, holder.EventAttempts));
            }
            if (m_stringListCache.Count > 0) WriteToDisk(path, m_stringListCache);
            DespawnAnalyticsEventDataHolderList(eventsToWrite, true);
        }
        // 0x0600003f: original read deletes source; Clone precedes attempts read and releases.
        private bool LoadAnalyticsEvents(out List<AnalyticsEventDataHolder> eventDataHolderList, string path)
        {
            bool loaded = ReadFromDisk(path, m_stringListCache, true);
            if (!loaded) { eventDataHolderList = null; return false; }
            eventDataHolderList = SpawnAnalyticsEventDataHolderList();
            foreach (string line in m_stringListCache)
            {
                if (string.IsNullOrEmpty(line)) continue;
                JSONHashtable table = JSONSerializer.Decode<JSONHashtable>(line, BindingFlags.Instance | BindingFlags.Public);
                JSONHashtable dataObject = table == null ? null : table.GetObject("data");
                if (dataObject == null)
                {
                    HLOutput.LogError("AnalyticsSystem::LoadAnalyticsEvents - Unable to decode event from string cache: " + line);
                    continue;
                }
                IJsonObject data = dataObject.Clone();
                int attempts = table.GetInt("event_attempts");
                AnalyticsEventDataHolder holder = AnalyticsEventDataHolder.Create((JSONHashtable)data, attempts);
                if (holder.CheckIsValid()) eventDataHolderList.Add(holder); else holder.Release();
                table.Release();
            }
            return loaded;
        }
        // 0x06000040: original nonmatching decoded objects are not released here.
        private void SaveToPersistentData(string key, string value)
        {
            if (ReadFromDisk(m_persistentDataFilePath, m_stringListCache))
            {
                bool replaced = false;
                for (int i = 0; i < m_stringListCache.Count; ++i)
                {
                    if (string.IsNullOrEmpty(m_stringListCache[i])) continue;
                    JSONHashtable table = JSONSerializer.Decode<JSONHashtable>(m_stringListCache[i], BindingFlags.Instance | BindingFlags.Public);
                    if (table == null || !table.ContainsKey(key)) continue;
                    m_stringListCache[i] = string.Format("{{\"{0}\":\"{1}\"}}", key, value);
                    table.Release();
                    replaced = true;
                }
                if (!replaced) m_stringListCache.Add(string.Format("{{\"{0}\":\"{1}\"}}", key, value));
                WriteToDisk(m_persistentDataFilePath, m_stringListCache, true);
            }
            else
            {
                m_stringListCache.Add(string.Format("{{\"{0}\":\"{1}\"}}", key, value));
                WriteToDisk(m_persistentDataFilePath, m_stringListCache.ToArray());
            }
        }
        // 0x06000041: a readable file with no matching key retains result=true/value=empty.
        private bool LoadFromPersistentData(string key, out string value)
        {
            if (!ReadFromDisk(m_persistentDataFilePath, m_stringListCache)) { value = string.Empty; return false; }
            string stored = string.Empty;
            bool result = true;
            foreach (string line in m_stringListCache)
            {
                if (string.IsNullOrEmpty(line)) continue;
                JSONHashtable table = JSONSerializer.Decode<JSONHashtable>(line, BindingFlags.Instance | BindingFlags.Public);
                if (table == null || !table.ContainsKey(key)) continue;
                result = table.TryGetString(key, out stored);
                table.Release();
            }
            value = stored;
            return result;
        }
        // 0x06000042: file reset precedes append; original exception is logged after disposal.
        private void WriteToDisk(string filePath, IList<string> lines, bool overwrite = false)
        {
            try
            {
                bool populated = File.Exists(filePath) && new FileInfo(filePath).Length > 0;
                if (overwrite || !populated) File.Create(filePath).Close();
                using (StreamWriter writer = File.AppendText(filePath))
                    foreach (string line in lines) writer.WriteLine(line);
            }
            catch (Exception exception) { HLOutput.LogError(exception); }
        }
        // 0x06000043: Clear and Exists are outside the read catch; deletion runs in finally.
        public static bool ReadFromDisk(string filePath, IList<string> lines, bool deleteSource = false)
        {
            lines.Clear();
            if (!File.Exists(filePath)) return false;
            try
            {
                using (StreamReader reader = new StreamReader(filePath, true))
                    while (!reader.EndOfStream) lines.Add(reader.ReadLine());
                return true;
            }
            catch (Exception exception) { HLOutput.LogError(exception); return false; }
            finally { if (deleteSource) File.Delete(filePath); }
        }
        // 0x06000044: original pool capacity ten; every spawned list is cleared.
        private List<AnalyticsEventDataHolder> SpawnAnalyticsEventDataHolderList()
        {
            if (!ObjectPool<List<AnalyticsEventDataHolder>>.IsInitialised()) ObjectPool<List<AnalyticsEventDataHolder>>.InitialisePool(10);
            List<AnalyticsEventDataHolder> events = ObjectPool<List<AnalyticsEventDataHolder>>.Spawn();
            events.Clear();
            return events;
        }
        // 0x06000045: null-list guard is original; holders have no added null guards.
        private void DespawnAnalyticsEventDataHolderList(List<AnalyticsEventDataHolder> analyticsEventDataHolderList, bool releaseContents = false)
        {
            if (analyticsEventDataHolderList == null) return;
            if (releaseContents) foreach (AnalyticsEventDataHolder holder in analyticsEventDataHolderList) holder.Release();
            analyticsEventDataHolderList.Clear();
            ObjectPool<List<AnalyticsEventDataHolder>>.Despawn(analyticsEventDataHolderList);
        }
        // 0x06000046: genuine optional assert parameter is unused in both shipped bodies.
        private bool IsInitialised(bool assert = true) => m_isInitialised;
        // 0x06000047: collection monitor begins while collection is allowed; false releases first.
        private bool CanAnalyticsEventsBeCollected(IAnalyticsEvent analyticsEvent)
        {
            bool allowed = m_analyticsSettings.IsAnalyticsEventsCollectionAllowed;
            if (allowed)
            {
                if (m_waitAnalyticsEventsCollectionNotAllowed == null)
                    m_waitAnalyticsEventsCollectionNotAllowed = CoroutineUtils.RunCoroutine(WaitAnalyticsEventsCollectionNotAllowed());
            }
            else { analyticsEvent.ReleaseEvent(); ClearAnalyticsEventsCached(); }
            return allowed;
        }
        // 0x06000048 / natural 0x06000053..58: original monitor clears after allowance ends.
        private IEnumerator WaitAnalyticsEventsCollectionNotAllowed()
        {
            while (m_analyticsSettings.IsAnalyticsEventsCollectionAllowed) yield return null;
            ClearAnalyticsEventsCached();
            m_waitAnalyticsEventsCollectionNotAllowed = null;
        }
        // 0x06000049: original readiness callback executes before immediate manual enqueue.
        private bool CanPushAnalyticsEventToManualQueue(IAnalyticsEvent analyticsEvent, int category)
        {
            if (analyticsEvent == null || !CanAnalyticsEventsBeCollected(analyticsEvent)) return false;
            if (analyticsEvent.IsLateDataReady) { analyticsEvent.SetLateData(); return true; }
            int index = GetNextCoroutineArrayIndex();
            StartLateDataCoroutine(WaitLateDataBeforePushingAnalyticsEventToManualQueue(analyticsEvent, category, index), index);
            return false;
        }
        // 0x0600004a / natural 0x0600005f..64: genuine interface-only generic constraint.
        // The first yield is unconditional. Completion has no finally cleanup on fault/disposal.
        private IEnumerator WaitLateDataBeforePushingAnalyticsEventToManualQueue<T>(T analyticsEvent, int category, int arrayIndex) where T : IAnalyticsEvent
        {
            do { yield return null; } while (!analyticsEvent.IsLateDataReady);
            analyticsEvent.SetLateData();
            InternalPushAnalyticsEventToManualQueue(analyticsEvent, category);
            CleanupCoroutineArray(arrayIndex);
        }
        // 0x0600004b: repeated original dictionary lookup preserves its exact null/count checks.
        private bool CanProcessManualQueue(int category)
        {
            return m_analyticsEventDataHolderManualListPerCategory.Count != 0
                && m_analyticsEventDataHolderManualListPerCategory.ContainsKey(category)
                && m_analyticsEventDataHolderManualListPerCategory[category] != null
                && m_analyticsEventDataHolderManualListPerCategory[category].Count != 0;
        }
        // 0x0600004c: original eviction occurs before RunCoroutine and tracking-list append.
        private void StartLateDataCoroutine(IEnumerator routine, int index)
        {
            StopLateDataCoroutine(index);
            m_activeCoroutines[index] = CoroutineUtils.RunCoroutine(routine);
            m_activeCoroutineIndexes.Add(index);
        }
        // 0x0600004d: first empty slot, otherwise oldest tracked index; no invented resize.
        private int GetNextCoroutineArrayIndex()
        {
            if (m_activeCoroutineIndexes.Count == 0) return 0;
            for (int i = 0; i < m_activeCoroutines.Length; ++i) if (m_activeCoroutines[i] == null) return i;
            return m_activeCoroutineIndexes[0];
        }
        // 0x0600004e
        private void StopLateDataCoroutine(int index)
        {
            if (m_activeCoroutines[index] != null)
            {
                CoroutineUtils.StopUtilCoroutine(ref m_activeCoroutines[index]);
                m_activeCoroutineIndexes.Remove(index);
            }
        }
        // 0x0600004f
        private void CleanupCoroutineArray(int index)
        {
            m_activeCoroutines[index] = null;
            m_activeCoroutineIndexes.Remove(index);
        }
        // 0x06000050
        private void StopAllLateDataCoroutines()
        {
            for (int i = 0; i < m_activeCoroutines.Length; ++i) StopLateDataCoroutine(i);
        }
    }
}
