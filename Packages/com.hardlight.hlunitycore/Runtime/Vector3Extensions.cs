using System;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000088, complete fieldless 32-method type.
    public static class Vector3Extensions
    {
        public static Vector2 xy(this Vector3 aVector) // 0x0600033f
        {
            return new Vector2(aVector.x, aVector.y);
        }
        public static Vector2 xz(this Vector3 aVector) // 0x06000340
        {
            return new Vector2(aVector.x, aVector.z);
        }
        public static Vector2 yz(this Vector3 aVector) // 0x06000341
        {
            return new Vector2(aVector.y, aVector.z);
        }
        public static Vector2 yx(this Vector3 aVector) // 0x06000342
        {
            return new Vector2(aVector.y, aVector.x);
        }
        public static Vector2 zx(this Vector3 aVector) // 0x06000343
        {
            return new Vector2(aVector.z, aVector.x);
        }
        public static Vector2 zy(this Vector3 aVector) // 0x06000344
        {
            return new Vector2(aVector.z, aVector.y);
        }
        public static Vector3 XZ(this Vector3 aVector) // 0x06000345
        {
            return new Vector3(aVector.x, 0f, aVector.z);
        }
        public static Vector3 Divide(this Vector3 a, Vector3 b) // 0x06000346
        {
            return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        }

        // 0x06000347: only evaluate the selected endpoint outside the interval.
        // Inside it, invoke from before to, then use the engine's spherical lerp.
        public static Vector3 SlerpClamped(Func<Vector3> fromFn, Func<Vector3> toFn, float t)
        {
            if (t <= 0f) return fromFn();
            if (t >= 1f) return toFn();
            return Vector3.Slerp(fromFn(), toFn(), t);
        }
        public static Vector3 LerpUnclamped(this Vector3 from, Vector3 to, float t) // 0x06000348
        {
            return from + (to - from) * t;
        }
        public static Vector3 Min(this Vector3 a, Vector3 b) // 0x06000349
        {
            return new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z));
        }
        public static Vector3 Max(this Vector3 a, Vector3 b) // 0x0600034a
        {
            return new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z));
        }
        public static bool WithinTolerance(this Vector3 a, Vector3 b, float tolerance = 0.0001f) // 0x0600034b
        {
            return (a - b).sqrMagnitude < tolerance * tolerance;
        }
        public static Vector3 ClampToZero(this Vector3 aVector, float zeroTolerance = 0.0001f) // 0x0600034c
        {
            return new Vector3(MathUtilities.ClampToZero(aVector.x, zeroTolerance),
                               MathUtilities.ClampToZero(aVector.y, zeroTolerance),
                               MathUtilities.ClampToZero(aVector.z, zeroTolerance));
        }
        public static Vector3 ClampToInt(this Vector3 aVector, float zeroTolerance = 0.0001f) // 0x0600034d
        {
            return new Vector3(MathUtilities.ClampToInt(aVector.x, zeroTolerance),
                               MathUtilities.ClampToInt(aVector.y, zeroTolerance),
                               MathUtilities.ClampToInt(aVector.z, zeroTolerance));
        }

        // 0x0600034e: exact component equality skips damping and retains velocity.
        // Copy velocities first; commit the ref only after all x/y/z work. The
        // original reads deltaTime separately for every changed component.
        public static Vector3 SmoothDampIndependentAxis(this Vector3 current, Vector3 target,
            ref Vector3 currentVelocity, Vector3 smoothTime)
        {
            Vector3 velocity = currentVelocity;
            Vector3 result = target;
            if (current.x != target.x)
                result.x = Mathf.SmoothDamp(current.x, target.x, ref velocity.x, smoothTime.x, float.PositiveInfinity, Time.deltaTime);
            if (current.y != target.y)
                result.y = Mathf.SmoothDamp(current.y, target.y, ref velocity.y, smoothTime.y, float.PositiveInfinity, Time.deltaTime);
            if (current.z != target.z)
                result.z = Mathf.SmoothDamp(current.z, target.z, ref velocity.z, smoothTime.z, float.PositiveInfinity, Time.deltaTime);
            currentVelocity = velocity;
            return result;
        }
        public static Vector3 SmoothDampAngleIndependentAxis(this Vector3 current, Vector3 target,
            ref Vector3 currentVelocity, Vector3 smoothTime) // 0x0600034f
        {
            Vector3 velocity = currentVelocity;
            Vector3 result = target;
            if (current.x != target.x)
                result.x = Mathf.SmoothDampAngle(current.x, target.x, ref velocity.x, smoothTime.x, float.PositiveInfinity, Time.deltaTime);
            if (current.y != target.y)
                result.y = Mathf.SmoothDampAngle(current.y, target.y, ref velocity.y, smoothTime.y, float.PositiveInfinity, Time.deltaTime);
            if (current.z != target.z)
                result.z = Mathf.SmoothDampAngle(current.z, target.z, ref velocity.z, smoothTime.z, float.PositiveInfinity, Time.deltaTime);
            currentVelocity = velocity;
            return result;
        }

        // 0x06000350: zero deltaTime returns before even copying currentVelocity.
        // Equality is not angular equivalence: 0 and 360 still invoke damping.
        public static Vector3 SmoothDampAngleIndependentAxis(this Vector3 current, Vector3 target,
            ref Vector3 currentVelocity, Vector3 smoothTime, float deltaTime)
        {
            if (deltaTime == 0f) return current;
            Vector3 velocity = currentVelocity;
            Vector3 result = target;
            if (current.x != target.x)
                result.x = Mathf.SmoothDampAngle(current.x, target.x, ref velocity.x, smoothTime.x, float.PositiveInfinity, deltaTime);
            if (current.y != target.y)
                result.y = Mathf.SmoothDampAngle(current.y, target.y, ref velocity.y, smoothTime.y, float.PositiveInfinity, deltaTime);
            if (current.z != target.z)
                result.z = Mathf.SmoothDampAngle(current.z, target.z, ref velocity.z, smoothTime.z, float.PositiveInfinity, deltaTime);
            currentVelocity = velocity;
            return result;
        }
        public static Vector3 ExtractTranslationFromMatrix(ref Matrix4x4 matrix) // 0x06000351
        {
            return new Vector3(matrix.m03, matrix.m13, matrix.m23);
        }

        // 0x06000352: measure four-dimensional columns, including the last row.
        public static Vector3 ExtractScale(ref Matrix4x4 matrix)
        {
            return new Vector3(matrix.GetColumn(0).magnitude, matrix.GetColumn(1).magnitude, matrix.GetColumn(2).magnitude);
        }

        // 0x06000353: mirror only X when the upper three column vectors have
        // negative handedness. Translation and the fourth row do not affect sign.
        public static Vector3 ExtractScaleNegativeX(ref Matrix4x4 matrix)
        {
            Vector3 result = ExtractScale(ref matrix);
            Vector3 first = new Vector3(matrix.m00, matrix.m10, matrix.m20);
            Vector3 second = new Vector3(matrix.m01, matrix.m11, matrix.m21);
            Vector3 third = new Vector3(matrix.m02, matrix.m12, matrix.m22);
            if (Vector3.Dot(Vector3.Cross(first, second), third) < 0f) result.x = -result.x;
            return result;
        }
        public static Vector3 MoveTowards(Vector3 from, Vector3 to, Vector3 amount) // 0x06000354
        {
            return new Vector3(Mathf.MoveTowards(from.x, to.x, amount.x),
                               Mathf.MoveTowards(from.y, to.y, amount.y),
                               Mathf.MoveTowards(from.z, to.z, amount.z));
        }
        public static Vector3 Abs(this Vector3 vec) // 0x06000355
        {
            return new Vector3(Mathf.Abs(vec.x), Mathf.Abs(vec.y), Mathf.Abs(vec.z));
        }

        // 0x06000356/57: original handedness is dot(other, cross(reference, normal)).
        // It differs from Vector3.SignedAngle, so do not replace it with that API.
        public static float SignedVectorAngle(Vector3 referenceVector, Vector3 otherVector, Vector3 normal)
        {
            return Vector3.Angle(referenceVector, otherVector) * SignOfAngle(referenceVector, otherVector, normal);
        }
        public static float SignOfAngle(Vector3 referenceVector, Vector3 otherVector, Vector3 normal)
        {
            return Mathf.Sign(Vector3.Dot(otherVector, Vector3.Cross(referenceVector, normal)));
        }

        // 0x06000358: the authored caller supplies a unit normal. Preserve the
        // direct subtraction rather than normalizing or dividing by its length.
        public static float SignedVectorAngleOnPlane(Vector3 referenceVector, Vector3 otherVector, Vector3 normal)
        {
            referenceVector -= normal * Vector3.Dot(referenceVector, normal);
            otherVector -= normal * Vector3.Dot(otherVector, normal);
            return SignedVectorAngle(referenceVector, otherVector, normal);
        }

        // 0x06000359: a supplied nonzero length is used as-is. Reject outside the
        // inclusive segment before committing vPoint; unordered projections pass
        // these rejection comparisons, preserving the original degenerate case.
        public static bool ProjectPointOnLine(Vector3 vA, Vector3 vB, ref Vector3 vPoint, float lineLength = 0f)
        {
            Vector3 pointOffset = vPoint - vA;
            Vector3 direction = vB - vA;
            if (lineLength == 0f) lineLength = direction.magnitude;
            direction /= lineLength;
            float projection = Vector3.Dot(pointOffset, direction);
            if (projection < 0f || projection > lineLength) return false;
            vPoint = vA + direction * projection;
            return true;
        }

        // 0x0600035a: parse all three current-culture fields before changing result.
        // A null string throws through StartsWith; nonfinite parsed values survive.
        public static bool TryParse(string sVector, ref Vector3 result)
        {
            if (!sVector.StartsWith("(") || !sVector.EndsWith(")")) return false;
            string[] values = sVector.Substring(1, sVector.Length - 2).Split(',');
            if (values.Length != 3) return false;
            float x, y, z;
            if (!float.TryParse(values[0], out x) || !float.TryParse(values[1], out y) || !float.TryParse(values[2], out z)) return false;
            result = new Vector3(x, y, z);
            return true;
        }

        // 0x0600035b: retain the genuine shared formatter, its thread ownership,
        // signed integer formatting, and the current-culture String.Format call.
        public static string ToDebugString(this Vector3 v, int numDecimalPlaces = 2)
        {
            string format = OpString.i + "({0:F" + numDecimalPlaces + "}, {1:F" + numDecimalPlaces + "}, {2:F" + numDecimalPlaces + "})";
            return string.Format(format, v.x, v.y, v.z);
        }
        public static Vector3 PointOnCircleWithFixedY(this Vector3 centre, float angleInRadians, float distance, float fixedY) // 0x0600035c
        {
            return new Vector3(centre.x + Mathf.Cos(angleInRadians) * distance, fixedY, centre.z + Mathf.Sin(angleInRadians) * distance);
        }
        public static Vector3 PointOnCircleWithFixedYLHS(this Vector3 centre, float angleInRadians, float distance, float fixedY) // 0x0600035d
        {
            return new Vector3(centre.x + Mathf.Sin(angleInRadians) * distance, fixedY, centre.z + Mathf.Cos(angleInRadians) * distance);
        }
        public static bool IsValid(this Vector3 aVector) // 0x0600035e
        {
            return aVector.x.IsValid() && aVector.y.IsValid() && aVector.z.IsValid();
        }
    }
}
