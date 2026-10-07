using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    public sealed class OriginalMessageCompletionPreservationTests
    {
        [Test]
        public void FullOriginalCompletionApiRetainsSignaturesFieldsFlagsAndReadonlyArguments()
        {
            Assert.That(ProjectLucid.Editor.OriginalMessageCompletionVerification.RunOriginalApi(), Is.EqualTo(1153));
        }

        [Test]
        public void OriginalCallbacksPoolingMutationFaultsAndAllAritiesArePreserved()
        {
            Assert.That(ProjectLucid.Editor.OriginalMessageCompletionVerification.RunOriginalSynchronousBoundaries(), Is.EqualTo(49));
        }

        [UnityTest]
        public IEnumerator OriginalTimeoutsCompleteWithoutBlockingTheUnitySynchronizationContext()
        {
            int checks = 0;
            yield return ProjectLucid.Editor.OriginalMessageCompletionVerification.RunOriginalTimeoutBoundaries(result => checks = result);
            Assert.That(checks, Is.EqualTo(14));
        }
    }
}
