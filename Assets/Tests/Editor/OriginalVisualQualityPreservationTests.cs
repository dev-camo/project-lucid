using NUnit.Framework;

namespace ProjectLucid.Verification
{
    public sealed class OriginalVisualQualityPreservationTests
    {
        [Test] public void OriginalConfigurationConstructorsPreserveAuthoredDefaults() =>
            Assert.AreEqual(18, OriginalVisualQualityPreservationVerification.RunConstructorDefaults18());
        [Test] public void OriginalConfigurationSelectionPreservesFirstFallbackAndFaultOrder() =>
            Assert.AreEqual(20, OriginalVisualQualityPreservationVerification.RunConfigurationSelection20());
        [Test] public void OriginalProfilesPreserveGuidEqualityAndLiveAliases() =>
            Assert.AreEqual(28, OriginalVisualQualityPreservationVerification.RunProfileIdentityEngine28());
        [Test] public void OriginalProfilesPreserveNestedCallbackSnapshotsAndLiveIdentity() =>
            Assert.AreEqual(24, OriginalVisualQualityPreservationVerification.RunProfileReentryEngine24());
        [Test] public void OriginalProfilesPreserveCallbackAndNullTransitionFaultPrefixes() =>
            Assert.AreEqual(16, OriginalVisualQualityPreservationVerification.RunProfileFaultPrefixesEngine16());
    }
}
