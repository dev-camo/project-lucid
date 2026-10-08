using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // HLUnityCore.Runtime original 0200021b; own methods 06000da7..06000da9.
    // Original abstract Apply is an authored contract, with no native method body.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class VisualQualityConfiguration
    {
        [SerializeField] private PerformanceProfile.QualityLevel m_qualityLevel;
        // 06000da7: direct original quality-field read.
        public PerformanceProfile.QualityLevel QualityLevel => m_qualityLevel;
        // 06000da8: original public abstract virtual method, never a substitute body.
        public abstract void Apply();
        // 06000da9: protected original System.Object constructor only.
        protected VisualQualityConfiguration() { }
    }
}
