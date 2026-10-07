using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class OriginalStartingPositionPreservationTests
    {
        [Test] public void OriginalStartingPositionRetainsCompleteSerializedShape()
        { Assert.That(ProjectLucid.Editor.OriginalStartingPositionVerification.RunDeclarations(), Is.EqualTo(19)); }
        [Test] public void OriginalStartingPositionRetainsDefaultReferences()
        { Assert.That(ProjectLucid.Editor.OriginalStartingPositionVerification.RunDefaultBoundaries(), Is.EqualTo(3)); }
        [Test] public void OriginalStartingPositionRetainsAuthoredReferencesAcrossSetData()
        { Assert.That(ProjectLucid.Editor.OriginalStartingPositionVerification.RunAuthoredReferences(), Is.EqualTo(5)); }
    }
}
