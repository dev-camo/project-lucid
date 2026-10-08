using NUnit.Framework;

namespace ProjectLucid.Verification
{
    public class OriginalRibbonVariantsTests
    {
        [TestCase(false, TestName = "OriginalInertCapturedCurveGeometry")]
        [TestCase(true, TestName = "OriginalSegmentedCapturedCurveGeometry")]
        public void OriginalCapturedCurveGeometry(bool segmented) =>
            Assert.That(OriginalRibbonVariantsVerification.RunOriginalGeometry(segmented), Is.EqualTo(20));

        [TestCase(false, TestName = "OriginalInertCachesAndDistinctIndexRules")]
        [TestCase(true, TestName = "OriginalSegmentedCachesAndDistinctIndexRules")]
        public void OriginalCachesAndDistinctIndexRules(bool segmented) =>
            Assert.That(OriginalRibbonVariantsVerification.RunCacheAndKnotRules(segmented), Is.EqualTo(17));

        [TestCase(false, TestName = "OriginalInertSerializedAndActualTransforms")]
        [TestCase(true, TestName = "OriginalSegmentedSerializedAndActualTransforms")]
        public void OriginalSerializedAndActualTransforms(bool segmented) =>
            Assert.That(OriginalRibbonVariantsVerification.RunTransformsAndBounds(segmented), Is.EqualTo(11));

        [TestCase(false, TestName = "OriginalInertNotificationPublicationOrder")]
        [TestCase(true, TestName = "OriginalSegmentedNotificationPublicationOrder")]
        public void OriginalNotificationPublicationOrder(bool segmented) =>
            Assert.That(OriginalRibbonVariantsVerification.RunEventOrdering(segmented), Is.EqualTo(14));

        [Test]
        public void OriginalFacadeSelectsStoredVariant() =>
            Assert.That(OriginalRibbonVariantsVerification.RunFacadeSelection(), Is.EqualTo(5));

        [Test]
        public void OriginalFacadeCreatesAndRetainsOriginalChild() =>
            Assert.That(OriginalRibbonVariantsVerification.RunFacadeCreation(), Is.EqualTo(12));

        [Test]
        public void OriginalFacadePublishesBeforeOldCallback() =>
            Assert.That(OriginalRibbonVariantsVerification.RunFacadePublication(), Is.EqualTo(7));
    }
}
