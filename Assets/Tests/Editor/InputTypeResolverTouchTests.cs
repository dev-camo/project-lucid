using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    [TestFixture]
    public sealed class InputTypeResolverTouchTests
    {
        [Test]
        public void OriginalTouchFlagResolvesWithoutAddingPlatformDetection()
        {
            InputTypeResolverVerification.VerifyTouchResolution();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
