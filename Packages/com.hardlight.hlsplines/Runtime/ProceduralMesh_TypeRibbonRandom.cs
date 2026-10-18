using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x0200004e; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class ProceduralMesh_TypeRibbonRandom : ProceduralMesh_TypeRandom
    {
        [SerializeField]
        private float m_biasMin;

        [SerializeField]
        private float m_biasMax;

        [SerializeField]
        private LocationGenerator.BiasType m_biasType;

        [SerializeField]
        private LocationGenerator.BiasDensity m_biasDensity;

        [SerializeField]
        private bool m_inverseBias;

        public float BiasMin => m_biasMin;

        public float BiasMax => m_biasMax;

        public LocationGenerator.BiasType BiasType => m_biasType;

        public LocationGenerator.BiasDensity BiasDensity => m_biasDensity;

        public bool InverseBias => m_inverseBias;

        // 06000187 (1.10.1 RVA 0x1A5E0E0). Declared explicitly: the bias defaults are
        // stored after the inlined base constructors (0x1A5E0FE..0x1A5E10B);
        // m_inverseBias is never written.
        public ProceduralMesh_TypeRibbonRandom()
        {
            m_biasMin = -1f;
            m_biasMax = 1f;
            m_biasType = LocationGenerator.BiasType.WidthRatio;
            m_biasDensity = LocationGenerator.BiasDensity.Even;
        }
    }
}
