using NUnit.Framework;
using UnityEngine.TestTools;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    // The pinned actual Unity EditMode runner completes each child before starting the next.
    // Unity's genuine custom NUnit has TestFixture/Test; it has no NonParallelizable attribute.
    [TestFixture]
    public sealed class BootTransitionTests
    {
        [Test] public void JsonDefaultsAndOriginalFactories() { BootTransitionVerification.VerifyJsonDefaultsAndOriginalFactories(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void SaveLoadedTruthTableAndNoDefaultInsertion() { BootTransitionVerification.VerifySaveLoadedTruthTableAndNoDefaultInsertion(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void UserAndTypedNullStorageFaults() { BootTransitionVerification.VerifyUserAndTypedNullStorageFaults(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void ConstructorRegistrationBeforeJsonFault() { BootTransitionVerification.VerifyConstructorRegistrationBeforeJsonFault(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void NullMachineFactoryFaults() { BootTransitionVerification.VerifyNullMachineFactoryFaults(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void ShippingEditorPredicateAndIgnoredJson() { BootTransitionVerification.VerifyShippingEditorPredicateAndIgnoredJson(); LogAssert.NoUnexpectedReceived(); }
    }
}
