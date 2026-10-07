using NUnit.Framework;
using ProjectLucid.Editor;

namespace ProjectLucid.Tests
{
    public sealed class OriginalTimeScaledUtilitiesPreservationTests
    {
        [Test]
        public void OriginalTimingDeclarationsAbsentManagerAndDelayedActionsRemainPreserved()
        {
            Assert.That(OriginalTimeScaledUtilitiesVerification.RunOriginalManagedBoundaries(), Is.EqualTo(207));
        }

        [Test]
        public void OriginalTimingPresentManagerRetainsInactiveAndUnorderedDurationOrdering()
        {
            Assert.That(OriginalTimeScaledUtilitiesVerification.RunOriginalDurationBoundaries(), Is.EqualTo(40));
        }

        [Test]
        public void OriginalTimingFixedYieldsRetainCapturedManagerAndActualCategoryScales()
        {
            Assert.That(OriginalTimeScaledUtilitiesVerification.RunOriginalFixedTimeBoundaries(), Is.EqualTo(28));
        }

        [Test]
        public void OriginalTimingFrameYieldsUseActualUnityDeltaAndUnorderedTimerSemantics()
        {
            Assert.That(OriginalTimeScaledUtilitiesVerification.RunOriginalFrameTimeBoundaries(), Is.EqualTo(10));
        }

        [Test]
        public void OriginalTimingPredicateAndNestedDelayFaultsPreserveReentryAndEmptyDispose()
        {
            Assert.That(OriginalTimeScaledUtilitiesVerification.RunOriginalFaultAndNestedDelayBoundaries(), Is.EqualTo(34));
        }
    }
}
