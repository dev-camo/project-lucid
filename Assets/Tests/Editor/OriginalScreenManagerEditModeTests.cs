using NUnit.Framework;
using ProjectLucid.Tests.ScreenShared;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalScreenManagerEditModeTests
    {
        [Test] public void CompleteDeclaredManagerAndEnumShapes() => ScreenManagerVerification.CompleteDeclaredManagerAndEnumShapes();
        [Test] public void TwoNaturalIteratorsHaveTwelveManagedRoles() => ScreenManagerVerification.TwoNaturalIteratorsHaveTwelveManagedRoles();
        [Test] public void InactiveAttachmentRetainsOriginalConstructorDefaults() => ScreenManagerVerification.InactiveAttachmentRetainsOriginalConstructorDefaults();
        [Test] public void InactiveNoChangeSuppressesCompletion() => ScreenManagerVerification.InactiveNoChangeSuppressesCompletion();
        [Test] public void InactivePublicSubscriptionsRetainOwnedMembership() => ScreenManagerVerification.InactivePublicSubscriptionsRetainOwnedMembership();
        [Test] public void InactiveLockAndTogglePreserveOriginalGate() => ScreenManagerVerification.InactiveLockAndTogglePreserveOriginalGate();
    }
}
