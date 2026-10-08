using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalEventSystemPreservationTests
    {
        [Test] public void OriginalEventSystemDeclarationsCachesAndDataBits() => Assert.AreEqual(94, OriginalEventSystemPreservationVerification.OriginalDeclarationsCachesAndDataBits());
        [Test] public void OriginalEventSystemValidationForwardingAndFaultOrder() => Assert.AreEqual(60, OriginalEventSystemPreservationVerification.OriginalValidationForwardingAndFaultOrder());
    }
}
