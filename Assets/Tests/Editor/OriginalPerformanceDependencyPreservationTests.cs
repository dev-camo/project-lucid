using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    public class OriginalPerformanceDependencyPreservationTests
    {
        [Test]
        public void CopyPreservesAliasesAndDestinationCallbacks()
        { Assert.That(OriginalPerformanceDependencyVerification.RunCopyManaged20(), Is.EqualTo(20)); }

        [Test]
        public void DelegateAccessorsPreserveDuplicatesAndCapturedOrder()
        { Assert.That(OriginalPerformanceDependencyVerification.RunDelegateAccessorsManaged18(), Is.EqualTo(18)); }

        [Test]
        public void ComparisonsPreserveThresholdsAndMissingFeatureFallback()
        { Assert.That(OriginalPerformanceDependencyVerification.RunComparisonsEngine44(), Is.EqualTo(44)); }

        [Test]
        public void ProfileCallbacksPreserveReentryAndFaultPrefixes()
        { Assert.That(OriginalPerformanceDependencyVerification.RunCallbacksAndEventsEngine22(), Is.EqualTo(22)); }

        [Test]
        public void ProfileSubscriptionsPreserveDuplicateAndFaultState()
        { Assert.That(OriginalPerformanceDependencyVerification.RunProfileSubscriptionsEngine18(), Is.EqualTo(18)); }
    }
}
