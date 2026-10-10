using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class HLHapticsTests
    {
        [Test] public void ConfigurationDefaultsAndLiteralNativeFiles() => HLHapticsVerification.ConfigurationDefaultsAndLiteralNativeFiles();
        [Test] public void ConfigurationJsonDuplicatesAndPrivateSerializedPath() => HLHapticsVerification.ConfigurationJsonDuplicatesAndPrivateSerializedPath();
        [Test] public void EventDataAndNestedVibrationsJsonRoundTrip() => HLHapticsVerification.EventDataAndNestedVibrationsJsonRoundTrip();
        [Test] public void InactiveManagerOwnedLifecycleDefaults() => HLHapticsVerification.InactiveManagerOwnedLifecycleDefaults();
        [Test] public void NaturalIteratorNonpositiveAndNullBoundaries() => HLHapticsVerification.NaturalIteratorNonpositiveAndNullBoundaries();
    }
}
