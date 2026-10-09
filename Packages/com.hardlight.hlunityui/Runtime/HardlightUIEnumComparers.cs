using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityUI.Runtime 0x0200004c. Retain an ordinary constructible class,
    // two public readonly registry fields and the original beforefieldinit flag.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HardlightUIEnumComparers
    {
        // Original 0x04000176/0x04000177; implicit .cctor 0x06000246 writes the
        // default value-type audio comparer before allocating the input comparer.
        public static readonly HLAudioTypeEqualityComparer HLAudioTypeComparer =
            new HLAudioTypeEqualityComparer();
        public static readonly UIInputTypeEqualityComparer UIInputTypeComparer =
            new UIInputTypeEqualityComparer();

        // Original 0x06000245; the instance constructor only calls Object.
        public HardlightUIEnumComparers() { }
    }
}
