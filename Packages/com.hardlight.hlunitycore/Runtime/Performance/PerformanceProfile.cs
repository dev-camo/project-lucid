using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "NewPerformanceProfile", menuName = "Hardlight/HLUnityCore/ScalablePerformance/PerformanceProfile")]
    public class PerformanceProfile : ScriptableObjectWithGuid
    {
        public enum QualityLevel
        {
            VeryLow = -200,
            Low = -100,
            Medium = 0,
            High = 100,
            VeryHigh = 200
        }

        [SerializeField] protected List<PerformanceAttribute> m_attributes = new List<PerformanceAttribute>();
        // 0x06000d79/7a: original event uses Combine/Remove and CompareExchange.
        public event Action OnProfileUpdated;

        // 0x06000d7b and natural 0x06000d81/82. Feature equality compares ID strings.
        public bool IsSupported(PerformanceAttribute requiredAttribute)
        {
            PerformanceAttribute attribute = m_attributes.Find(x => x.Feature.ID == requiredAttribute.Feature.ID);
            if (attribute != null)
                return attribute.Level >= requiredAttribute.Level;
            return requiredAttribute.Level > QualityLevel.Medium;
        }

        // 0x06000d7c and natural 0x06000d83/84: a missing feature is exactly Medium.
        public bool IsExactlySupported(PerformanceAttribute requiredAttribute)
        {
            PerformanceAttribute attribute = m_attributes.Find(x => x.Feature.ID == requiredAttribute.Feature.ID);
            if (attribute != null)
                return requiredAttribute.Level == attribute.Level;
            return requiredAttribute.Level == QualityLevel.Medium;
        }

        // 0x06000d7d and natural 0x06000d85/86: retain first List.Find match.
        public PerformanceAttribute GetAttribute(string feature)
        {
            return m_attributes.Find(x => x.Feature.ID == feature);
        }

        // 0x06000d7e and natural 0x06000d87/88: compare IDs, including null strings.
        public PerformanceAttribute GetAttribute(ScalableFeature feature)
        {
            return m_attributes.Find(x => x.Feature.ID == feature.ID);
        }

        // 0x06000d7f: invoke one captured delegate; no GUID-base OnValidate call.
        protected override void OnValidate()
        {
            OnProfileUpdated?.Invoke();
        }
        // 0x06000d80 initializes the list before the genuine GUID base constructor.
    }
}
