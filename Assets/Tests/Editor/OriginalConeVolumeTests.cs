using NUnit.Framework;
using ProjectLucid.Editor;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalConeVolumeTests
    {
        // Emission-regression check of the project's own compile, not original evidence.
        [Test] public void CurrentBuildEmissionMatchesRecordedSnapshot()
        { OriginalConeVolumeVerification.RunCurrentBuildEmissionMatchesRecordedSnapshot(); }

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
