using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000856; the constructor 0x60EE10 only chains to ViewTargetParameters.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ViewTargetIndicatorParameters : ViewTargetParameters
    {
        public Sprite TargetIndicator;
    }
}
