using NUnit.Framework;
using ProjectLucid.Editor;

namespace ProjectLucid.Tests
{
    public sealed class OriginalObjectExtensionsPreservationTests
    {
        [Test]
        public void OriginalObjectExtensionsRetainCompleteDeclarations()
        {
            Assert.That(OriginalObjectExtensionsVerification.RunOriginalCompleteDeclarations(), Is.EqualTo(55));
        }

        [Test]
        public void OriginalComponentQueriesRetainMissingAndInactiveResults()
        {
            Assert.That(OriginalObjectExtensionsVerification.RunOriginalComponentQueries(), Is.EqualTo(7));
        }

        [Test]
        public void OriginalGameObjectQueriesRetainCreationAndExistingIdentity()
        {
            Assert.That(OriginalObjectExtensionsVerification.RunOriginalGameObjectQueriesAndCreation(), Is.EqualTo(10));
        }

        [Test]
        public void OriginalImmediateObjectDestructionRetainsUnityWrappers()
        {
            Assert.That(OriginalObjectExtensionsVerification.RunOriginalImmediateDestruction(), Is.EqualTo(4));
        }

        [Test]
        public void OriginalChildRemovalRetainsReverseHierarchyBehavior()
        {
            Assert.That(OriginalObjectExtensionsVerification.RunOriginalReverseChildRemoval(), Is.EqualTo(4));
        }

        [Test]
        public void OriginalComponentRemovalRetainsRequirementsAndExclusions()
        {
            Assert.That(OriginalObjectExtensionsVerification.RunOriginalRequiredComponentRemoval(), Is.EqualTo(6));
        }

        [Test]
        public void OriginalHierarchyPathRetainsNamesAndCurrentParents()
        {
            Assert.That(OriginalObjectExtensionsVerification.RunOriginalHierarchyPath(), Is.EqualTo(4));
        }
    }
}
