using System;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000ab2: a ribbon-specific subtype with no members of its own;
    // the only body is the inherited default constructor.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class RibbonPathSineCurveGenerator : SurfacePathSineCurveGenerator
    {
    }
}
