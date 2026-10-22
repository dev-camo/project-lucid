using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class DebugBackingFieldAttributeTests
    {
        [Test]
        public void DebugOnlyAllowanceForCompilerDebuggerBrowsableIsExactAndReleaseStaysStrict()
        {
            Assert.That(DebugBackingFieldVerification.Run(), Is.EqualTo(20));
        }
    }
}
