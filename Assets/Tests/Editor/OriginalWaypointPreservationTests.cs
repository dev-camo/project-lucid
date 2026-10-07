using NUnit.Framework;
using ProjectLucid.Editor;

namespace ProjectLucid.Tests
{
    public sealed class OriginalWaypointPreservationTests
    {
        [Test]
        public void OriginalWaypointDeclarationsPreserveFieldsOptionsAndCallbackContract()
        {
            Assert.That(OriginalWaypointVerification.RunOriginalDeclarations(), Is.EqualTo(40));
        }

        [Test]
        public void OriginalWaypointsPreserveOverridesDeferredLifecycleAndFailureOrdering()
        {
            Assert.That(OriginalWaypointVerification.RunOriginalEngineBoundaries(), Is.EqualTo(33));
        }
    }
}
