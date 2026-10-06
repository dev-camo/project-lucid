using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RuntimeIssueConfiguration
    {
        private const float BytesPerMegabyte = 1048576;
        [SerializeField] private string m_path;
        [SerializeField] private float m_value;
        [SerializeField] private bool m_isMaxThreshold;

        // Game.Runtime 0x06002023/24: direct original field getters.
        public string Path => m_path;
        public float Value => m_value;

        // 0x06002025: ARM64 multiplies by Single 0x35800000 before threshold reads.
        public bool IsValidBytes(float value)
        {
            value *= 1f / BytesPerMegabyte;
            return m_isMaxThreshold ? value <= m_value : value >= m_value;
        }

        // 0x06002026: inclusive ordered comparisons; NaN does not meet either threshold.
        public bool IsValid(float value)
        {
            return m_isMaxThreshold ? value <= m_value : value >= m_value;
        }
        // 0x06002027: object-base-only constructor; serialized values remain uninitialized.
    }
}
