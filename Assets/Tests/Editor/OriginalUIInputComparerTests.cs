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

        // x86-64 bodies: UIInputTypeEqualityComparer.Equals 0x060002fb 0x1b4fea0 is
        // cmp esi, edx / sete and GetHashCode 0x060002fc 0x1b4feb0 is mov eax, esi;
        // HLAudioTypeEqualityComparer 0x06000243/44 at 0x1b4ada0/0x1b4adb0 are identical.
        // Any payload, listed literal or not, is therefore its own hash and key.
        [Test]
        public void UnknownSignedKeysRemainDistinct()
        {
            UIInputComparerPreservationVerification.UnknownSignedKeysRemainDistinct();
        }
    }
}
