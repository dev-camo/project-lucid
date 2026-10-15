using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class HLHapticsWeakRemediationTests
    {
        // Catches a reordered serialized field or a changed enum literal, which a self
        // round trip of the same data would carry through unchanged.
        [Test]
        public void EventDataJsonUsesOriginalFieldOrderAndEnumLiterals()
        {
            HLHapticsEventContractVerification.JsonUsesOriginalFieldOrderAndEnumLiterals();
            LogAssert.NoUnexpectedReceived();
        }

        // Catches dropping the unavailable branch's None return or its debug-flag return.
        [Test]
        public void UnavailableTriggerTestsNoneBeforeDebugFlagAndDefinitions()
        {
            HLHapticsEventContractVerification.UnavailableTriggerTestsNoneBeforeDebugFlagAndDefinitions();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
