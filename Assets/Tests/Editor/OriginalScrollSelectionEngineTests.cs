using NUnit.Framework;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalScrollSelectionEngineTests
    {
        [Test] public void OriginalScrollSelectionActualConstructorsPoliciesAndNullSelection()
            => Assert.AreEqual(34, Verification.OriginalScrollSelectionEngineVerification.OriginalRealConstructorsPoliciesAndNullSelection());
        [Test] public void OriginalScrollSelectionActualSignedGeometryAndAlignmentCases()
            => Assert.AreEqual(41, Verification.OriginalScrollSelectionEngineVerification.OriginalSignedGeometryAndAllAlignmentCases());
        [Test] public void OriginalScrollSelectionActualInstantCallbackAndPartialFailureOrder()
            => Assert.AreEqual(21, Verification.OriginalScrollSelectionEngineVerification.OriginalInstantCallbackAndPartialFailureOrder());
    }
}
