using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    [TestFixture]
    public sealed class UIOriginalLeafTests
    {
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
        public void OriginalAbstractUIBridgeAndSupplierPreserveCompleteMetadataAndBaseOnlyBodies()
        {
            UIOriginalLeafVerification.VerifyOriginalAbstractBridgeAndSupplierMetadata();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
