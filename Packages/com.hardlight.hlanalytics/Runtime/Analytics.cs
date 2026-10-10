using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original HLAnalytics.Runtime 0x02000002; complete eleven declared methods.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class Analytics
    {
        private static readonly IAnalytics s_analytics;

        // 0x06000001: original interface slot 0, without an extra null guard.
        public static bool IsSupported => s_analytics.IsSupported;
        // 0x06000002: the original provider remains in the explicit research branch.
        // Intentional port selection: normal desktop builds use the offline adapter.
        static Analytics()
        {
#if PROJECT_LUCID_ORIGINAL_ANALYTICS
            s_analytics = new AnalyticsSystem();
#else
            s_analytics = new ProjectLucid.Offline.LocalAnalytics();
#endif
        }
        // 0x06000003
        public static void Initialise(IAnalyticsSettings analyticsSettings) => s_analytics.Initialise(analyticsSettings);
        // 0x06000004
        public static void ResetEventIndex() => s_analytics.ResetEventIndex();
        // 0x06000005
        public static void ProcessManualQueue(int category) => s_analytics.ProcessManualQueue(category);
        // 0x06000006
        public static void PushAnalyticsEventToManualQueue(IAnalyticsEvent analyticsEvent, int category) => s_analytics.PushAnalyticsEventToManualQueue(analyticsEvent, category);
        // 0x06000007
        public static IAnalyticsSettings GetAnalyticsSettings() => s_analytics.GetAnalyticsSettings();
        // 0x06000008
        public static int GetEventIndex() => s_analytics.GetEventIndex();
        // 0x06000009
        public static void ApplicationPause(bool paused) => s_analytics.ApplicationPause(paused);
        // 0x0600000a
        public static void Shutdown() => s_analytics.Shutdown();
        // 0x0600000b
        public static void ClearAnalyticsEventsCached() => s_analytics.ClearAnalyticsEventsCached();
    }
}
