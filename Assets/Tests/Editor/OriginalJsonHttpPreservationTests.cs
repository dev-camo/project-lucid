using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalJsonHttpPreservationTests
    {
        [Test]
        public void OriginalJsonScalarConversionsRetainTypesDefaultsAndPoolSizes() =>
            Assert.That(JsonHttpPreservationVerification.ScalarConversions(), Is.EqualTo(34));
        [Test]
        public void OriginalJsonParserRetainsCursorsPermissiveInputAndFaults() =>
            Assert.That(JsonHttpPreservationVerification.ParserAndCursor(), Is.EqualTo(35));
        [Test]
        public void OriginalJsonSerializationRetainsOrderingEscapesAndDisposal() =>
            Assert.That(JsonHttpPreservationVerification.SerializationOrder(), Is.EqualTo(22));
        [Test]
        public void OriginalJsonContainersRetainOwnershipReuseAndReleaseOrder() =>
            Assert.That(JsonHttpPreservationVerification.ContainerOwnership(), Is.EqualTo(13));
        [Test]
        public void OriginalHttpNullRequestBoundariesRetainCallbacksAndIteratorFaults() =>
            Assert.That(JsonHttpPreservationVerification.HttpNullRequestBoundaries(), Is.EqualTo(21));
        [Test]
        public void JsonHttpPreservationFixturesRetainExistingStateAcrossRepeatsAndFaults() =>
            Assert.That(JsonHttpPreservationVerification.ExistingStateSurvivesRepeatedChecksAndFaults(), Is.EqualTo(17));
    }
}
