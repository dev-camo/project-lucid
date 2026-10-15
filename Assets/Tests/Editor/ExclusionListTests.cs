using NUnit.Framework;
using UnityEngine.TestTools;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    // Future exact Unity EditMode cases against the complete genuine original Core provider.
    [TestFixture]
    public sealed class ExclusionListTests
    {
        // Emission-regression check of the project's own compile, not original evidence.
        [Test] public void CurrentBuildEmissionMatchesRecordedSnapshot() { ExclusionListVerification.VerifyCurrentBuildEmissionMatchesRecordedSnapshot(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void ConstructorDefaultsAndWriteOnlyEnabled() { ExclusionListVerification.VerifyConstructorDefaultsAndWriteOnlyEnabled(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void DisabledShortCircuitBeforeNullInputs() { ExclusionListVerification.VerifyDisabledShortCircuitBeforeNullInputs(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void EnabledArrayAndMessageFaultBoundaries() { ExclusionListVerification.VerifyEnabledArrayAndMessageFaultBoundaries(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void PartialCaseSensitiveAndEmptyNeedles() { ExclusionListVerification.VerifyPartialCaseSensitiveAndEmptyNeedles(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void IndexedShortCircuitAndNullNeedleOrder() { ExclusionListVerification.VerifyIndexedShortCircuitAndNullNeedleOrder(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void LiveArrayReplacementAndIndependentInstances() { ExclusionListVerification.VerifyLiveArrayReplacementAndIndependentInstances(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void OriginalPublicFieldJsonOverwrite() { ExclusionListVerification.VerifyOriginalPublicFieldJsonOverwrite(); LogAssert.NoUnexpectedReceived(); }
    }
}
