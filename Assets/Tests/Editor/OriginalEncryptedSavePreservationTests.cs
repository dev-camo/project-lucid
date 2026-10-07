using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class OriginalEncryptedSavePreservationTests
    {
        [Test]
        public void OriginalEncryptedSaveRetainsIndependentCbcZeroPaddingVectors()
        {
            Assert.That(ProjectLucid.Editor.OriginalEncryptedSaveVerification.RunFixedVectors(), Is.EqualTo(12));
        }

        [Test]
        public void OriginalEncryptedSaveRetainsUtf8ConstructorBoundaries()
        {
            Assert.That(ProjectLucid.Editor.OriginalEncryptedSaveVerification.RunConstructorBoundaries(), Is.EqualTo(8));
        }

        [Test]
        public void OriginalEncryptedSaveRetainsDecryptCatchBoundaries()
        {
            Assert.That(ProjectLucid.Editor.OriginalEncryptedSaveVerification.RunDecryptCatchBoundaries(), Is.EqualTo(7));
        }

        [Test]
        public void OriginalPropertyStoreDefaultConstructorSelectsOfflineStorage()
        {
            Assert.That(ProjectLucid.Editor.OriginalEncryptedSaveVerification.RunDefaultOfflineRoute(), Is.EqualTo(5));
        }
    }
}
