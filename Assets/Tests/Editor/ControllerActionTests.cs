using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class ControllerActionTests
    {
        [Test] public void GenuineConstructorsAndPersistentSystemCache() { Assert.Greater(ControllerActionVerification.VerifyDeclarationsConstructorsAndCache(), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void HeldUpAndDisabledPublishBeforeBrainFaults() { Assert.Greater(ControllerActionVerification.VerifyHeldUpAndDisabledPrefixes(), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void RejectedModifiersAndValidatorsRetainFlags() { Assert.Greater(ControllerActionVerification.VerifyRejectedModifiersAndValidatorFaults(), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void PrimarySecondaryMappingCallsAndRealClosures() { Assert.Greater(ControllerActionVerification.VerifySettingsMappingAndOwnedClosures(), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void NeverActiveInputSystemUsesExplicitOriginalCallbacks() { Assert.Greater(ControllerActionVerification.VerifyOwnedInputSystemExplicitCallbacks(), 0); LogAssert.NoUnexpectedReceived(); }
    }
}
