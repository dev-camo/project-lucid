using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a2d; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SplineObjectFollower : SurfaceObjectFollower
    {
        [SerializeField]
        private Spline m_attachedSpline;

        protected override ISurface GetSurface()
        {
            return m_attachedSpline.GetSpline();
        }
    }
}
