using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    public class OriginalNotificationsPreservationTests
    {
        [Test]
        public void ReceivedTokenPreservesStoreReentryAndFaultOrder()
        { Assert.That(OriginalNotificationsVerification.RunManagedCallbacks(), Is.EqualTo(31)); }

        [Test]
        public void ConfigurationPreservesDefaultsAndOverrideAliases()
        { Assert.That(OriginalNotificationsVerification.RunConfigurationEngine(), Is.EqualTo(10)); }
    }
}
