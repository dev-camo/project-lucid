using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class OriginalCharacterTransformPreservationTests
    {
        [Test] public void OriginalReversalPredicatesRetainStrictNegativeMath()
        { Assert.That(ProjectLucid.Editor.OriginalCharacterTransformVerification.RunReversalMath(), Is.EqualTo(22)); }

        [Test] public void OriginalReversalChunkRetainsDocumentedDeclarationScope()
        { Assert.That(ProjectLucid.Editor.OriginalCharacterTransformVerification.RunReversalDeclarations(), Is.EqualTo(12)); }

        [Test] public void OriginalTransformUtilityRetainsAllElevenDeclarations()
        { Assert.That(ProjectLucid.Editor.OriginalCharacterTransformVerification.RunTransformDeclarations(), Is.EqualTo(28)); }

        [Test] public void OriginalTransformLocalOperationsRetainCopyResetAndParentValues()
        { Assert.That(ProjectLucid.Editor.OriginalCharacterTransformVerification.RunLocalTransformOperations(), Is.EqualTo(9)); }

        [Test] public void OriginalTransformHierarchyRetainsAncestorPathsAndWholeVectorScale()
        { Assert.That(ProjectLucid.Editor.OriginalCharacterTransformVerification.RunHierarchyAndScale(), Is.EqualTo(14)); }

        [Test] public void OriginalTransformGeometryRetainsLocalRectsAndFourScreenCorners()
        { Assert.That(ProjectLucid.Editor.OriginalCharacterTransformVerification.RunBoundingAndScreenGeometry(), Is.EqualTo(8)); }

        [Test] public void OriginalTransformChildrenRetainCollectionAndSiblingSortOrdering()
        { Assert.That(ProjectLucid.Editor.OriginalCharacterTransformVerification.RunImmediateChildrenAndSorting(), Is.EqualTo(10)); }

        [Test] public void OriginalTransformNullAndDestroyedObjectsRetainBranchBoundaries()
        { Assert.That(ProjectLucid.Editor.OriginalCharacterTransformVerification.RunNullAndDestroyedBoundaries(), Is.EqualTo(14)); }
    }
}
