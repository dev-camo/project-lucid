using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalCharacterRigPreservationTests
    {
        [Test] public void OriginalRigCacheRetainsExactTypesDuplicateOrderAndPartialFailures() =>
            Assert.That(CharacterRigPreservationVerification.CacheCorrespondence(), Is.EqualTo(18));
        [Test] public void OriginalRigAttachmentsRetainWorldPoseAndReverseSiblingOrder() =>
            Assert.That(CharacterRigPreservationVerification.AttachmentParenting(), Is.EqualTo(18));
        [Test] public void OriginalRigPositionsRetainLocalPoseScaleAndAnimatorOrder() =>
            Assert.That(CharacterRigPreservationVerification.RigLocalPoses(), Is.EqualTo(14));
        [Test] public void OriginalRigGhostMaterialsRetainNamesIndexedListsAndPartialFailures() =>
            Assert.That(CharacterRigPreservationVerification.GhostMaterialCorrespondence(), Is.EqualTo(9));
        [Test] public void OriginalSubstitutionRetainsWorldPoseActivationAndFaultOrder() =>
            Assert.That(CharacterRigPreservationVerification.SubstituteWorldPose(), Is.EqualTo(16));
        [Test] public void OriginalPersistentSubstitutionRetainsParentAndBypassesRestoration() =>
            Assert.That(CharacterRigPreservationVerification.SubstitutePersistence(), Is.EqualTo(13));
    }
}
