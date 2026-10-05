using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TimeManager : MonoBehaviour, ISystem
    {
        [Tooltip("The time categories to control game speed."), SerializeField]
        private List<TimeCategoryObject> m_gameSpeedTimeCategories = new List<TimeCategoryObject>();
        [Tooltip("The minimum allowed game time setting override."), SerializeField]
        private float m_gameSpeedMin = 0.2f;
        [Tooltip("The maximum allowed game time setting override."), SerializeField]
        private float m_gameSpeedMax = 1f;
        public const float TimeScaleMinDelta = 0.001f;
        private const int SettingId = 0;
        private readonly StackableData m_timeSettings = new StackableData();
        private readonly Dictionary<UpdateOn, Dictionary<TimeCategoryObject, List<TimeScaledSubscription>>> m_updateObjects =
            new Dictionary<UpdateOn, Dictionary<TimeCategoryObject, List<TimeScaledSubscription>>>(EnumComparers.UpdateOnComparer);
        private readonly List<TimeCategoryObject> m_categoryPriority = new List<TimeCategoryObject>();
        private readonly Dictionary<TimeCategoryObject, float> m_totalTimeLookup = new Dictionary<TimeCategoryObject, float>();
        private readonly Dictionary<TimeCategoryObject, float> m_totalFixedTimeLookup = new Dictionary<TimeCategoryObject, float>();
        private readonly Dictionary<TimeCategoryObject, float> m_totalLateUpdateTimeLookup = new Dictionary<TimeCategoryObject, float>();
        private readonly List<TimeScaledSubscription> m_pendingSubscriptions = new List<TimeScaledSubscription>();
        private readonly Dictionary<TimeCategoryObject, ProfilerMarker> m_categoryProfilerMarkers = new Dictionary<TimeCategoryObject, ProfilerMarker>();
        private float m_overrideUnityTimescaleValue = 1f;
        private TimeCategoryObject m_currentUpdateCategory;
        private UpdateOn m_currentUpdateType;
        private float m_fixedTimeSetting;
        private TimeCategoryObject m_unityTimeCategory;
        private TimeSetting m_gameSpeedSetting;
        private StackableDataHandle m_gameSpeedOverrideHandle;
        private float m_overrideSpeed;
        private static TimeSetting s_cachedTimeSettingResult;
        private TimeCategoryConfiguration m_timeCategoryConfiguration;

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class TimeScaledSubscription
        {
            public ITimeScaled TimeScaledObject;
            public bool IsSubscribed;
            public ProfilerMarker ProfilerMarker;
            public string TransformPath;
            // Original 06000ea4 / ARM64 1b1d30c; no field initialization.
            public TimeScaledSubscription() { }
        }

        // Original 06000e82..87 / ARM64 1b1ae28..50.
        public float GameSpeedMin => m_gameSpeedMin;
        public float GameSpeedMax => m_gameSpeedMax;
        public bool GameSpeedApplied { get; set; } = true;
        public bool UnityTimescaleOverride { get; private set; }

        // 06000e88 / 1b1ae58: default settings and phase dictionaries precede Reset
        // and registration. Category additions use the real AddUnique boundary.
        private void Awake()
        {
            m_timeCategoryConfiguration = SystemConfiguration.GetConfig<TimeCategoryConfiguration>();
            m_unityTimeCategory = m_timeCategoryConfiguration.UnityGlobalTime;
            m_timeCategoryConfiguration.AddCategory(m_unityTimeCategory);
            s_cachedTimeSettingResult = TimeSetting.GetDefault();
            StackableData.RegisterTypeOperations<TimeSetting>(multiply: TimeSettingMultiply);
            m_timeSettings.SetBaseValue(SettingId, TimeSetting.GetDefault(), StackableData.RetrievalOperation.Multiply);
            m_updateObjects.Add(UpdateOn.FixedUpdate, new Dictionary<TimeCategoryObject, List<TimeScaledSubscription>>());
            m_updateObjects.Add(UpdateOn.LateUpdate, new Dictionary<TimeCategoryObject, List<TimeScaledSubscription>>());
            m_updateObjects.Add(UpdateOn.Update, new Dictionary<TimeCategoryObject, List<TimeScaledSubscription>>());
            foreach (TimeCategoryObject category in m_timeCategoryConfiguration.PrioritisedCategories)
                m_categoryProfilerMarkers[category] = new ProfilerMarker(category.name);
            m_fixedTimeSetting = Time.fixedDeltaTime;
            Reset();
            ProcessManager.RegisterSystem(this);
        }

        // 06000e89 / 1b1b7a8 retains old setting keys, overrides, subscriptions,
        // and total-time entries outside the new priority list.
        public void Reset()
        {
            m_categoryPriority.Clear();
            m_categoryPriority.AddRange(m_timeCategoryConfiguration.PrioritisedCategories);
            Time.timeScale = m_timeCategoryConfiguration.DefaultTimescale;
            ResetTotalTimes();
            TimeSetting defaults = TimeSetting.GetDefault();
            TimeSetting current = m_timeSettings.Get<TimeSetting>(SettingId);
            foreach (KeyValuePair<TimeCategoryObject, float> entry in defaults.OverrideDictionaryLookup)
                current.SetCategory(entry.Key, entry.Value);
        }

        // 06000e8a / 1b1bd7c restores only timeScale, removes the speed handle,
        // removes one global category, and then unregisters the original system.
        private void OnDestroy()
        {
            Time.timeScale = 1f;
            RemoveGameSpeedOverride();
            m_timeCategoryConfiguration.RemoveCategory(m_timeCategoryConfiguration.UnityGlobalTime);
            ProcessManager.UnregisterSystem(this);
        }

        // 06000e8b..8d / 1b1bebc, 1b1c5c0, 1b1c6e4. The original phase lambdas
        // generate <>c callbacks; LateUpdate specifically uses unscaledDeltaTime.
        public void FixedUpdate()
        { PerformUpdate(UpdateOn.FixedUpdate, Time.fixedDeltaTime, m_totalFixedTimeLookup, (timeScaledObject, deltaTime) => timeScaledObject.OnFixedUpdate(deltaTime)); }
        public void Update()
        { PerformUpdate(UpdateOn.Update, Time.deltaTime, m_totalTimeLookup, (timeScaledObject, deltaTime) => timeScaledObject.OnUpdate(deltaTime)); }
        public void LateUpdate()
        { PerformUpdate(UpdateOn.LateUpdate, Time.unscaledDeltaTime, m_totalLateUpdateTimeLookup, (timeScaledObject, deltaTime) => timeScaledObject.OnLateUpdate(deltaTime)); }

        // 06000e8e / 1b1bfe0: category order is authored. State reset occurs only
        // on normal completion; a callback failure leaves the active category/type.
        private void PerformUpdate(UpdateOn updateOn, float deltaTime,
            Dictionary<TimeCategoryObject, float> totalTimeLookup, Action<ITimeScaled, float> updateAction)
        {
            TimeSetting setting = m_timeSettings.Get<TimeSetting>(SettingId);
            Time.timeScale = UnityTimescaleOverride ? m_overrideUnityTimescaleValue :
                setting.GetOverrideWithDefault(m_unityTimeCategory, 1f);
            m_currentUpdateType = updateOn;
            Dictionary<TimeCategoryObject, List<TimeScaledSubscription>> byCategory = m_updateObjects[updateOn];
            foreach (TimeCategoryObject category in m_categoryPriority)
            {
                if (!byCategory.TryGetValue(category, out List<TimeScaledSubscription> subscriptions)) continue;
                // Remove the object through List.Remove, not by index; duplicated
                // subscription references retain the original first-match behavior.
                for (int i = subscriptions.Count - 1; i >= 0; i--)
                {
                    TimeScaledSubscription subscription = subscriptions[i];
                    if (!subscription.IsSubscribed) subscriptions.Remove(subscription);
                }
                float scaledTime = setting.GetOverrideWithDefault(category, 1f) * deltaTime;
                totalTimeLookup[category] += scaledTime;
                using (m_categoryProfilerMarkers[category].Auto())
                {
                    m_currentUpdateCategory = category;
                    UpdateCategory(subscriptions, scaledTime, updateAction);
                    m_currentUpdateCategory = null;
                }
            }
            m_currentUpdateType = (UpdateOn)0;
            if (updateOn == UpdateOn.FixedUpdate)
            {
                if (Mathf.Approximately(setting.GetOverrideWithDefault(m_unityTimeCategory, 1f), 0f))
                    Time.fixedDeltaTime = m_fixedTimeSetting;
                else
                    Time.fixedDeltaTime = setting.GetOverrideWithDefault(m_unityTimeCategory, 1f) * m_fixedTimeSetting;
            }
        }

        // 06000e8f / 1b1c874. Exact zero pauses, any other value updates. The
        // pause flag changes before its callback; pending entries drain afterward
        // and remain pending if a callback throws. Existing lists stay live.
        private void UpdateCategory(List<TimeScaledSubscription> updatedByCategory, float scaledTime,
            Action<ITimeScaled, float> updateAction)
        {
            foreach (TimeScaledSubscription subscription in updatedByCategory)
            {
                if (!subscription.IsSubscribed) continue;
                ITimeScaled tickedObject = subscription.TimeScaledObject;
                if (tickedObject == null)
                {
                    HLOutput.LogError("Timescaled object was deleted without proper unsubscription, path: " + subscription.TransformPath);
                    subscription.IsSubscribed = false;
                    continue;
                }
                if (scaledTime != 0f)
                {
                    ProfilerMarker marker = subscription.ProfilerMarker;
                    if (tickedObject.IsPaused)
                    {
                        tickedObject.IsPaused = false;
                        tickedObject.OnResume();
                    }
                    using (marker.Auto()) updateAction(tickedObject, scaledTime);
                }
                else if (!tickedObject.IsPaused)
                {
                    tickedObject.IsPaused = true;
                    tickedObject.OnPause();
                }
            }
            foreach (TimeScaledSubscription subscription in m_pendingSubscriptions)
                updatedByCategory.Add(subscription);
            m_pendingSubscriptions.Clear();
        }

        // 06000e90 / 1b1cf64. Only the existing category list participates in
        // duplicate detection; pending subscriptions are not searched.
        public void Subscribe(ITimeScaled tickedObject, TimeCategoryObject category,
            UpdateOn updateOn = UpdateOn.Update, string path = "")
        {
            if (updateOn == UpdateOn.None) return;
            Dictionary<TimeCategoryObject, List<TimeScaledSubscription>> byCategory = m_updateObjects[updateOn];
            if (!byCategory.ContainsKey(category)) byCategory[category] = new List<TimeScaledSubscription>();
            List<TimeScaledSubscription> subscriptions = byCategory[category];
            foreach (TimeScaledSubscription subscription in subscriptions)
            {
                if (ReferenceEquals(subscription.TimeScaledObject, tickedObject))
                {
                    subscription.IsSubscribed = true;
                    return;
                }
            }
            TimeScaledSubscription newSubscription = new TimeScaledSubscription
            {
                TimeScaledObject = tickedObject,
                IsSubscribed = true,
                ProfilerMarker = new ProfilerMarker(tickedObject.ToString()),
                TransformPath = path
            };
            if (m_currentUpdateCategory == category && m_currentUpdateType == updateOn)
                m_pendingSubscriptions.Add(newSubscription);
            else subscriptions.Add(newSubscription);
        }

        // 06000e91 / 1b1d314 marks only the first existing reference match.
        public void Unsubscribe(ITimeScaled tickedObject, TimeCategoryObject category, UpdateOn updateOn = UpdateOn.Update)
        {
            if (updateOn == UpdateOn.None) return;
            if (!m_updateObjects[updateOn].TryGetValue(category, out List<TimeScaledSubscription> subscriptions)) return;
            foreach (TimeScaledSubscription subscription in subscriptions)
            {
                if (ReferenceEquals(subscription.TimeScaledObject, tickedObject))
                {
                    subscription.IsSubscribed = false;
                    break;
                }
            }
        }

        // 06000e92 / 1b1bb48 updates only priority keys, without clearing lookups.
        private void ResetTotalTimes()
        {
            m_timeCategoryConfiguration.AddCategory(m_timeCategoryConfiguration.UnityGlobalTime);
            foreach (TimeCategoryObject category in m_categoryPriority)
            {
                m_totalTimeLookup[category] = 0f;
                m_totalFixedTimeLookup[category] = 0f;
                m_totalLateUpdateTimeLookup[category] = 0f;
            }
        }

        // 06000e93..97 / 1b1d4b4..694 retain direct indexer failure for absent
        // total-time and timescale keys; only category update uses a default.
        public float GetTotalTime(TimeCategoryObject category) { return m_totalTimeLookup[category]; }
        public float GetTotalFixedTime(TimeCategoryObject category) { return m_totalFixedTimeLookup[category]; }
        public float GetDeltaTime(TimeCategoryObject category) { return Time.deltaTime * GetTimescale(category); }
        public float GetFixedDeltaTime(TimeCategoryObject category) { return Time.fixedDeltaTime * GetTimescale(category); }
        public float GetTimescale(TimeCategoryObject category) { return m_timeSettings.Get<TimeSetting>(SettingId).OverrideDictionaryLookup[category]; }
        // 06000e98..9a / 1b1d6d8..7b4: only handle update/removal guard null.
        public StackableDataHandle ApplyTimeSetting(TimeSetting setting) { return m_timeSettings.AddOverride(SettingId, setting); }
        public void UpdateTimeSetting(StackableDataHandle handle, TimeSetting setting)
        { if (handle != null) m_timeSettings.AddOverride(handle, SettingId, setting); }
        public void RemoveTimeSetting(StackableDataHandle handle)
        { if (handle != null) m_timeSettings.RemoveOverrides(handle); }

        // 06000e9b / 1b1d7c8 shares one cached result across all stacks/managers.
        private static StackableData.OperationAction TimeSettingMultiply(
            StackableData.StackableDataContainer<TimeSetting> stackableDataContainer,
            ref StackableData.ResultCarrier<TimeSetting> result)
        {
            if (!result.HasValue)
            {
                s_cachedTimeSettingResult.Copy(stackableDataContainer.Value);
                result.SetValue(s_cachedTimeSettingResult);
            }
            else
            {
                TimeSetting current = result.Value;
                foreach (KeyValuePair<TimeCategoryObject, float> entry in stackableDataContainer.Value.OverrideDictionaryLookup)
                    current.SetCategory(entry.Key, entry.Value * current.GetOverrideWithDefault(entry.Key, 1f));
                result.SetValue(current);
            }
            return StackableData.OperationAction.Continue;
        }

        // 06000e9c / 1b1dedc creates a new override without removing any old one.
        public void InitialiseGameSpeedOverride()
        {
            m_gameSpeedSetting = TimeSetting.GetDefault();
            m_gameSpeedOverrideHandle = m_timeSettings.AddOverride(SettingId, m_gameSpeedSetting);
        }
        // 06000e9d / 1b1df68 stores without clamping or applying immediately.
        public void UpdateGameSpeedOverride(float overrideSpeed) { m_overrideSpeed = overrideSpeed; }
        // 06000e9e / 1b1df70 selects max when disabled; the min field is not used.
        public void UpdateGameSpeedOverride()
        {
            float speed = GameSpeedApplied ? m_overrideSpeed : m_gameSpeedMax;
            foreach (TimeCategoryObject category in m_gameSpeedTimeCategories)
                m_gameSpeedSetting.SetCategory(category, speed);
            UpdateTimeSetting(m_gameSpeedOverrideHandle, m_gameSpeedSetting);
        }
        // 06000e9f / 1b1be70 clears the handle only after successful removal.
        private void RemoveGameSpeedOverride()
        {
            if (m_gameSpeedOverrideHandle == null) return;
            m_timeSettings.RemoveOverrides(m_gameSpeedOverrideHandle);
            m_gameSpeedOverrideHandle = null;
        }
        // 06000ea0 / 1b1e148: a NaN passes the original less-than guard.
        public void OverrideUnityTimescale(float timescale, bool debugOverride = false)
        {
            if (timescale < TimeScaleMinDelta && !debugOverride) return;
            UnityTimescaleOverride = true;
            m_overrideUnityTimescaleValue = timescale;
        }
        public float GetUnityTimescale() { return m_overrideUnityTimescaleValue; }
        // 06000ea2 / 1b1e178 leaves the stored override value intact.
        public void ClearUnityTimescaleOverride() { UnityTimescaleOverride = false; }
        // 06000ea3 / 1b1e180 uses declaration-order initializers before MonoBehaviour.
        public TimeManager() { }
    }
}
