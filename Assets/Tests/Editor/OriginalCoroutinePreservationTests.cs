using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalCoroutinePreservationTests
    {
        [Test] public void OriginalCoroutineWholeDeclarations() => Assert.AreEqual(74, CoroutinePreservationVerification.OriginalDeclarations());
        [Test] public void OriginalCoroutineSingleYieldFaultAndDisposalOrder() => Assert.AreEqual(45, CoroutinePreservationVerification.SingleYieldCallbacksAndFaults());
        [Test] public void OriginalCoroutineSignedFrameCountsAndResumeOverflow() => Assert.AreEqual(37, CoroutinePreservationVerification.FrameCountsSignedAndResume());
        [Test] public void OriginalCoroutinePredicateOrderAndPartialFailures() => Assert.AreEqual(25, CoroutinePreservationVerification.PredicateOrderAndPartialFaults());
        [Test] public void OriginalCoroutineCachedWaitAndStartPredicatePolarity() => Assert.AreEqual(44, CoroutinePreservationVerification.WaitObjectsAndIntervalOrder());
        [Test] public void OriginalCoroutineLazyTimeAndNullHostBranches() => Assert.AreEqual(42, CoroutinePreservationVerification.LazyTimeAndNullInstanceBranches());
    }
}
