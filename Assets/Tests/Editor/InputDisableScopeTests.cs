using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    public sealed class InputDisableScopeTests
    {
        [Test]
        public void OriginalDisableScopesCallbacksAndInterruptedShutdownRemainIntact()
        {
            InputControlMappingVerification.VerifyDisableScopes();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
