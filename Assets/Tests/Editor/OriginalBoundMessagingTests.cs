using ProjectLucid.Verification;
using NUnit.Framework;
namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalBoundMessagingTests
    {
        [Test] public void OriginalBoundMessageOrderFlagsAndMissingKeys() => Assert.AreEqual(15, OriginalBoundMessagingVerification.OrderingFlagsAndMissingBindings());
        [Test] public void OriginalBoundMessageDuplicatesNullAndFaultPrefixes() => Assert.AreEqual(13, OriginalBoundMessagingVerification.DuplicateNullAndCallbackFaultPrefixes());
        [Test] public void OriginalBoundMessageLiveMutationAndNestedPublish() => Assert.AreEqual(6, OriginalBoundMessagingVerification.LiveMutationAndReentrantCache());
        [Test] public void OriginalBoundMessageInvalidationAndNullKeyState() => Assert.AreEqual(15, OriginalBoundMessagingVerification.InvalidationAndNullKeyFaultState());
        [Test] public void OriginalBoundMessageReadonlyHandlesAndParameterCasts() => Assert.AreEqual(25, OriginalBoundMessagingVerification.ReadonlyHandlesAndWidgetParameterCasts());
        [Test] public void OriginalBoundMessageExchangeApisRestoreProcessActions() => Assert.AreEqual(24, OriginalBoundMessagingVerification.ExchangeApisAndProcessSubscriptionRestoration());
    }
}
