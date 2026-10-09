using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests.EditMode
{
    [TestFixture]
    public sealed class OriginalEnumPreservationTests
    {
        [Test] public void ParseRetainsCaseSensitiveNamesAndNumericValues() => Assert.AreEqual(16, OriginalEnumPreservationVerification.ParseRetainsCaseSensitiveNamesAndNumericValues());
        [Test] public void SafeParseRetainsFallbackAcrossParseAndTypeFaults() => Assert.AreEqual(12, OriginalEnumPreservationVerification.SafeParseRetainsFallbackAcrossParseAndTypeFaults());
        [Test] public void NamesAndValuesRetainAliasesAndIndependentArrays() => Assert.AreEqual(30, OriginalEnumPreservationVerification.NamesAndValuesRetainAliasesAndIndependentArrays());
        [Test] public void IntegerValuesRetainSignedConversionAndOverflow() => Assert.AreEqual(34, OriginalEnumPreservationVerification.IntegerValuesRetainSignedConversionAndOverflow());
        [Test] public void MembershipRequiresDefinedEnumValues() => Assert.AreEqual(9, OriginalEnumPreservationVerification.MembershipRequiresDefinedEnumValues());
        [Test] public void ConversionRetainsOutValueAndFaultPrefixes() => Assert.AreEqual(16, OriginalEnumPreservationVerification.ConversionRetainsOutValueAndFaultPrefixes());
        [Test] public void OriginalConstraintsRemainObservable() => Assert.AreEqual(25, OriginalEnumPreservationVerification.OriginalConstraintsRemainObservable());
    }
}
