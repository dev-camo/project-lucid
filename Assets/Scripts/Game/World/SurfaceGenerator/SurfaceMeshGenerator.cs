using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000aa6; the only body is the constructor (06003d49, 0x6D4D40),
    // which creates the left and right section lists.
    [ExecuteInEditMode]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SurfaceMeshGenerator : SurfaceGenerator
    {
        [SerializeField]
        [Tooltip("Material to assign to the mesh.")]
        private Material m_material;

        [SerializeField]
        private bool m_doubleSided;

        [SerializeReference]
        private SurfaceMeshCrossSectionGenerator m_centreCrossSectionGenerator;

        [SerializeReference]
        private List<SurfaceMeshCrossSectionGenerator> m_leftCrossSections = new List<SurfaceMeshCrossSectionGenerator>();

        [SerializeReference]
        private List<SurfaceMeshCrossSectionGenerator> m_rightCrossSections = new List<SurfaceMeshCrossSectionGenerator>();
    }
}
