using NUnit.Framework;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class LocalAccountTests
    {
        [Test] public void BothFactoryFlagsKeepIdentitySeparateFromAuthentication() => LocalAccountVerification.BothFactoryFlagsKeepIdentitySeparateFromAuthentication();
        [Test] public void RealCallbacksPreserveOrderPayloadAndClear() => LocalAccountVerification.RealCallbacksPreserveOrderPayloadAndClear();
        [Test] public void CallbackFaultStopsLaterPhotoCompletionAndOwnedClearRecovers() => LocalAccountVerification.CallbackFaultStopsLaterPhotoCompletionAndOwnedClearRecovers();
        [Test] public void GenuineFacadeDefaultsKeepFailedAuthenticationAndShutdownCleanup() => LocalAccountVerification.GenuineFacadeDefaultsKeepFailedAuthenticationAndShutdownCleanup();
        [Test] public void GenuinePlayerPhotoIteratorCompletesWithoutYieldThroughLocalCallbacks() => LocalAccountVerification.GenuinePlayerPhotoIteratorCompletesWithoutYieldThroughLocalCallbacks();
    }
}
