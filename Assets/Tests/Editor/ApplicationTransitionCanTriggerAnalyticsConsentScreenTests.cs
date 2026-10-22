using System;
using Hardlight;
using Hardlight.Analytics;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // ApplicationTransitionCanTriggerAnalyticsConsentScreen (Game.Runtime 0x02000166, DoUpdate 0x06000833) under the default
    // (offline) startup, which registers no AnalyticsConsentManager. The boot consent flow (Application_AnalyticsConsentFlow)
    // takes NOT(this transition) -> Finished, so a false answer ends the flow. Not covered: the consulted-manager path (the
    // original body, which a registered manager would take through the hook's honoured-service branch), whose answer needs a
    // logged-in GameCenter player that the offline GameCenter never reports. With the adapters off (hook H-066) the original body
    // dereferences the missing manager, which the second case pins.
    public sealed class ApplicationTransitionCanTriggerAnalyticsConsentScreenTests
    {
        private static readonly FSMUpdateContext s_context = new FSMUpdateContext(0.25f, FSMUpdateType.Manual);

        // The field initialiser's typed reference auto-creates an empty consent row (original behaviour); the case owns it.
        [Test]
        public void WithoutAConsentManagerNoConsentScreenIsTriggeredDuringBoot()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<AnalyticsConsentManager>();
                IFSMTransition transition = ApplicationTransitionCanTriggerAnalyticsConsentScreen.ConstructInstance(
                    new FiniteStateMachine("Lucid-consent-fsm-" + Guid.NewGuid().ToString("N"), skipAddToManager: true),
                    "Lucid-consent-transition-" + Guid.NewGuid().ToString("N"), null);
                var user = new FSMUser(new FSMStorage(0));

                bool result = true;
                Assert.DoesNotThrow(() => result = transition.Update(user, s_context));

                Assert.That(result, Is.False);
            });
        }

        // Hook H-066 with the adapters off: the original body dereferences the consent manager that the offline startup never
        // registers (the empty reference throws, or logs and returns null, which the call then dereferences).
        [Test]
        public void WithTheAdaptersOffTheOriginalBodyNeedsTheConsentManager()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<AnalyticsConsentManager>();
                IFSMTransition transition = ApplicationTransitionCanTriggerAnalyticsConsentScreen.ConstructInstance(
                    new FiniteStateMachine("Lucid-consent-fsm-" + Guid.NewGuid().ToString("N"), skipAddToManager: true),
                    "Lucid-consent-transition-" + Guid.NewGuid().ToString("N"), null);
                var user = new FSMUser(new FSMStorage(0));
                bool ignoring = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
                try
                {
                    using (ProjectLucid.Offline.OfflineAdapters.Override(false))
                    {
                        Assert.Throws<NullReferenceException>(() => transition.Update(user, s_context));
                    }
                }
                finally
                {
                    UnityEngine.TestTools.LogAssert.ignoreFailingMessages = ignoring;
                }
            });
        }
    }
}
