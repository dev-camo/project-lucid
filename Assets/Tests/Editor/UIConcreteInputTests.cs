using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class UIConcreteInputTests
    {
        [Test] public void ConcreteDeclarationsAndOwnedDefaults() { Assert.Greater(UIConcreteInputVerification.VerifyDeclarationsAndDefaults(), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void EnumValidationPrecedesNullModuleFault() { Assert.Greater(UIConcreteInputVerification.VerifyNullModuleAndEnumBoundary(), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void AllNullSuppliersFaultBeforeMapping() { Assert.AreEqual(8, UIConcreteInputVerification.VerifyNullSupplierBeforeMapping()); LogAssert.NoUnexpectedReceived(); }
        [Test] public void GenuineSupplierFieldAndGetterRoundtrip() { Assert.Greater(UIConcreteInputVerification.VerifySupplierFieldRoundtrip(), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void GenuineTypedCallbacksAndLastInputMapping() { Assert.Greater(UIConcreteInputVerification.VerifyTypedCallbackAndLastType(), 0); LogAssert.NoUnexpectedReceived(); }
        [Test] public void OriginalLatchAndShutdownRemovalOrder() { Assert.Greater(UIConcreteInputVerification.VerifyRegistrationAndShutdownOrdering(), 0); LogAssert.NoUnexpectedReceived(); }
    }
}
