using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalGameCenterLocalPlayerPreservationTests
    {
        [Test]
        public void OriginalShippingGameCenterStubRetainsIdentityAndCallbackRules() =>
            Assert.That(GameCenterLocalPlayerPreservationVerification.ShippingStubIdentityAndCallbacks(), Is.EqualTo(22));
        [Test]
        public void OriginalGameCenterPlayerRetainsParsingAndIdentifierRules() =>
            Assert.That(GameCenterLocalPlayerPreservationVerification.PlayerParsingAndIdentity(), Is.EqualTo(32));
        [Test]
        public void OriginalGameCenterLocalPlayerRetainsAuthenticationAndCacheOrder() =>
            Assert.That(GameCenterLocalPlayerPreservationVerification.LocalPlayerAuthenticationOrder(), Is.EqualTo(15));
        [Test]
        public void OriginalShippingGameCenterPhotoCoroutineRetainsWhiteTextureAndCleanup() =>
            Assert.That(GameCenterLocalPlayerPreservationVerification.ShippingPhotoCoroutineInUnity(), Is.EqualTo(12));
    }
}
