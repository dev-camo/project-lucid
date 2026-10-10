using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class CameraInputDisablerTests
    {
        [Test] public void OriginalAxesThresholdsAndSnapArguments() { Assert.Greater(CameraInputDisablerVerification.VerifyCameraAxesAndSnapCallbacks(), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void OriginalCaptureAndRealDebounceRetainFaultOrder() { Assert.Greater(CameraInputDisablerVerification.VerifyOriginalDeferredDebounceBodyAndFaultOrder(), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void OriginalCameraSubscriptionsRetainForeignCallbacks() { Assert.Greater(CameraInputDisablerVerification.VerifyOriginalCameraRegistrationAndShutdown(), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void OriginalDisablerGuardAndInactiveEnableCallbacks() { Assert.Greater(CameraInputDisablerVerification.VerifyDisablerGuardsAndOriginalEnableCallbacks(), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void OriginalDisablerCapturedArrayAndPartialFault() { Assert.Greater(CameraInputDisablerVerification.VerifyDisablerCapturedArrayAndPartialAddFault(), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void OriginalDisablerReentrancyAndLiveReleaseFaults() { Assert.Greater(CameraInputDisablerVerification.VerifyDisablerReentrancyAndReleaseFaults(), 0); LogAssert.NoUnexpectedReceived(); }
    }
}
