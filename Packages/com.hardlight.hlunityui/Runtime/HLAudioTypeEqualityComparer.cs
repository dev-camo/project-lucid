using System.Collections.Generic;

namespace Hardlight
{
    // Original HLUnityUI.Runtime 0x0200004b; this comparer is a stateless struct.
    // Native ARM64/x86_64 compares and returns the raw signed Int32 enum payload.
    public struct HLAudioTypeEqualityComparer : IEqualityComparer<HLAudioTypes>
    {
        // Original 0x06000243; unknown enum payloads are compared without validation.
        public bool Equals(HLAudioTypes x, HLAudioTypes y) => x == y;

        // Original 0x06000244; retain the original signed payload, including negatives.
        public int GetHashCode(HLAudioTypes obj) => (int)obj;
    }
}
