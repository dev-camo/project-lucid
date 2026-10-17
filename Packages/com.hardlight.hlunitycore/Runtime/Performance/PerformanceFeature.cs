using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200020c (nested 0x0200020d), methods
    // 0x06000d76..0x06000d78; shared bodies at x86-64 0x18ca540/0x18ca6f0.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class PerformanceFeature<T>
    {
        [Serializable]
        private class QualityAttributes
        {
            public PerformanceProfile.QualityLevel QualityLevel;
            public T QualitySetting;
        }

        [Tooltip("Feature to define the quality setting.")]
        [SerializeField]
        private ScalableFeature m_scalableFeature;
        [Tooltip("Defines each quality setting's attributes.")]
        [SerializeField]
        private List<QualityAttributes> m_qualityAttributes;
        private readonly SystemRef<ScalablePerformance> m_scalablePerformanceRef = ProcessManager.GetSystemRef<ScalablePerformance>();

        // 0x06000d76: the active profile's attribute for this feature is read
        // without a null check; the first row whose level matches wins.
        public bool TryGet(out T qualitySetting)
        {
            if (m_scalablePerformanceRef.TryGet(out ScalablePerformance scalablePerformance))
            {
                PerformanceAttribute attribute = scalablePerformance.ActiveProfile.GetAttribute(m_scalableFeature);
                foreach (QualityAttributes qualityAttributes in m_qualityAttributes)
                {
                    if (qualityAttributes.QualityLevel == attribute.Level)
                    {
                        qualitySetting = qualityAttributes.QualitySetting;
                        return true;
                    }
                }
            }
            qualitySetting = default;
            return false;
        }
    }
}
