using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalLeaderboardPreservationTests
    {
        [Test]
        public void OriginalLeaderboardRecordsRetainParsingCultureAndIdentity() =>
            Assert.That(LeaderboardPreservationVerification.RecordParsingAndIdentity(), Is.EqualTo(56));
        [Test]
        public void OriginalShippingLeaderboardRequestsRetainRecurrenceAndScoreCleanup() =>
            Assert.That(LeaderboardPreservationVerification.ShippingRequestsAndRecurrence(), Is.EqualTo(18));
        [Test]
        public void OriginalLeaderboardEntryRequestsRetainRanksAndStartedSubscriptionMismatch() =>
            Assert.That(LeaderboardPreservationVerification.EntryListsAndRetainedSubscription(), Is.EqualTo(14));
        [Test]
        public void OriginalLeaderboardFaultedIteratorsRetainEmptyDisposal() =>
            Assert.That(LeaderboardPreservationVerification.RequestFaultsAndDisposal(), Is.EqualTo(7));
        [Test]
        public void OriginalLeaderboardManagedCallbacksRetainSharedMapRouting() =>
            Assert.That(LeaderboardPreservationVerification.ManagedCallbackRouting(), Is.EqualTo(10));
        [Test]
        public void OriginalShippingLeaderboardImageRequestRetainsWhiteTextureAndCleanup() =>
            Assert.That(LeaderboardPreservationVerification.ShippingImageRequestInUnity(), Is.EqualTo(3));
    }
}
