using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLInput/GameInputGlyphMaps/Create DirectionGlyphMap")]
    public class DirectionGlyphMap : BaseGlyphMap<InputSwipe.Direction>
    {
        public DirectionGlyphMap() : base() { } // Original06000261, genuine whole ctor-only type.
    }
}
