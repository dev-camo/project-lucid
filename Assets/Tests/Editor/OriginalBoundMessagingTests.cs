using System;
using System.Linq;
using Hardlight;
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
        // HLModernUI.Runtime UIModernEventType 0x02000010 has exactly fourteen literal rows,
        // 0x0400002b OnInteractable = 0 through 0x04000038 OnDisabledSelected = 13, which is
        // the 0..13 range the verifier constructs messages for.
        [Test] public void OriginalBoundMessageReadonlyHandlesAndParameterCasts()
        {
            Assert.AreEqual(25, OriginalBoundMessagingVerification.ReadonlyHandlesAndWidgetParameterCasts());
            CollectionAssert.AreEqual(new[] { "OnInteractable", "OnUninteractable", "OnNavigateToward", "OnNavigateAway",
                "OnDeselect", "OnPress", "OnRelease", "OnDisabled", "OnClick", "OnDisabledClick", "OnOfflineClick",
                "OnButtonDown", "OnButtonUp", "OnDisabledSelected" }, Enum.GetNames(typeof(UIModernEventType)));
            CollectionAssert.AreEqual(Enumerable.Range(0, 14).ToArray(), Enum.GetValues(typeof(UIModernEventType)).Cast<int>().ToArray());
        }
        [Test] public void OriginalBoundMessageExchangeApisRestoreProcessActions() => Assert.AreEqual(24, OriginalBoundMessagingVerification.ExchangeApisAndProcessSubscriptionRestoration());
    }
}
