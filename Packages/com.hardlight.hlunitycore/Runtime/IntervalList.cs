using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.NullChecks, false)]
    public class IntervalList<TInterval, TValue> where TInterval : IComparable<TInterval>
    {
        [UnityEngine.SerializeField]
        [UnityEngine.TooltipAttribute("The list of intervals.")]
        private List<IntervalType> m_intervals = new List<IntervalType>();

        // Original 06000f89: first matching half-open interval wins. The enumerator
        // is disposed before the miss writes default; exceptional comparisons leave
        // the caller's out storage untouched, matching the native control flow.
        public bool TryGetIntervalValue(TInterval t, out TValue value)
        {
            foreach (IntervalType interval in m_intervals)
            {
                if (interval.IsValid(t))
                {
                    value = interval.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        public IntervalList() { }

        [Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
        private class IntervalType
        {
            [UnityEngine.TooltipAttribute("Minimum included value to trigger interval.")]
            [UnityEngine.SerializeField]
            private TInterval m_min;
            [UnityEngine.TooltipAttribute("Maximum excluded value to trigger interval.")]
            [UnityEngine.SerializeField]
            private TInterval m_max;
            [UnityEngine.SerializeField]
            [UnityEngine.TooltipAttribute("Value for this interval.")]
            private TValue m_value;

            // Original 06000f8b compares the query to the minimum before maximum.
            public bool IsValid(TInterval t) => t.CompareTo(m_min) >= 0 && t.CompareTo(m_max) < 0;
            public TValue Value => m_value;
            public IntervalType() { }
        }
    }
}
