using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    // Controlled original configuration, direct trigger callbacks and glyph tests.
    // Automatic trigger lifecycle scheduling is a separate PlayMode scope.
    public sealed class InputRemainingOriginalBehaviorTests
    {
        [Test]
        public void OriginalConfigurationPathsAndInactiveProvidersRemainIntact()
        {
            InputRemainingOriginalBehaviorVerification.VerifyConfigurationAndInactiveProviders();
            LogAssert.NoUnexpectedReceived();
        }
        [Test]
        public void OriginalGameInputTriggerControlledCallbacksPreserveSubscriptionAndUnityEventOrder()
        {
            InputRemainingOriginalBehaviorVerification.VerifyGameInputTriggerCallbacks();
            LogAssert.NoUnexpectedReceived();
        }
        [Test]
        public void OriginalGameInputTriggerControlledCallbacksPreserveNullAndCallbackFaultOrdering()
        {
            InputRemainingOriginalBehaviorVerification.VerifyGameInputTriggerFaults();
            LogAssert.NoUnexpectedReceived();
        }
        [Test]
        public void OriginalGlyphDisplayUsesGenuineTextAndRawImageSetterOrdering()
        {
            InputRemainingOriginalBehaviorVerification.VerifyGlyphDisplaySetterOrderAndFaults();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
