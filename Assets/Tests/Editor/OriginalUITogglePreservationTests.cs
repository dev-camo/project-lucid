using ProjectLucid.Verification;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalUITogglePreservationTests
    {
        [Test] public void OriginalUICanvasAndUnconditionalSetter() => Assert.AreEqual(16, OriginalUITogglePreservationVerification.CanvasAndUnconditionalSetter());
        [Test] public void OriginalUITogglePointerAndFreshGroupCallbacks() => Assert.AreEqual(16, OriginalUITogglePreservationVerification.TogglePointerAndFreshGroupCallbacks());
        [Test] public void OriginalUIMembershipAndSignedSelectionFaults() => Assert.AreEqual(20, OriginalUITogglePreservationVerification.MembershipAndSignedSelectionFaults());
        [Test] public void OriginalUILiveMutationAndSwitchOffFaults() => Assert.AreEqual(16, OriginalUITogglePreservationVerification.LiveMutationAndSwitchOffFaults());
        [Test] public void OriginalUIElementLifecycleMembership() => Assert.AreEqual(12, OriginalUITogglePreservationVerification.ElementLifecycleMembership());
        [Test] public void OriginalUIAnimationQueueCallbackAndDisableFaults() => Assert.AreEqual(21, OriginalUITogglePreservationVerification.AnimationQueueCallbackAndDisableFaults());
        [Test] public void OriginalUIAnimationPointerAndSettingFaultPrefixes() => Assert.AreEqual(17, OriginalUITogglePreservationVerification.AnimationPointerAndSettingFaultPrefixes());
    }
}
