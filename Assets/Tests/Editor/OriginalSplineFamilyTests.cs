using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests.EditMode
{
    [TestFixture]
    public sealed class OriginalSplineFamilyTests
    {
        [Test] public void SplineKnotsPreserveSerializedFieldsAndSignedLUTQueries()
            => Assert.AreEqual(35, OriginalSplineFamilyVerification.PlainKnotFieldsAndLUT());
        [Test] public void SplineKnotsRetainEightOriginalNotImplementedBodies()
            => Assert.AreEqual(11, OriginalSplineFamilyVerification.EightOriginalKnotThrows());
        [Test] public void SplineComponentsRetainDefaultsIndexFaultsAndParentCaching()
            => Assert.AreEqual(39, OriginalSplineFamilyVerification.ComponentDefaultsAndParentCache());
        [Test] public void SplineCopiesEvaluateAnalyticStraightGeometryAndWorldTransforms()
            => Assert.AreEqual(64, OriginalSplineFamilyVerification.CopiedLineGeometryAndWorldTransforms());
        [Test] public void SplineBoundsCacheAndLocalZDistanceRemainOriginal()
            => Assert.AreEqual(15, OriginalSplineFamilyVerification.BoundsCacheAndLocalDistanceQuirk());
        [Test] public void SplineEventsRetainTimestampAndDelegateFailurePrefixes()
            => Assert.AreEqual(19, OriginalSplineFamilyVerification.TimestampsAndDelegateFaultPrefixes());
        [Test] public void SplineFacadePublishesReplacementBeforeOriginalCallback()
            => Assert.AreEqual(19, OriginalSplineFamilyVerification.FacadePublicationAndOwnedCreation());
        [Test] public void SplineTrackerUsesTrueResolverAndRetainsCallbackFailureState()
            => Assert.AreEqual(15, OriginalSplineFamilyVerification.GenuineTrackerWorldAndFaultPrefix());
        [Test] public void SplineSamplingCacheRestoresEveryPriorBitAfterOwnedFaults()
            => Assert.AreEqual(7, OriginalSplineFamilyVerification.SharedSamplingCacheFaultRestoration());
    }
}
