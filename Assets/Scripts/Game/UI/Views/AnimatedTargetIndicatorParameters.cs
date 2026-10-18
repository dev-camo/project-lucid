using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000833; the constructor 0x607930 only chains to ViewTargetParameters.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AnimatedTargetIndicatorParameters : ViewTargetParameters
    {
        public GameObject OriginTarget;
    }
}
