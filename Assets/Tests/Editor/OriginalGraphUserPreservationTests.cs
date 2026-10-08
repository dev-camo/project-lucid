using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    public class OriginalGraphUserPreservationTests
    {
        [Test]
        public void TypedReferenceCastPreservesIdentityAndInvalidCastFault()
        { Assert.That(OriginalGraphUserVerification.RunReferenceCasts7(), Is.EqualTo(7)); }

        [Test]
        public void TypedValueCastPreservesUnboxingCopyAndNullFault()
        { Assert.That(OriginalGraphUserVerification.RunValueCasts5(), Is.EqualTo(5)); }
    }
}
