using NUnit.Framework;
using ProjectLucid.RecoveredProof;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public class OriginalCoreHelperPreservationTests
    {
        [Test] public void OpStringCreationMutationAndOverloads() => Assert.AreEqual(29, OriginalCoreHelperPreservationVerification.OpStringCreationMutationAndOverloads());
        [Test] public void OpStringCultureFormattingAndOriginalTrim() => Assert.AreEqual(30, OriginalCoreHelperPreservationVerification.OpStringCultureFormattingAndOriginalTrim());
        [Test] public void OpStringThreadCacheAndExceptionalRestoration() => Assert.AreEqual(22, OriginalCoreHelperPreservationVerification.OpStringThreadCacheAndExceptionalRestoration());
        [Test] public void ListSearchLiveAliasAndOutFaults() => Assert.AreEqual(15, OriginalCoreHelperPreservationVerification.ListSearchLiveAliasAndOutFaults());
        [Test] public void ListUniqueRangeCallbacksAndDisposal() => Assert.AreEqual(20, OriginalCoreHelperPreservationVerification.ListUniqueRangeCallbacksAndDisposal());
        [Test] public void SeededShuffleSnapshotCallbacksAndPartialWrites() => Assert.AreEqual(16, OriginalCoreHelperPreservationVerification.SeededShuffleSnapshotCallbacksAndPartialWrites());
        [Test] public void ResourcePathStringsAndOutFaultPrefixes() => Assert.AreEqual(25, OriginalCoreHelperPreservationVerification.ResourcePathStringsAndOutFaultPrefixes());
        [Test] public void ResourceCombineCacheAliasesAndRestoration() => Assert.AreEqual(14, OriginalCoreHelperPreservationVerification.ResourceCombineCacheAliasesAndRestoration());
    }
}
