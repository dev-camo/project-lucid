using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class HLInputPathConstants : MonoBehaviour
    {
        public const string ModuleMenuBase = "Hardlight/HLInput/";
        public const string ControllerProviders = "ControllerProviders/";
        public const string GameInputGlyphMaps = "GameInputGlyphMaps/";
        public const string Modifiers = "Modifiers/";
        public HLInputPathConstants() { } // 06000106: original MonoBehaviour, not static utility.
    }
}
