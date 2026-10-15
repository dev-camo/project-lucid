using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class HLHapticsTests
    {
        // Current-build snapshot gate, kept apart so behavior cases do not fail on unrelated source edits.
        [Test] public void CurrentDeclarationsBodiesAndProviderSourcePins() => HLHapticsVerification.CurrentDeclarationsBodiesAndProviderSourcePins();
        [Test] public void ConfigurationDefaultsAndLiteralNativeFiles() => HLHapticsVerification.ConfigurationDefaultsAndLiteralNativeFiles();
        [Test] public void ConfigurationJsonDuplicatesAndPrivateSerializedPath() => HLHapticsVerification.ConfigurationJsonDuplicatesAndPrivateSerializedPath();
        [Test] public void EventDataAndNestedVibrationsJsonRoundTrip() => HLHapticsVerification.EventDataAndNestedVibrationsJsonRoundTrip();
        [Test] public void InactiveManagerOwnedLifecycleDefaults() => HLHapticsVerification.InactiveManagerOwnedLifecycleDefaults();
        [Test] public void NaturalIteratorNonpositiveAndNullBoundaries() => HLHapticsVerification.NaturalIteratorNonpositiveAndNullBoundaries();
    }
}
