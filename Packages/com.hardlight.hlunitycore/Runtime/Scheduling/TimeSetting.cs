using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TimeSetting
    {
        [SerializeField] protected SerializableDictionary<TimeCategoryObject, float> m_overridesDictionary =
            new SerializableDictionary<TimeCategoryObject, float>();
        private const float DefaultTimescale = 1f;
        private readonly List<KeyValuePair<TimeCategoryObject, float>> m_kvpsToModify =
            new List<KeyValuePair<TimeCategoryObject, float>>();

        // Original 06000edf / ARM64 1b1f8f4 returns the actual mutable dictionary as a readonly view.
        public IReadOnlyDictionary<TimeCategoryObject, float> OverrideDictionaryLookup => m_overridesDictionary;

        // 06000ee0 / 1b1f8fc. Only this object's existing keys are interpolated.
        // The scratch list is cleared after normal writeback, not at entry or in a finally.
        public void Lerp(TimeSetting from, TimeSetting to, float t)
        {
            float clamped = Mathf.Clamp01(t);
            foreach (KeyValuePair<TimeCategoryObject, float> entry in m_overridesDictionary)
            {
                float a = System.Collections.Generic.CollectionExtensions.GetValueOrDefault(from.m_overridesDictionary, entry.Key, DefaultTimescale);
                float b = System.Collections.Generic.CollectionExtensions.GetValueOrDefault(to.m_overridesDictionary, entry.Key, DefaultTimescale);
                m_kvpsToModify.Add(new KeyValuePair<TimeCategoryObject, float>(entry.Key, a + clamped * (b - a)));
            }
            foreach (KeyValuePair<TimeCategoryObject, float> entry in m_kvpsToModify)
                m_overridesDictionary[entry.Key] = entry.Value;
            m_kvpsToModify.Clear();
        }

        // 06000ee1 / 1b1b3e8 preserves the engine isPlaying read even though this supplied
        // player's native body discards its result before retrieving the runtime config.
        public static TimeSetting GetDefault(float scaling = 1f)
        {
            TimeSetting setting = new TimeSetting();
            _ = Application.isPlaying;
            TimeCategoryConfiguration config = SystemConfiguration.GetConfig<TimeCategoryConfiguration>();
            config.AddCategory(config.UnityGlobalTime);
            setting.m_overridesDictionary = new SerializableDictionary<TimeCategoryObject, float>();
            foreach (TimeCategoryObject category in config.PrioritisedCategories)
                setting.m_overridesDictionary[category] = scaling;
            return setting;
        }

        // 06000ee2 / 1b1fe98 allocates first and then copies, without a null guard.
        public static TimeSetting Clone(TimeSetting baseSetting)
        {
            TimeSetting setting = new TimeSetting();
            setting.Copy(baseSetting);
            return setting;
        }
        // 06000ee3 / 1b1bd10 and 06000ee4 / 1b1c808 retain original dictionary boundaries.
        public void SetCategory(TimeCategoryObject timeCategory, float timeScale) { m_overridesDictionary[timeCategory] = timeScale; }
        public float GetOverrideWithDefault(TimeCategoryObject category, float defaultValue)
        { return m_overridesDictionary.TryGetWithDefault(category, defaultValue); }

        // 06000ee5 / 1b1dbc8 clears before reading the source; Copy(this) therefore empties itself.
        public void Copy(TimeSetting other)
        {
            m_overridesDictionary.Clear();
            foreach (KeyValuePair<TimeCategoryObject, float> entry in other.m_overridesDictionary)
                m_overridesDictionary.Add(entry.Key, entry.Value);
        }
        // 06000ee6 / 1b1fdb8: original dictionaries/lists are allocated in declaration order.
        public TimeSetting() { }
    }
}
