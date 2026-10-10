using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime 0x02000023, with the genuine HLAnalytics settings contract.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AnalyticsSettings : IAnalyticsSettings
    {
        private static Analytics_SystemConfig s_systemConfig_Internal;
        private readonly SystemRef<AnalyticsConsentManager> m_analyticsConsentManagerRef;

        // Original 0x060000af: Unity object equality determines whether to refresh the cache.
        private static Analytics_SystemConfig s_systemConfig
        {
            get
            {
                if (s_systemConfig_Internal == null)
                    s_systemConfig_Internal = SystemConfiguration.GetConfig<Analytics_SystemConfig>();
                return s_systemConfig_Internal;
            }
        }

        // Original 0x060000b0..b5, in original property order.
        public string GameID => s_systemConfig.GameID;
        public string Environment => s_systemConfig.Environment;
        public Uri ServerBaseURL { get; } = new Uri(s_systemConfig.ServerBaseURL);
        public int MaxQueueLength => s_systemConfig.MaxQueueLength;
        public int MaxSendAttempts => s_systemConfig.MaxSendAttempts;
        public bool PersistentEventIndex => s_systemConfig.PersistentEventIndex;

        // Original 0x060000b6/0x060000b7.
        public bool IsAnalyticsEventsSendingAllowed => IsAnalyticsSendingAllowed();
        public bool IsAnalyticsEventsCollectionAllowed => IsAnalyticsCollectionAllowed();

        // Original 0x060000b8: the URI initializer runs before the base constructor.
        public AnalyticsSettings()
        {
            m_analyticsConsentManagerRef = ProcessManager.GetSystemRef<AnalyticsConsentManager>();
        }

        // Original 0x060000b9 accepts only the signed Int32 category 1.
        public bool IsAnalyticsEventToManualQueueCollectionEnabled(int category) => category == 1;

        // Original 0x060000ba.
        private bool IsAnalyticsSendingAllowed() =>
            m_analyticsConsentManagerRef.Get().ConsentState == AnalyticsConsentState.Consented;

        // Original 0x060000bb: one consent-state read serves both comparisons.
        private bool IsAnalyticsCollectionAllowed()
        {
            AnalyticsConsentState consentState = m_analyticsConsentManagerRef.Get().ConsentState;
            return consentState == AnalyticsConsentState.None || consentState == AnalyticsConsentState.Consented;
        }
    }
}
