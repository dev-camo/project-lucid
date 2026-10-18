using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace HardlightProject
{
    // Original Game.Runtime 02000aa0; the only body is the constructor (06003d29, 0x6D31B0),
    // which creates the path list before the MonoBehaviour constructor.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class SurfaceGenerator : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The ribbon to generate knots and their transforms along.")]
        protected Ribbon m_ribbon;

        [Tooltip("The spline to generate knots and their transforms along.")]
        [SerializeField]
        protected Spline m_spline;

        [SerializeReference]
        [FormerlySerializedAs("m_ribbonPaths")]
        [Tooltip("Paths to generate knots from.")]
        private List<SurfacePathGenerator> m_surfacePaths = new List<SurfacePathGenerator>();
    }
}
