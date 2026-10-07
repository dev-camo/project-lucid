using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class OriginalAnimationCurvePreservationTests
    {
        [Test] public void OriginalCurveHelperRetainsBothDeclarationsAndDefaults()
        { Assert.That(ProjectLucid.Editor.OriginalAnimationCurveVerification.RunDeclarations(), Is.EqualTo(16)); }
        [Test] public void OriginalCurveHelperRetainsNullFailureOrdering()
        { Assert.That(ProjectLucid.Editor.OriginalAnimationCurveVerification.RunNullBoundaries(), Is.EqualTo(2)); }
        [Test] public void OriginalCurveHelperRetainsCurrentFinalKeyTimes()
        { Assert.That(ProjectLucid.Editor.OriginalAnimationCurveVerification.RunFinalKeyTimes(), Is.EqualTo(8)); }
        [Test] public void OriginalCurveAreaRetainsStepFormulaAndBoundaryDecisions()
        { Assert.That(ProjectLucid.Editor.OriginalAnimationCurveVerification.RunAreaArithmetic(), Is.EqualTo(20)); }
    }
}
