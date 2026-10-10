using Hardlight.Analytics;

namespace ProjectLucid.Offline
{
    // Intentional offline port adapter at the original IAnalytics boundary.
    // IsSupported=false selects the original AnalyticsEvent<T> unsupported
    // instance and prevents event serialization and pool allocation there.
    // This adapter shares HLAnalytics.Runtime to avoid a circular dependency
    // between its original facade and an assembly implementing its interface.
    public sealed class LocalAnalytics : IAnalytics
    {
        private IAnalyticsSettings m_settings;

        public bool IsSupported => false;

        // Keep the caller's settings identity without evaluating its original
        // configuration, consent, server, or account-dependent properties.
        public void Initialise(IAnalyticsSettings analyticsSettings)
        {
            m_settings = analyticsSettings;
        }

        public IAnalyticsSettings GetAnalyticsSettings() => m_settings;
        public int GetEventIndex() => -1;
        public void ResetEventIndex() { }
        public void ProcessManualQueue(int category) { }
        public void ApplicationPause(bool paused) { }
        public void ClearAnalyticsEventsCached() { }
        public void Shutdown() { m_settings = null; }

        // The original facade transfers ownership of an event. Discard it
        // synchronously through its own release contract, which clears the
        // original late-data callbacks. Never request or serialize event data.
        public void PushAnalyticsEventToManualQueue(IAnalyticsEvent analyticsEvent, int category)
        {
            analyticsEvent?.ReleaseEvent();
        }
    }
}
