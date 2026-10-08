using NUnit.Framework;
using ProjectLucid.Verification;
namespace ProjectLucid.EditMode
{
 [TestFixture]
 public sealed class OriginalInertRibbonManagedTests
 {
  [Test] public void InertLutRetainsNullBoundaryAndLengthNoOps(){Assert.AreEqual(36,OriginalInertRibbonManagedVerification.VerifyLutAndInertLength());}
  [Test] public void AlignmentCacheRetainsFirstMatchMissesFaultsAndCenterForwarding(){Assert.AreEqual(46,OriginalInertRibbonManagedVerification.VerifyAlignmentCacheAndCenterForwarding());}
  [Test] public void SetTransformRetainsRawRotationAndOtherCapturedFields(){Assert.AreEqual(33,OriginalInertRibbonManagedVerification.VerifyTransformOnlyMutation());}
  [Test] public void SubSplineCopyRetainsBoundsReciprocalAndOriginalThrow(){Assert.AreEqual(43,OriginalInertRibbonManagedVerification.VerifySplineCopyCacheAndOriginalThrow());}
 }
}
