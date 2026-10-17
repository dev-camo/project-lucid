using System;
using System.Collections;
using Hardlight;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    public sealed class OriginalMessageCompletionPreservationTests
    {
        [Test]
        public void FullOriginalCompletionApiRetainsSignaturesFieldsFlagsAndReadonlyArguments()
        {
            Assert.That(WithShutdownRow(ProjectLucid.Editor.OriginalMessageCompletionVerification.RunOriginalApi), Is.EqualTo(1153));
        }

        [Test]
        public void OriginalCallbacksPoolingMutationFaultsAndAllAritiesArePreserved()
        {
            Assert.That(WithShutdownRow(ProjectLucid.Editor.OriginalMessageCompletionVerification.RunOriginalSynchronousBoundaries), Is.EqualTo(49));
        }

        [UnityTest]
        public IEnumerator OriginalTimeoutsCompleteWithoutBlockingTheUnitySynchronizationContext()
        {
            int checks = 0;
            var actionRows = new VerificationFixtureOwnership.ActionRows(SystemAction.Shutdown);
            bool completed = false;
            try
            {
                yield return ProjectLucid.Editor.OriginalMessageCompletionVerification.RunOriginalTimeoutBoundaries(result => checks = result);
                completed = true;
            }
            finally
            {
                // As in RunThenCleanUp: a release report never replaces the proof's own failure.
                if (completed) actionRows.Dispose();
                else
                {
                    try { actionRows.Dispose(); }
                    catch (Exception failure) { UnityEngine.Debug.LogWarning("Fixture cleanup failed after an earlier failure: " + failure); }
                }
            }
            Assert.That(checks, Is.EqualTo(14));
        }

        // MessageExchangeBase's constructor subscribes to Shutdown, and
        // SubscribeToAction creates a missing action row that the proofs'
        // UnsubscribeFromAction keeps. Own that row and release it after the
        // proof's own cleanup, on the pass and failure paths.
        private static int WithShutdownRow(Func<int> proof)
        {
            var actionRows = new VerificationFixtureOwnership.ActionRows(SystemAction.Shutdown);
            int checks = 0;
            VerificationFixtureOwnership.RunThenCleanUp(() => checks = proof(), actionRows.Dispose);
            return checks;
        }
    }
}
