using NUnit.Framework;
using ProjectLucid.Editor;

namespace ProjectLucid.Tests
{
    public sealed class OriginalSaveRecordMergePreservationTests
    {
        [Test]
        public void OriginalSaveRecordsRetainCompleteDeclarationsAndSignedDefault()
        { Assert.That(OriginalSaveRecordMergeVerification.RunOriginalDeclarationsAndDefault(), Is.EqualTo(90)); }

        [Test]
        public void OriginalUnlockAndSeenMergesRetainFlagsAndRawDirtyState()
        { Assert.That(OriginalSaveRecordMergeVerification.RunUnlockAndSeenMerges(), Is.EqualTo(188)); }

        [Test]
        public void OriginalStatsRetainZeroSignedOverflowAndMaximumMerge()
        { Assert.That(OriginalSaveRecordMergeVerification.RunSignedStatAndCollectableBoundaries(), Is.EqualTo(69)); }

        [Test]
        public void OriginalBonusAndStoreStatesRetainUnknownEnumTransitions()
        { Assert.That(OriginalSaveRecordMergeVerification.RunOriginalStateTransitions(), Is.EqualTo(160)); }

        [Test]
        public void OriginalCustomisationRetainsManagedGUIDFaultOrdering()
        { Assert.That(OriginalSaveRecordMergeVerification.RunCustomisationManagedFaults(), Is.EqualTo(9)); }

        [Test]
        public void OriginalCustomisationRetainsActualUnityDestroyedObjectGUIDPath()
        { Assert.That(OriginalSaveRecordMergeVerification.RunCustomisationUnityGUIDBoundaries(), Is.EqualTo(5)); }

        [Test]
        public void OriginalStoreCreationRetainsIdentityNullKeysAndDirtyState()
        { Assert.That(OriginalSaveRecordMergeVerification.RunStoreCreationAndDirtyBoundaries(), Is.EqualTo(13)); }

        [Test]
        public void OriginalStoreMergeRetainsAliasingSelfMergeAndPartialFaults()
        { Assert.That(OriginalSaveRecordMergeVerification.RunStoreMergeAliasingAndPartialFaults(), Is.EqualTo(17)); }

        [Test]
        public void OriginalStoreCallbacksRetainDuplicatesAndFailurePrefixes()
        { Assert.That(OriginalSaveRecordMergeVerification.RunStoreSerializationCallbacks(), Is.EqualTo(17)); }

        [Test]
        public void OriginalSaveRecordsRetainActualUnitySerializationBoundaries()
        { Assert.That(OriginalSaveRecordMergeVerification.RunUnitySerializationBoundaries(), Is.EqualTo(11)); }
    }
}
