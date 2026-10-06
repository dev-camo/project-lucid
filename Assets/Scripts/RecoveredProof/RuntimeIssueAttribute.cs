using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RuntimeIssueAttribute
    {
        [SerializeField] private PerformanceProfile.QualityLevel m_level;
        [SerializeField] private PerformanceAttribute m_performanceAttribute;
        [SerializeField] private List<RuntimeIssueConfiguration> m_configurations;

        // Game.Runtime 0x0600201f..21: return the original fields, including null lists.
        public PerformanceProfile.QualityLevel QualityLevel => m_level;
        public PerformanceAttribute PerformanceAttribute => m_performanceAttribute;
        public IReadOnlyCollection<RuntimeIssueConfiguration> Configurations => m_configurations;
        // 0x06002022: object-base-only constructor; no defaults or list initialization.
    }
}
