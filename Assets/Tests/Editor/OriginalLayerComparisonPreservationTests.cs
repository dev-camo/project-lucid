using NUnit.Framework;
namespace ProjectLucid.Tests
{
    public sealed class OriginalLayerComparisonPreservationTests
    {
        [Test] public void OriginalLayerAndComparisonOwnersRetainAllDeclarations()
        { Assert.That(ProjectLucid.Editor.OriginalLayerComparisonVerification.RunOriginalDeclarations(), Is.EqualTo(42)); }
        [Test] public void OriginalLayerOperationsRetainSignedAndWrappedBits()
        { Assert.That(ProjectLucid.Editor.OriginalLayerComparisonVerification.RunFixedLayerBitVectors(), Is.EqualTo(91)); }
        [Test] public void OriginalLayerObjectOverloadReadsRealAuthoredLayers()
        { Assert.That(ProjectLucid.Editor.OriginalLayerComparisonVerification.RunAuthoredGameObjectLayers(), Is.EqualTo(14)); }
        [Test] public void OriginalFloatComparisonsRetainApproximationAndExceptionalValues()
        { Assert.That(ProjectLucid.Editor.OriginalLayerComparisonVerification.RunFixedFloatComparisons(), Is.EqualTo(154)); }
        [Test] public void OriginalIntegerComparisonsRetainSignedEndpoints()
        { Assert.That(ProjectLucid.Editor.OriginalLayerComparisonVerification.RunFixedSignedIntegerComparisons(), Is.EqualTo(84)); }
        [Test] public void OriginalUnknownComparisonsRetainTypedErrorsAndMessages()
        { Assert.That(ProjectLucid.Editor.OriginalLayerComparisonVerification.RunOriginalUnknownOperationErrors(), Is.EqualTo(18)); }
    }
}
