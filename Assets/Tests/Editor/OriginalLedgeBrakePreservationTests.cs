using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class OriginalLedgeBrakePreservationTests
    {
        [Test]
        public void OriginalLedgeBrakingCachesRetainAuthoredAnglesAndExceptionalValues()
        {
            Assert.That(ProjectLucid.Editor.OriginalLedgeBrakeVerification.RunOriginalBoundaries(), Is.EqualTo(34));
        }
    }
}
