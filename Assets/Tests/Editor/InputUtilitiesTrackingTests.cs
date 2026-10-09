using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    [TestFixture]
    public sealed class InputUtilitiesTrackingTests
    {
        [Test]
        public void TrackingCachesPreservePublicationReentrancyAndFailureOrder()
        {
            InputUtilitiesVerification.VerifyTrackingCacheAndFailures();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
