using System;
using Hardlight.JSON;
using Hardlight.Pooling;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original HLAnalytics.Runtime 0x02000003. One abstract and eleven native APIs.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class AnalyticsEvent<T> : IAnalyticsEvent where T : AnalyticsEvent<T>, new()
    {
        // 0x04000002; original 0x06000017 initializer retains BeforeFieldInit intent.
        private static readonly T s_instanceStub = Analytics.IsSupported ? null : new T();
        private Func<bool> m_isLateDataReadyFunc;
        private Action<T> m_setLateDataAction;

        // 0x0600000c
        bool IAnalyticsEvent.IsLateDataReady => IsLateDataReady();
        // 0x0600000d
        void IAnalyticsEvent.SetLateData() { SetLateData(); }
        // 0x0600000e: callbacks are cleared before returning the event to its pool.
        void IAnalyticsEvent.ReleaseEvent()
        {
            m_isLateDataReadyFunc = null;
            m_setLateDataAction = null;
            if (Analytics.IsSupported) ObjectPool<T>.Despawn(this as T);
        }
        // 0x0600000f: FillData fault leaves the original allocated table unreleased.
        JSONHashtable IAnalyticsEvent.CreateData()
        {
            if (!Analytics.IsSupported) return null;
            JSONHashtable data = JSONHashtable.Create();
            FillData(data);
            return data;
        }
        // 0x06000010: genuine optional null defaults and shared unsupported instance.
        public static T Create(Func<bool> isLateDataReady = null, Action<T> setLateData = null)
        {
            T analyticsEvent = Analytics.IsSupported ? Spawn() : s_instanceStub;
            analyticsEvent.m_isLateDataReadyFunc = isLateDataReady;
            analyticsEvent.m_setLateDataAction = setLateData;
            return analyticsEvent;
        }
        // 0x06000011: both original native bodies return immediately.
        protected virtual void Initialise() { }
        // 0x06000012
        protected virtual bool IsLateDataReady() => m_isLateDataReadyFunc == null || m_isLateDataReadyFunc();
        // 0x06000013
        protected virtual void SetLateData()
        {
            if (m_setLateDataAction != null) m_setLateDataAction(this as T);
        }
        // 0x06000014: true original abstract contract, no native body.
        protected abstract void FillData(JSONHashtable data);
        // 0x06000015: original capacity five; Initialise follows the pool spawn.
        private static T Spawn()
        {
            if (!ObjectPool<T>.IsInitialised()) ObjectPool<T>.InitialisePool(5);
            T analyticsEvent = ObjectPool<T>.Spawn();
            analyticsEvent.Initialise();
            return analyticsEvent;
        }
        // 0x06000016
        protected AnalyticsEvent() { }
    }
}
