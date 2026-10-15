using NUnit.Framework;
using ProjectLucid.Editor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Shipped HLPhysics.Runtime rows: BoundsOctree`1 0x02000003 (fields 0x04000001..05,
    // methods 0x06000002..08) and BoundsOctreeNode`1 0x02000004 (fields 0x04000006..0f,
    // NUM_OBJECTS_ALLOWED default 8; methods 0x06000009..18). x86-64 addresses below are
    // the shared (_gshared) instantiation each outer method calls.
    public sealed class OriginalBoundsOctreePreservationTests
    {
        [Test] public void OriginalOctreeDeclarationsRemainComplete()
        {
            Assert.AreEqual(42, OriginalBoundsOctreeVerification.VerifyOriginalDeclarations());
            // Names, raw attributes and parameter names of all 23 methods in token order.
            Assert.AreEqual(151, OriginalBoundsOctreeNativeGeometryVerification.VerifyMethodRows());
        }
        [Test] public void OriginalOctantsRetainEqualityAndUnorderedChoices() { Assert.AreEqual(155, OriginalBoundsOctreeVerification.VerifyOctantComparisons()); }
        // .ctor 0x06000004 x86-64 0xe42dd0 builds the warning from literals "Minimum node size ...
        // Was: " / " Adjusted to: " and clamps looseness with minss 2.0 then cmpltss 1.0;
        // SetValues 0x06000015 0xe3f640 stores its arguments with no validation, so a
        // negative BaseLength stays. SetChildren 0x06000012 0xe3f2f0 compares Length with 8
        // after a null fault and logs "Child octree array must be length 8. Was length: ".
        [Test] public void OriginalConstructionRetainsBoundsAndChildAliases()
        {
            LogAssert.Expect(LogType.Warning, "Minimum node size must be at least as big as the initial world size. Was: 20 Adjusted to: 10");
            LogAssert.Expect(LogType.Error, "Child octree array must be length 8. Was length: 7");
            Assert.AreEqual(31, OriginalBoundsOctreeVerification.VerifyConstructionAndChildren());
            Assert.AreEqual(3, OriginalBoundsOctreeNativeGeometryVerification.VerifyUnorderedMinimumSkipsWarning());
        }
        // GetColliding 0x06000010 x86-64 0xe3ea30 tests node bounds, then each object, and
        // only then dereferences the result; children follow in fixed slots 0..7.
        [Test] public void OriginalInsertionAndCollisionQueriesRetainOrderingAndFaults()
        {
            Assert.AreEqual(25, OriginalBoundsOctreeVerification.VerifyInsertionAndQueries());
            Assert.AreEqual(4, OriginalBoundsOctreeNativeGeometryVerification.VerifyObjectFilterPrecedesResultAccess());
        }
        // SubAdd 0x06000016 x86-64 0xe3f980 walks m_objects from Count-1 down, calls the
        // child's SubAdd (0xe3fe49) and then List`1.Remove(existingObj) (0xe3feaf), so the
        // 36 equality calls and the partial failure state follow from Remove's linear search.
        [Test] public void OriginalReverseMigrationRetainsEqualityFailureState() { Assert.AreEqual(9, OriginalBoundsOctreeVerification.VerifyMigrationFailureOrdering()); }
        // Add 0x06000005 x86-64 0xe42f80 aborts with "Aborted Add operation as it seemed to be
        // going on forever (" + 20 + ") attempts at growing the octree." after Grow 0xe43210.
        [Test] public void OriginalGrowthRetainsObjectsAndBoundedAbortMutations()
        {
            LogAssert.Expect(LogType.Error, "Aborted Add operation as it seemed to be going on forever (20) attempts at growing the octree.");
            Assert.AreEqual(11, OriginalBoundsOctreeVerification.VerifyGrowthAndAbort());
            Assert.AreEqual(31, OriginalBoundsOctreeNativeGeometryVerification.VerifyGrowthSiblingGeometry());
        }
        [Test] public void OriginalFrustumQueriesRetainGeometryAndPartialResults() { Assert.AreEqual(8, OriginalBoundsOctreeVerification.VerifyFrustumQueries()); }
    }
}
