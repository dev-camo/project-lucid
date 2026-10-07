using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class OriginalSerializedDictionaryFamilyPreservationTests
    {
        [Test] public void OriginalDictionaryGenericShapeAndAttributesRemainComplete()
        { Assert.That(ProjectLucid.Editor.OriginalSerializedDictionaryFamilyVerification.RunOriginalMetadata(), Is.EqualTo(20)); }
        [Test] public void OriginalReferenceDictionaryConversionsPreserveNullsAndValueTypes()
        { Assert.That(ProjectLucid.Editor.OriginalSerializedDictionaryFamilyVerification.RunReferenceConversions(), Is.EqualTo(14)); }
        [Test] public void OriginalListPairRetainsMutationOrderAndCopyBoundaries()
        { Assert.That(ProjectLucid.Editor.OriginalSerializedDictionaryFamilyVerification.RunListPairMutationAndCopies(), Is.EqualTo(14)); }
        [Test] public void OriginalListDictionaryPreservesCopiesAndPartialFailureState()
        { Assert.That(ProjectLucid.Editor.OriginalSerializedDictionaryFamilyVerification.RunListDictionaryOrdering(), Is.EqualTo(12)); }
    }
}
