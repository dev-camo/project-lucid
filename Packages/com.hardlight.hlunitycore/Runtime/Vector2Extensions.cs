using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000086; complete twelve-method type.
    public static class Vector2Extensions
    {
        // 0x06000332/33: preserve the engine's component conversion/rounding.
        public static Vector2Int CeilToInt(this Vector2 vector2)
        {
            return new Vector2Int(Mathf.CeilToInt(vector2.x), Mathf.CeilToInt(vector2.y));
        }
        public static Vector2Int RoundToInt(this Vector2 vector2)
        {
            return new Vector2Int(Mathf.RoundToInt(vector2.x), Mathf.RoundToInt(vector2.y));
        }

        // 0x06000334: the original implementation truncates toward zero. Its
        // name must not tempt callers to substitute Mathf.FloorToInt here.
        public static Vector2Int FloorToInt(this Vector2 vector2)
        {
            return new Vector2Int((int)vector2.x, (int)vector2.y);
        }
        public static Vector3 ToXZ(this Vector2 aVector) // 0x06000335
        {
            return new Vector3(aVector.x, 0f, aVector.y);
        }
        public static Vector2 Divide(this Vector2 a, Vector2 b) // 0x06000336
        {
            return new Vector2(a.x / b.x, a.y / b.y);
        }
        public static Vector2 Min(this Vector2 a, Vector2 b) // 0x06000337
        {
            return new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
        }
        public static Vector2 Max(this Vector2 a, Vector2 b) // 0x06000338
        {
            return new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
        public static Vector2 FlipX(this Vector2 vector) // 0x06000339
        {
            return new Vector2(-vector.x, vector.y);
        }
        public static Vector2 FlipY(this Vector2 vector) // 0x0600033a
        {
            return new Vector2(vector.x, -vector.y);
        }
        public static Vector2 Abs(this Vector2 vec) // 0x0600033b
        {
            return new Vector2(Mathf.Abs(vec.x), Mathf.Abs(vec.y));
        }
        public static bool IsValid(this Vector2 aVector) // 0x0600033c
        {
            return aVector.x.IsValid() && aVector.y.IsValid();
        }

        // 0x0600033d: squared Euclidean distance and a strict boundary. Negative
        // tolerances are squared too, and identical vectors fail tolerance zero.
        public static bool WithinTolerance(this Vector2 a, Vector2 b, float tolerance = 0.0001f)
        {
            return (a - b).sqrMagnitude < tolerance * tolerance;
        }
    }
}
