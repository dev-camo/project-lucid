using NUnit.Framework;
using ProjectLucid.Editor;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalConeVolumeTests
    {
        [Test] public void OrdinaryDescriptionRetainsZeroInitializationAndValueCopies()
        { OriginalConeVolumeVerification.RunOrdinaryDescriptionValues(); }

        [Test] public void EllipseRetainsInclusiveBoundariesAndIgnoresLocalDepth()
        { OriginalConeVolumeVerification.RunEllipseBoundaryDepthAndTranslation(); }

        [Test] public void AngleTableRetainsEveryOriginalBitAndMutableArrayIdentity()
        { OriginalConeVolumeVerification.RunExactAngleTableAndMutableAlias(); }

        [Test] public void CacheReplacesWrongSizesAndRetainsArbitrarySixteenEntries()
        { OriginalConeVolumeVerification.RunCacheSizeReplacementAndSizedRetention(); }

        [Test] public void BoundsRetainAxisHalfTurnNegativeDistanceAndIgnoredStep()
        { OriginalConeVolumeVerification.RunBoundsAxisHalfTurnNegativeDistanceAndStep(); }

        [Test] public void BoundsConsumeCallerMutationsOfTheActualCachedArray()
        { OriginalConeVolumeVerification.RunBoundsConsumeMutableCachedAngles(); }

        [Test] public void BoundsRetainNonunitQuaternionWithoutNormalization()
        { OriginalConeVolumeVerification.RunBoundsRetainNonunitQuaternion(); }
    }
}
