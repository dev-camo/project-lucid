using NUnit.Framework;
using ProjectLucid.Editor;

namespace ProjectLucid.Tests
{
    public sealed class OriginalGameTimeScaledUtilitiesPreservationTests
    {
        [Test]
        public void OriginalGameWaitDefersLookupAndRefreshesDestroyedConfiguration()
        {
            Assert.That(OriginalTimeScaledUtilitiesVerification.RunOriginalGameWaitConfigurationBoundaries(), Is.EqualTo(26));
        }

        [Test]
        public void OriginalGameTimingRetainsImmediateAndDeferredFaultPrefixes()
        {
            Assert.That(OriginalTimeScaledUtilitiesVerification.RunOriginalGameTimingFaultPrefixes(), Is.EqualTo(17));
        }
    }
}
