using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000854; the constructor 0x607940 only chains to ViewParameters.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ViewTargetParameters : ViewParameters
    {
        public Vector3? WorldspaceTarget;
    }
}
