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
    public class TimeSettings_SDT : TimeSetting
    {
        [SerializeField] private SerializableDictionary<TimeCategory, float> m_overrides =
            new SerializableDictionary<TimeCategory, float>(HardlightEnumComparers.TimeCategoryComparer);

        // Original 06002e70 / ARM64 5d5a48. Any prior base entry suppresses the entire
        // conversion, including retries after a partially applied failing lookup.
        public void ApplyTimeCategoryObjectConversion(IDictionary<TimeCategory, TimeCategoryObject> lookup)
        {
            if (m_overridesDictionary.Count > 0) return;
            foreach (KeyValuePair<TimeCategory, float> entry in m_overrides)
                m_overridesDictionary[lookup[entry.Key]] = entry.Value;
        }
        // 06002e71 / 5d5d90: own enum dictionary allocation precedes the genuine base constructor.
        public TimeSettings_SDT() { }
    }
}
