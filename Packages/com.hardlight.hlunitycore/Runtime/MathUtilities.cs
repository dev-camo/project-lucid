using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000193. All five own fields are retained;
    // this is the genuine two-method subset used by Vector3Extensions. The other
    // twenty-two original methods remain unresolved, rather than receiving bodies.
    public static class MathUtilities
    {
        public const float ZeroTolerance = 0.0001f;
        public const int DegreesInCircle = 360;
        public const int DegreesInSemiCircle = 180;
        public const float RadiansInCircle = 6.2831855f;
        public const float RadiansInSemiCircle = 3.1415927f;

        // 0x06000b4c: add one half then truncate, including the original asymmetric
        // negative rounding. Double precision is used for the strict window.
        // Nonfinite/out-of-range casts retain the scripting backend's behavior.
        public static float ClampToInt(float value, float zeroTolerance = 0.0001f)
        {
            float integer = (int)(value + 0.5f);
            return (double)integer - zeroTolerance < value &&
                   (double)integer + zeroTolerance > value ? integer : value;
        }

        // 0x06000b4d: equality with the tolerance does not snap to zero.
        public static float ClampToZero(float value, float zeroTolerance = 0.0001f)
        {
            return Mathf.Abs(value) < zeroTolerance ? 0f : value;
        }
    }
}
