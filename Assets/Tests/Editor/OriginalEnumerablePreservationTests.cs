using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalEnumerablePreservationTests
    {
        [Test]
        public void DeepEqualityPreservesNullFaultAndDisposalOrder() =>
            Assert.That(OriginalEnumerablePreservationVerification.VerifyDeepEquality(), Is.EqualTo(32));

        [Test]
        public void DeepHashPreservesSignedOrderedValuesAndCleanup() =>
            Assert.That(OriginalEnumerablePreservationVerification.VerifyDeepHash(), Is.EqualTo(12));

        [Test]
        public void JoinPreservesBuilderAliasAndFaultPrefixes() =>
            Assert.That(OriginalEnumerablePreservationVerification.VerifyJoin(), Is.EqualTo(27));

        [Test]
        public void CountPreservesCallbackReceiverMutationAndReentry() =>
            Assert.That(OriginalEnumerablePreservationVerification.VerifyCount(), Is.EqualTo(16));

        [Test]
        public void ZipPreservesDeferredAcquisitionAndAsymmetricCleanup() =>
            Assert.That(OriginalEnumerablePreservationVerification.VerifyZip(), Is.EqualTo(33));

    }
}
