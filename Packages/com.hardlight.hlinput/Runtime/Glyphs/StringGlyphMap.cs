using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLInput/GameInputGlyphMaps/Create StringGlyphMap")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class StringGlyphMap : BaseGlyphMap<string>
    {
        // HLInput.Runtime 0x06000276: the original fieldless concrete map has
        // only the natural BaseGlyphMap<string> constructor. Native ARM64
        // 0x1a387a4 initializes its generic context and tail-calls that base.
    }
}
