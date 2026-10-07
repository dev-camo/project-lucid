using NUnit.Framework;
using ProjectLucid.Editor;

namespace ProjectLucid.Tests
{
    public sealed class OriginalMissionRingRewardPreservationTests
    {
        [Test]
        public void OriginalMissionRingRewardsPreserveAuthoredOrderAndInclusiveSignedThresholds()
        {
            Assert.That(OriginalMissionRingRewardVerification.RunOriginalBoundaries(), Is.EqualTo(41));
        }
    }
}
