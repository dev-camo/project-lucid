using System;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalMessageManagerPreservationTests
    {
        [Test]
        public void RealFiveProvidersInvalidateOldHandlesAndAllowFreshHandlesAfterRepeatedShutdown()
        {
            Assert.That(WithShutdownRow(ProjectLucid.Verification.OriginalMessageManagerVerification.RunRealFiveProviderShutdownHandles), Is.EqualTo(56));
        }

        [Test]
        public void FiveProvidersReceiveOrderedNullContextAndFaultPrefixRetries()
        {
            Assert.That(WithShutdownRow(ProjectLucid.Verification.OriginalMessageManagerVerification.RunFiveOrderedCallbacksAndFaultRetry), Is.EqualTo(44));
        }

        // MessageManager and each MessageExchange provider subscribe to Shutdown
        // on construction, and SubscribeToAction creates a missing action row
        // that the proofs' UnsubscribeFromAllActions keeps. Own that row and
        // release it after the proof's own cleanup, on the pass and failure paths.
        private static int WithShutdownRow(Func<int> proof)
        {
            var actionRows = new VerificationFixtureOwnership.ActionRows(SystemAction.Shutdown);
            int checks = 0;
            VerificationFixtureOwnership.RunThenCleanUp(() => checks = proof(), actionRows.Dispose);
            return checks;
        }
    }
}
