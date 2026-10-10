using System;
using UnityEngine;

namespace Hardlight
{
    [Serializable]
    public class ExclusionList
    {
        [Tooltip("The name of the Exclusion List. This can be used to enable/disable the exclusions as required.")]
        public string Name;
        [Tooltip("A list of strings (can be partial) to compare each log entry to. If a log entry contains one of these strings, it will be ignored.")]
        public string[] Exclusions;
        private bool m_enabled = true;

        // HLUnityCore.Runtime 06000a5a: write-only property, without normalization.
        public bool Enabled { set => m_enabled = value; }

        // 06000a5b: disabled exits before touching the array or message. The original
        // indexed search retains null-array, null-message and null-needle failures.
        public bool IsExcluded(string message)
        {
            if (!m_enabled) return false;
            for (int i = 0; i < Exclusions.Length; ++i)
                if (message.Contains(Exclusions[i])) return true;
            return false;
        }

        // 06000a5c: enabled field initialization occurs before Object construction.
        public ExclusionList() { }
    }
}
