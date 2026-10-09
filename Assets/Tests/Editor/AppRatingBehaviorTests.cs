using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    [TestFixture]
    public sealed class AppRatingBehaviorTests
    {
        [Test]
        public void UnsupportedProviderRemainsSelectedAndSilent()
        {
            AppRatingVerification.VerifyUnsupportedProviderAndSilence();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
