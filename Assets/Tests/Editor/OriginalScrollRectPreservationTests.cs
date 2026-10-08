using NUnit.Framework;

namespace ProjectLucid.Verification.Tests
{
    [TestFixture]
    public sealed class OriginalScrollRectPreservationTests
    {
        [Test] public void OriginalScrollRectDeclarationsAndMarker() => Assert.AreEqual(18, ScrollRectPreservationVerification.OriginalDeclarations());
        [Test] public void OriginalScrollRectRubberDeltaFixedVectors() => Assert.AreEqual(18, ScrollRectPreservationVerification.RubberDeltaFixedVectors());
        [Test] public void OriginalScrollRectSmallContentTruthTable() => Assert.AreEqual(32, ScrollRectPreservationVerification.PublishedSmallContentPredicate());
        [Test] public void OriginalScrollRectClampAndFailurePrefixes() => Assert.AreEqual(162, ScrollRectPreservationVerification.PointerClampAndFailurePrefixes());
    }
}
