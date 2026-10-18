using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a1f; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class RibbonObjectFollower : SurfaceObjectFollower
    {
        [SerializeField]
        private Ribbon m_attachedRibbon;

        protected override ISurface GetSurface()
        {
            return m_attachedRibbon.GetRibbon();
        }
    }
}
