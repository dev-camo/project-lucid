using NUnit.Framework;
using ProjectLucid.Editor;

namespace ProjectLucid.Tests
{
    public sealed class OriginalArrayExtensionsPreservationTests
    {
        [Test]
        public void OriginalArrayExtensionsRetainCompleteDeclarations()
        {
            Assert.That(OriginalArrayExtensionsVerification.RunOriginalDeclarations(), Is.EqualTo(67));
        }

        [Test]
        public void OriginalArrayValidityAndContainsRetainNullAndEqualityRules()
        {
            Assert.That(OriginalArrayExtensionsVerification.RunOriginalArrayValidityAndContains(), Is.EqualTo(17));
        }

        [Test]
        public void OriginalArrayDelegateConversionsRetainOrderAndFailurePrefixes()
        {
            Assert.That(OriginalArrayExtensionsVerification.RunOriginalDelegateConversions(), Is.EqualTo(21));
        }

        [Test]
        public void OriginalArrayImplicitConversionsRetainEnumAndCultureRules()
        {
            Assert.That(OriginalArrayExtensionsVerification.RunOriginalImplicitConversions(), Is.EqualTo(16));
        }

        [Test]
        public void OriginalArrayTryConversionsRetainFailureAndOutputRules()
        {
            Assert.That(OriginalArrayExtensionsVerification.RunOriginalTryConversionFailures(), Is.EqualTo(19));
        }

        [Test]
        public void OriginalArraySwapsRetainEqualIndexAndFaultOrdering()
        {
            Assert.That(OriginalArrayExtensionsVerification.RunOriginalSwapBoundaries(), Is.EqualTo(18));
        }

        [Test]
        public void OriginalArrayValidityRetainsDestroyedUnityWrappers()
        {
            Assert.That(OriginalArrayExtensionsVerification.RunOriginalUnityObjectValidity(), Is.EqualTo(8));
        }
    }
}
