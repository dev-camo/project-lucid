using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    [TestFixture]
    public sealed class InputUtilitiesMappingTests
    {
        [Test]
        public void OriginalJoystickAxisAndMouseMappingsRemainIntact()
        {
            InputUtilitiesVerification.VerifyOriginalMappings();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
