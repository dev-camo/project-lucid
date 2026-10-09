using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests.EditMode
{
    [TestFixture]
    public sealed class AnalyticsMissionTypeHelpersTests
    {
        [Test]
        public void OriginalLabelsComparersAndMutableCacheBehavior() =>
            AnalyticsEnumHelpersVerification.VerifyMissionType();
    }
}
