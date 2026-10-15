using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class CameraInputDisablerTests
    {
        // Current-build snapshot gate, kept apart so behavior cases do not depend on current compiler output.
        [Test] public void CurrentDeclarationsAndBodiesMatchSnapshot() => CameraInputDisablerVerification.VerifyCurrentDeclarationsAndBodies();
        [Test] public void OriginalAxesThresholdsAndSnapArguments() { Assert.AreEqual(15, CameraInputDisablerVerification.VerifyCameraAxesAndSnapCallbacks()); LogAssert.NoUnexpectedReceived(); }
        [Test] public void OriginalCaptureAndRealDebounceRetainFaultOrder() { Assert.AreEqual(10, CameraInputDisablerVerification.VerifyOriginalDeferredDebounceBodyAndFaultOrder()); LogAssert.NoUnexpectedReceived(); }
        [Test] public void OriginalCameraSubscriptionsRetainForeignCallbacks() { Assert.AreEqual(35, CameraInputDisablerVerification.VerifyOriginalCameraRegistrationAndShutdown()); LogAssert.NoUnexpectedReceived(); }
        [Test] public void OriginalDisablerGuardAndInactiveEnableCallbacks() { Assert.AreEqual(8, CameraInputDisablerVerification.VerifyDisablerGuardsAndOriginalEnableCallbacks()); LogAssert.NoUnexpectedReceived(); }
        [Test] public void OriginalDisablerCapturedArrayAndPartialFault() { Assert.AreEqual(3, CameraInputDisablerVerification.VerifyDisablerCapturedArrayAndPartialAddFault()); LogAssert.NoUnexpectedReceived(); }
        [Test] public void OriginalDisablerReentrancyAndLiveReleaseFaults() { Assert.AreEqual(6, CameraInputDisablerVerification.VerifyDisablerReentrancyAndReleaseFaults()); LogAssert.NoUnexpectedReceived(); }
    }
}
