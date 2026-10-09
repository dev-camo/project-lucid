using NUnit.Framework;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalMessageManagerPreservationTests
    {
        [Test]
        public void RealFiveProvidersInvalidateOldHandlesAndAllowFreshHandlesAfterRepeatedShutdown()
        {
            Assert.That(ProjectLucid.Verification.OriginalMessageManagerVerification.RunRealFiveProviderShutdownHandles(), Is.EqualTo(56));
        }

        [Test]
        public void FiveProvidersReceiveOrderedNullContextAndFaultPrefixRetries()
        {
            Assert.That(ProjectLucid.Verification.OriginalMessageManagerVerification.RunFiveOrderedCallbacksAndFaultRetry(), Is.EqualTo(44));
        }
    }
}
