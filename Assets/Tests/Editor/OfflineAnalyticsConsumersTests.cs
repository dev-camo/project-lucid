using Hardlight.Analytics;
using HardlightProject;
using NUnit.Framework;
using System.Reflection;
using static ProjectLucid.Tests.OfflineAnalyticsConsumersTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using HardlightAnalytics = Hardlight.Analytics.Analytics;

namespace ProjectLucid.Tests
{
    // The offline policy for the omitted analytics services (ProjectLucid.Offline.OfflineAnalyticsConsumers, issue 414) and
    // the production selection that omits them. Each consumer's own cases live beside the consumer; these tie the default
    // startup to the policy and pin the preserved original bodies.
    public sealed class OfflineAnalyticsConsumersTests
    {
        // The adapters are internal to Game.Runtime.
        private static object Invoke(string type, string method, params object[] arguments) =>
            typeof(App).Assembly.GetType("ProjectLucid.Offline." + type, true)
                .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, arguments);

        // App.InitialiseAnalytics (0x06000571) registers the session and consent managers only when the offline adapters are off (hook H-014); the
        // active adapters run OfflineTelemetryStartup.Initialise, after which neither service has a row, and each consumer
        // hook answers its documented offline policy without creating one. Analytics settings are restored as found.
        [Test]
        public void TheDefaultOfflineStartupLeavesBothManagersUnregisteredAndTheConsumersFollowThePolicy()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<AnalyticsConsentManager>(); // restores exactly the row state found, idle or absent
                owned.LeaseRow<AnalyticsSessionManager>();
                bool consentRow = RequireNoManager<AnalyticsConsentManager>();
                bool sessionRow = RequireNoManager<AnalyticsSessionManager>();
                IAnalyticsSettings prior = HardlightAnalytics.GetAnalyticsSettings();
                try
                {
                    Invoke("OfflineTelemetryStartup", "Initialise");

                    AssertUntouched<AnalyticsConsentManager>(consentRow, "startup registers no consent manager");
                    AssertUntouched<AnalyticsSessionManager>(sessionRow, "startup registers no session manager");
                    Assert.That(Invoke("OfflineAnalyticsConsumers", "CanShowConsentScreenDuringBoot"), Is.False);
                    Assert.That(Invoke("OfflineAnalyticsConsumers", "CanShowConsentScreenButtonOnSettings"), Is.False);
                    Assert.DoesNotThrow(() => Invoke("OfflineAnalyticsConsumers", "ReloadAnalyticsConsumers", new object[] { null }));
                    AssertUntouched<AnalyticsConsentManager>(consentRow, "no consumer left a consent row");
                    AssertUntouched<AnalyticsSessionManager>(sessionRow, "no consumer left a session row");
                }
                finally
                {
                    Invoke("OfflineTelemetryStartup", "Shutdown");
                    HardlightAnalytics.Initialise(prior);
                }
            });
        }
    }
}
