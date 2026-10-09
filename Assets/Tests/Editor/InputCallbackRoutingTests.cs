using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    public sealed class InputCallbackRoutingTests
    {
        [Test]
        public void OriginalDeviceRoutingFrameConsumptionAndSubscriptionLifetimesRemainIntact()
        {
            InputControlMappingVerification.VerifyCallbackRouting();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
