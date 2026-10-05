using System;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000076. No own fields.
    public static class NumericExtensions
    {
        // 0x060002d2: inclusive absolute tolerance; unordered comparisons take
        // the original -1 branch instead of Single.CompareTo's NaN ordering.
        public static int CompareTo(this float value, float other, float tolerance)
        {
            if (Mathf.Abs(value - other) <= tolerance) return 0;
            return value > other ? 1 : -1;
        }

        // 0x060002d3: all finite Single bit patterns are accepted, including
        // both signs of zero and subnormal values; exponent-all-ones is rejected.
        public static bool IsValid(this float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
