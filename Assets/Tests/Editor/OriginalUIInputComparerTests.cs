using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    public sealed class OriginalUIInputComparerTests
    {
        [Test]
        public void InputOverridesSurviveSerializedRoundTrip()
        {
            UIInputComparerPreservationVerification.InputOverridesSurviveSerializedRoundTrip();
        }

        [Test]
        public void UnknownSignedKeysRemainDistinct()
        {
            UIInputComparerPreservationVerification.UnknownSignedKeysRemainDistinct();
        }
    }
}
