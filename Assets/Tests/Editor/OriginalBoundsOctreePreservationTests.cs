using NUnit.Framework;
using ProjectLucid.Editor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    public sealed class OriginalBoundsOctreePreservationTests
    {
        [Test] public void OriginalOctreeDeclarationsRemainComplete() { Assert.AreEqual(42, OriginalBoundsOctreeVerification.VerifyOriginalDeclarations()); }
        [Test] public void OriginalOctantsRetainEqualityAndUnorderedChoices() { Assert.AreEqual(155, OriginalBoundsOctreeVerification.VerifyOctantComparisons()); }
        [Test] public void OriginalConstructionRetainsBoundsAndChildAliases()
        {
            LogAssert.Expect(LogType.Warning, "Minimum node size must be at least as big as the initial world size. Was: 20 Adjusted to: 10");
            LogAssert.Expect(LogType.Error, "Child octree array must be length 8. Was length: 7");
            Assert.AreEqual(31, OriginalBoundsOctreeVerification.VerifyConstructionAndChildren());
        }
        [Test] public void OriginalInsertionAndCollisionQueriesRetainOrderingAndFaults() { Assert.AreEqual(25, OriginalBoundsOctreeVerification.VerifyInsertionAndQueries()); }
        [Test] public void OriginalReverseMigrationRetainsEqualityFailureState() { Assert.AreEqual(9, OriginalBoundsOctreeVerification.VerifyMigrationFailureOrdering()); }
        [Test] public void OriginalGrowthRetainsObjectsAndBoundedAbortMutations()
        {
            LogAssert.Expect(LogType.Error, "Aborted Add operation as it seemed to be going on forever (20) attempts at growing the octree.");
            Assert.AreEqual(11, OriginalBoundsOctreeVerification.VerifyGrowthAndAbort());
        }
        [Test] public void OriginalFrustumQueriesRetainGeometryAndPartialResults() { Assert.AreEqual(8, OriginalBoundsOctreeVerification.VerifyFrustumQueries()); }
    }
}
