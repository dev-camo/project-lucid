using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    [TestFixture]
    public sealed class InputTypeResolverButtonTests
    {
        [Test]
        public void OriginalOverridesAndSignedJoystickFallbacksRemainIntact()
        {
            InputTypeResolverVerification.VerifyButtonResolution();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
