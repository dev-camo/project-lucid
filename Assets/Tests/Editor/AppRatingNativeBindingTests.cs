using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    [TestFixture]
    public sealed class AppRatingNativeBindingTests
    {
        [Test]
        public void MacOSNativeBindingDeclarationRemainsPreserved()
        {
            AppRatingVerification.VerifyMacOSNativeDeclaration();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
