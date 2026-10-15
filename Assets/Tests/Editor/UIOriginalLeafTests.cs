using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    [TestFixture]
    public sealed class UIOriginalLeafTests
    {
        // Emission-regression check of the project's own compile, not original evidence.
        [Test]
        public void CurrentBuildEmissionMatchesRecordedSnapshot()
        {
            UIOriginalLeafVerification.VerifyCurrentBuildEmissionMatchesRecordedSnapshot();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OriginalMenuCameraUsesOwnedInactiveStoredCameraAndUnguardedToggle()
        {
            UIOriginalLeafVerification.VerifyOwnedInactiveMenuCamera();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OriginalUIConfigurationUsesOwnedInactiveStoredGettersAndPrimitiveJson()
        {
            UIOriginalLeafVerification.VerifyOwnedInactiveConfigurationStoredGetters();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OriginalUIConfigurationLifecycleRegistersAndUnregistersBeforeUnguardedBridgeCalls()
        {
            UIOriginalLeafVerification.VerifyOwnedInactiveConfigurationLifecycleRegistryOrder();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OriginalUIConfigurationLifecycleRevokesOwnedRegistrationAfterAssertionFault()
        {
            UIOriginalLeafVerification.VerifyOwnedInactiveConfigurationLifecycleReleasesRowAfterAssertionFault();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OriginalAbstractUIBridgeAndSupplierPreserveCompleteMetadataAndBaseOnlyConstructors()
        {
            UIOriginalLeafVerification.VerifyOriginalAbstractBridgeAndSupplierMetadata();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
