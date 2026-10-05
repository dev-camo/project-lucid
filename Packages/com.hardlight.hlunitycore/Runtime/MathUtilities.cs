using System;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000193: all five fields and twenty-four
    // methods. Native evidence preserves the original edge cases and evaluation
    // order. The two previously recovered clamp methods are unchanged context.
    public static class MathUtilities
    {
        public const float ZeroTolerance = 0.0001f;
        public const int DegreesInCircle = 360;
        public const int DegreesInSemiCircle = 180;
        public const float RadiansInCircle = 6.2831855f;
        public const float RadiansInSemiCircle = 3.1415927f;

        // 0x06000b46/47: the open interval is computed in double precision even
        // when all inputs are floats. Equality at either end is outside.
        public static bool WithinTolerance(float value, float target, float tolerance = 0.0001f)
        {
            return WithinTolerance((double)value, target, tolerance);
        }

        public static bool WithinTolerance(double value, double target, double tolerance = 9.999999747378752E-05)
        {
            return target - tolerance < value && target + tolerance > value;
        }

        // 0x06000b48/49: unordered endpoints are exchanged by the same ordered
        // comparison as the tolerance overload. Both ends are inclusive here.
        public static bool WithinRange(float value, float min, float max)
        {
            return WithinRangeWithTolerance((double)value, min, max, 0.0);
        }

        public static bool WithinRange(double value, double min, double max)
        {
            return WithinRangeWithTolerance(value, min, max, 0.0);
        }

        // 0x06000b4a/4b: inverted bounds are supported; negative tolerance shrinks
        // the interval. A NaN endpoint still propagates to the comparison.
        public static bool WithinRangeWithTolerance(float value, float min, float max, float tolerance = 0.0001f)
        {
            return WithinRangeWithTolerance((double)value, min, max, tolerance);
        }

        public static bool WithinRangeWithTolerance(double value, double min, double max, double tolerance = 9.999999747378752E-05)
        {
            if (min > max)
            {
                double oldMin = min;
                min = max;
                max = oldMin;
            }
            return min - tolerance <= value && max + tolerance >= value;
        }

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

        // 0x06000b4e: authored callers supply a unit direction; do not normalize it
        // or divide by its squared length. The line is infinite in both directions.
        public static Vector3 GetClosestPointOnLine(Vector3 origin, Vector3 direction, Vector3 point)
        {
            return origin + direction * Vector3.Dot(direction, point - origin);
        }

        // 0x06000b4f: the original range is (-180,180], with repeated subtraction
        // and addition. Infinite or sufficiently large stagnant inputs can loop;
        // no modulo, finite guard, or new termination policy is substituted.
        public static float ShortestAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle <= -180f) angle += 360f;
            return angle;
        }

        // 0x06000b50/51: these conversions do not clamp their input.
        public static float NormalisedToPercent(float value) { return value * 100f; }
        public static float PercentToNormalised(float value) { return value * 0.01f; }

        // 0x06000b52: this is numeric conversion through Single, not generic
        // operators. The original IConvertible constraint is retained. Box both operands before testing either; null returns the
        // default of T. Conversion order, culture, rounding and exceptions come
        // from the genuine BCL. Mathf.Lerp clamps only t, including its NaN quirk.
        public static T Lerp<T>(T a, T b, float t) where T : IConvertible
        {
            object first = a;
            object second = b;
            if (first == null || second == null) return default(T);
            float firstValue = Convert.ToSingle(first);
            float secondValue = Convert.ToSingle(second);
            return (T)Convert.ChangeType(Mathf.Lerp(firstValue, secondValue, t), typeof(T));
        }

        // 0x06000b53: coefficients are (quadratic, linear, constant). Preserve the
        // elimination order; duplicate abscissas produce native nonfinite values.
        public static Vector3 QuadraticFromPoints(float x0, float y0, float x1, float y1, float x2, float y2)
        {
            float squareDifference20 = x2 * x2 - x0 * x0;
            float squareDifference10 = x1 * x1 - x0 * x0;
            float difference10 = y1 - y0;
            float linear = (difference10 * squareDifference20 - squareDifference10 * (y2 - y0)) /
                (squareDifference10 * (x0 - x2) - (x0 - x1) * squareDifference20);
            float quadratic = (difference10 + (x0 - x1) * linear) / squareDifference10;
            float constant = y0 - quadratic * x0 * x0 - linear * x0;
            return new Vector3(quadratic, linear, constant);
        }

        // 0x06000b54: multiply the quadratic coefficient by x twice; do not
        // reassociate to coefficients.x*(x*x) or use Horner's method.
        public static float QuadraticEvaluate(float x, Vector3 coefficients)
        {
            return coefficients.x * x * x + coefficients.y * x + coefficients.z;
        }

        // 0x06000b55..58: the genuine Unity helpers use nearest-even rounding and
        // floor. The floating overload preserves the supplied native's Single
        // half-step arithmetic outside exact ties, including changed large integral
        // quotients; calling managed Mathf.Round directly would erase that quirk.
        // Integer division first converts the multiple to Single, casts the
        // rounded/floored quotient, then performs unchecked integer multiplication.
        // Invalid and overflowing conversions retain the scripting backend limit.
        public static int RoundToMultipleOf(float number, int multiple)
        {
            return unchecked(Mathf.RoundToInt(number / multiple) * multiple);
        }

        public static float RoundToMultipleOf(float number, float multiple)
        {
            float quotient = number / multiple;
            double integral = Math.Truncate(quotient);
            double fraction = quotient - integral;
            if (quotient < 0f)
            {
                if (fraction == -0.5)
                {
                    float whole = (float)integral;
                    return (((long)integral & 1L) == 0L ? whole : whole - 1f) * multiple;
                }
                return Mathf.Ceil(quotient - 0.5f) * multiple;
            }
            if (fraction == 0.5)
            {
                float whole = (float)integral;
                return (((long)integral & 1L) == 0L ? whole : whole + 1f) * multiple;
            }
            return Mathf.Floor(quotient + 0.5f) * multiple;
        }

        public static int FloorToMultipleOf(float number, int multiple)
        {
            return unchecked(Mathf.FloorToInt(number / multiple) * multiple);
        }

        public static float FloorToMultipleOf(float number, float multiple)
        {
            return Mathf.Floor(number / multiple) * multiple;
        }

        // 0x06000b59: despite its name, this chooses a radial line/ellipse
        // intersection using the rectangle between zero and the scaled point.
        // Retain its zero-x, interior-point and degenerate-radius behavior; it is
        // not a replacement Euclidean closest-point solver.
        public static Vector2 ClosestPointOnEllipse(Vector2 point, float rx, float ry)
        {
            Vector2 scaled = point * (rx * ry);
            float slope = scaled.x == 0f ? 0f : scaled.y / scaled.x;
            float squareRx = rx * rx;
            float coefficient = ry * ry + slope * (squareRx * slope);
            float determinant = Mathf.Sqrt(0f + (squareRx * ry * ry) * (coefficient * 4f));
            float divisor = coefficient + coefficient;
            float firstX = -determinant / divisor;
            float secondX = determinant / divisor;
            Vector2 first = new Vector2(firstX, slope * firstX);
            Vector2 second = new Vector2(secondX, slope * secondX);
            float minX = Mathf.Min(0f, scaled.x);
            float minY = Mathf.Min(0f, scaled.y);
            float width = Mathf.Max(0f, scaled.x) - minX;
            float height = Mathf.Max(0f, scaled.y) - minY;
            if (height == 0f)
            {
                if (first.x >= minX && first.x <= minX + width) return first;
                if (second.x >= minX && second.x <= minX + width) return second;
            }
            else if (width == 0f)
            {
                if (first.y >= minY && first.y <= minY + height) return first;
                if (second.y >= minY && second.y <= minY + height) return second;
            }
            return first.x >= minX && first.x < minX + width &&
                   first.y >= minY && first.y < minY + height ? first : second;
        }

        // 0x06000b5a: angles are degrees. The segment's Y radius controls height;
        // tubeRadius changes the X/Z radial terms but does not scale the height.
        public static Vector3 CalculateTorusPosition(float circleAngle, float tubeAngle, float tubeRadius, Vector3 segmentRadius)
        {
            float circleRadians = circleAngle * Mathf.Deg2Rad;
            float circleSine = Mathf.Sin(circleRadians);
            float circleCosine = Mathf.Cos(circleRadians);
            float tubeRadians = tubeAngle * Mathf.Deg2Rad;
            float tubeSine = Mathf.Sin(tubeRadians);
            float tubeCosine = Mathf.Cos(tubeRadians) * tubeRadius;
            return new Vector3(circleSine * (segmentRadius.x + tubeCosine),
                tubeSine * segmentRadius.y,
                circleCosine * (segmentRadius.z + tubeCosine));
        }

        // 0x06000b5b/5c: bounds are not sorted. Below-min wins when both comparisons
        // could clamp an inverted interval; equal endpoints and NaN leave it alone.
        public static bool TryClamp(ref float value, float min, float max)
        {
            if (value < min) { value = min; return true; }
            if (value > max) { value = max; return true; }
            return false;
        }

        public static bool TryClamp(ref int value, int min, int max)
        {
            if (value < min) { value = min; return true; }
            if (value > max) { value = max; return true; }
            return false;
        }

        // 0x06000b5d: normals/distances are used as stored. Parallel/degenerate
        // planes divide by zero instead of returning a new sentinel or success flag.
        public static Vector3 PlaneIntersect(Plane p1, Plane p2, Plane p3)
        {
            Vector3 cross23 = Vector3.Cross(p2.normal, p3.normal);
            return (-p2.distance * Vector3.Cross(p3.normal, p1.normal) -
                    p1.distance * cross23 - p3.distance * Vector3.Cross(p1.normal, p2.normal)) /
                   Vector3.Dot(p1.normal, cross23);
        }
    }
}
