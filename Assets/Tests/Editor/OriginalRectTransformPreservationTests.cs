using NUnit.Framework;
using ProjectLucid.OriginalPreservation;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalRectTransformPreservationTests
    {
        [Test] public void RectTransformOriginalDeclarationsAndNullFailurePrefixes()
        { Assert.AreEqual(21, RectTransformPreservationVerification.ManagedDeclarationAndFailurePrefixes()); }
        [Test] public void RectTransformImmediateChildrenRetainsAllOriginalSiblingSlots()
        { Assert.AreEqual(5, RectTransformPreservationVerification.ImmediateChildrenRetainsSiblingOrderAndInactiveNullSlots()); }
        [Test] public void RectTransformFullscreenResetRetainsOriginalUnwrittenProperties()
        { Assert.AreEqual(7, RectTransformPreservationVerification.FullscreenResetPreservesPivotRotationAndScale()); }
        [Test] public void RectTransformEdgePointsRetainOriginalLocalGeometry()
        { Assert.AreEqual(9, RectTransformPreservationVerification.EdgePointsRetainLocalGeometryAndApproximateZeroDirection()); }
    }
}
