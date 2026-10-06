using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class PerformanceAttribute
    {
        public ScalableFeature Feature;
        public PerformanceProfile.QualityLevel Level;
        // HLUnityCore.Runtime 0x06000d6a: native object-base-only constructor.
    }
}
