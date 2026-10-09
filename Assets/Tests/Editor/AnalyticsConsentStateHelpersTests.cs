using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests.EditMode
{
    [TestFixture]
    public sealed class AnalyticsConsentStateHelpersTests
    {
        [Test]
        public void OriginalLabelsComparersAndMutableCacheBehavior() =>
            AnalyticsEnumHelpersVerification.VerifyConsentState();
    }
}
