using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class Bezier
    {
        // Original 06000331, ARM64 01a7f390; t is not clamped.
        public static Vector3 PointAlongSpline(float t, ref SplineControlPoints controls)
        {
            float tSquared = t * t;
            float tCubed = tSquared * t;
            float inverseT = 1f - t;
            float inverseSquared = inverseT * inverseT;
            return controls.m_controlPt0 * (inverseT * inverseSquared)
                 + controls.m_controlPt1 * ((inverseSquared * 3f) * t)
                 + controls.m_controlPt2 * (tSquared * (inverseT * 3f))
                 + controls.m_controlPt3 * tCubed;
        }

        // Original 06000332, ARM64 01a7f41c: unscaled quadratic tangent normalized.
        public static Vector3 TangentAlongSpline(float t, ref SplineControlPoints controls)
        {
            float inverseT = 1f - t;
            return ((controls.m_controlPt1 - controls.m_controlPt0) * (inverseT * inverseT)
                  + (controls.m_controlPt2 - controls.m_controlPt1) * ((t * 2f) * inverseT)
                  + (controls.m_controlPt3 - controls.m_controlPt2) * (t * t)).normalized;
        }

        // Original 06000333, ARM64 01a7f59c: magnitude is a third of the Bezier derivative magnitude.
        public static PositionAndTangent PointAndTangentAlongSpline(ref float tangentMagnitude, float t, ref SplineControlPoints controls)
        {
            // Native captures all four point values before the output ref store, even when the refs alias.
            Vector3 point0 = controls.m_controlPt0;
            Vector3 point1 = controls.m_controlPt1;
            Vector3 point2 = controls.m_controlPt2;
            Vector3 point3 = controls.m_controlPt3;
            float tSquared = t * t;
            float inverseT = 1f - t;
            Vector3 tangent = (point1 - point0) * (inverseT * inverseT)
                            + (point2 - point1) * ((t * 2f) * inverseT)
                            + (point3 - point2) * tSquared;
            tangentMagnitude = tangent.magnitude;
            tangent.Normalize();
            Vector3 position = point0 * (inverseT * (inverseT * inverseT))
                             + point1 * (((inverseT * inverseT) * 3f) * t)
                             + point2 * (tSquared * (inverseT * 3f))
                             + point3 * (tSquared * t);
            return new PositionAndTangent(position, tangent);
        }

        // Original 06000334, ARM64 01a7f824.
        public static Vector3 DerivativeAlongBezier(float t, ref SplineControlPoints controls)
        {
            float inverseT = 1f - t;
            return ((controls.m_controlPt1 - controls.m_controlPt0) * 3f) * (inverseT * inverseT)
                 + (((controls.m_controlPt2 - controls.m_controlPt1) * 3f) * 2f * t) * inverseT
                 + ((controls.m_controlPt3 - controls.m_controlPt2) * 3f) * (t * t);
        }

        // Original 06000335, ARM64 01a7f8cc.
        public static Vector3 SecondDerivativeAlongBezier(float t, ref SplineControlPoints controls)
        {
            return ((controls.m_controlPt2 - controls.m_controlPt1 * 2f + controls.m_controlPt0) * 6f) * (1f - t)
                 + ((controls.m_controlPt1 + (controls.m_controlPt3 - controls.m_controlPt2 * 2f)) * 6f) * t;
        }

        // Original 06000336, ARM64 01a7f95c; non-positive iteration counts return zero.
        public static float CalculateLengthBruteForce(SplineControlPoints controls, int iterations)
        {
            float length = 0f;
            for (int i = 0; i < iterations; ++i)
            {
                Vector3 start = PointAlongSpline((float)i / iterations, ref controls);
                Vector3 end = PointAlongSpline((float)(i + 1) / iterations, ref controls);
                length += (end - start).magnitude;
            }
            return length;
        }

        // Original 06000337/06000342: natural cached lambda at original outer ordinal 6.
        public static float CalculateSegmentLengthRecursive(float startT, float endT, ref SplineControlPoints controls)
        {
            return SplineCommon.CalculateSegmentLengthRecursive(startT, endT, ref controls, (float f, ref SplineControlPoints points) => PointAlongSpline(f, ref points));
        }

        // Original 06000338, ARM64 01a80058.
        public static float CalculateLength(SplineControlPoints controls)
        {
            return CalculateSegmentLengthRecursive(0f, 1f, ref controls);
        }

        // Original 06000339, ARM64 01a80064: only control points are written; existing caches survive.
        public static void SplitSpline(ref SplineControlPoints spline0, ref SplineControlPoints spline1, SplineControlPoints controls, float ratio)
        {
            Vector3 first = Vector3.LerpUnclamped(controls.m_controlPt0, controls.m_controlPt1, ratio);
            Vector3 second = Vector3.LerpUnclamped(controls.m_controlPt1, controls.m_controlPt2, ratio);
            Vector3 third = Vector3.LerpUnclamped(controls.m_controlPt2, controls.m_controlPt3, ratio);
            Vector3 firstSecond = Vector3.LerpUnclamped(first, second, ratio);
            Vector3 secondThird = Vector3.LerpUnclamped(second, third, ratio);
            Vector3 split = Vector3.LerpUnclamped(firstSecond, secondThird, ratio);
            spline0.m_controlPt0 = controls.m_controlPt0;
            spline0.m_controlPt1 = first;
            spline0.m_controlPt2 = firstSecond;
            spline0.m_controlPt3 = split;
            spline1.m_controlPt0 = split;
            spline1.m_controlPt1 = secondThird;
            spline1.m_controlPt2 = third;
            spline1.m_controlPt3 = controls.m_controlPt3;
        }

        // Original 0600033a, ARM64 01a80174: retain the original extrapolation, including its unusual handles.
        public static SplineControlPoints CombineSplines(SplineControlPoints spline0, SplineControlPoints spline1)
        {
            Vector3 offset = (spline0.m_controlPt1 - spline0.m_controlPt0) * 0.5f;
            Vector3 midpoint = spline0.m_controlPt0 * 0.125f + spline0.m_controlPt1 * 0.375f + spline1.m_controlPt2 * 0.375f + spline1.m_controlPt3 * 0.125f;
            Vector3 middleHandle = Vector3.LerpUnclamped(spline0.m_controlPt1, spline1.m_controlPt2, 0.5f);
            Vector3 extrapolated = midpoint + (midpoint - middleHandle) / 0.33333334f;
            Vector3 centre = Vector3.LerpUnclamped(extrapolated, spline0.m_controlPt3, 1.3333334f);
            Vector3 first = Vector3.LerpUnclamped(centre, spline0.m_controlPt3 - offset * 0.5f, 2f);
            Vector3 second = Vector3.LerpUnclamped(centre, spline0.m_controlPt3 + offset * 0.5f, 2f);
            return new SplineControlPoints
            {
                m_controlPt0 = spline0.m_controlPt0,
                m_controlPt1 = Vector3.LerpUnclamped(spline0.m_controlPt0, first, 2f),
                m_controlPt2 = Vector3.LerpUnclamped(spline1.m_controlPt3, second, 2f),
                m_controlPt3 = spline1.m_controlPt3
            };
        }

        // Original 0600033b, ARM64 01a80300: bounded 1000 steps; ties select the right neighbour.
        public static float FindNearestPointOnSpline(Vector3 point, SplineControlPoints controls, float epsilon = 0.001f)
        {
            float currentT = 0.5f;
            float step = 0.5f;
            int iterations = 1000;
            while (step > epsilon && iterations-- > 0)
            {
                step *= 0.5f;
                float leftT = currentT - step;
                float rightT = currentT + step;
                float leftDistance = (PointAlongSpline(leftT, ref controls) - point).sqrMagnitude;
                float currentDistance = (PointAlongSpline(currentT, ref controls) - point).sqrMagnitude;
                float rightDistance = (PointAlongSpline(rightT, ref controls) - point).sqrMagnitude;
                if (!(currentDistance <= leftDistance && currentDistance <= rightDistance))
                    currentT = leftDistance < rightDistance ? leftT : rightT;
            }
            return currentT;
        }

        // Original 0600033c, ARM64 01a804f4: eleven attempts, previous estimate on convergence, zero on exhaustion.
        public static float NewtonRaphsonIterationX(float startPoint, SplineControlPoints controls)
        {
            float first = (controls.m_controlPt1.x - controls.m_controlPt0.x) * 3f;
            float middle = ((controls.m_controlPt2.x - controls.m_controlPt1.x) * 3f) * 2f;
            float last = (controls.m_controlPt3.x - controls.m_controlPt2.x) * 3f;
            float secondFirst = (controls.m_controlPt0.x + (controls.m_controlPt2.x - controls.m_controlPt1.x * 2f)) * 6f;
            float secondLast = (controls.m_controlPt1.x + (controls.m_controlPt3.x - controls.m_controlPt2.x * 2f)) * 6f;
            for (int i = 0; i < 11; ++i)
            {
                float inverse = 1f - startPoint;
                float derivative = ((startPoint * startPoint) * last) + ((inverse * inverse) * first + inverse * (middle * startPoint));
                float secondDerivative = inverse * secondFirst + secondLast * startPoint;
                float next = startPoint - derivative / secondDerivative;
                if (Mathf.Abs(next - startPoint) < 0.001f) return startPoint;
                startPoint = next;
            }
            return 0f;
        }

        // Original 0600033d, ARM64 01a808b0; Z compares a double threshold, unlike X's Single threshold.
        public static float NewtonRaphsonIterationZ(float startPoint, SplineControlPoints controls)
        {
            float first = (controls.m_controlPt1.z - controls.m_controlPt0.z) * 3f;
            float middle = ((controls.m_controlPt2.z - controls.m_controlPt1.z) * 3f) * 2f;
            float last = (controls.m_controlPt3.z - controls.m_controlPt2.z) * 3f;
            float secondFirst = (controls.m_controlPt0.z + (controls.m_controlPt2.z - controls.m_controlPt1.z * 2f)) * 6f;
            float secondLast = (controls.m_controlPt1.z + (controls.m_controlPt3.z - controls.m_controlPt2.z * 2f)) * 6f;
            for (int i = 0; i < 11; ++i)
            {
                float inverse = 1f - startPoint;
                float derivative = ((startPoint * startPoint) * last) + ((inverse * inverse) * first + inverse * (middle * startPoint));
                float secondDerivative = inverse * secondFirst + secondLast * startPoint;
                float next = startPoint - derivative / secondDerivative;
                if ((double)Mathf.Abs(next - startPoint) < 0.001) return startPoint;
                startPoint = next;
            }
            return 0f;
        }

        // Original 0600033e, ARM64 01a80ca0: align endpoint direction to forward; retain the supplied array.
        public static void CalculateBounds(ref Vector3[] box, int numSteps, SplineControlPoints controls)
        {
            Vector3 origin = controls.m_controlPt0;
            Vector3 second = controls.m_controlPt1 - origin;
            Vector3 third = controls.m_controlPt2 - origin;
            Vector3 last = controls.m_controlPt3 - origin;
            Quaternion rotation = Quaternion.FromToRotation(last, Vector3.forward);
            SplineControlPoints local = new SplineControlPoints
            {
                m_controlPt0 = Vector3.zero,
                m_controlPt1 = rotation * second,
                m_controlPt2 = rotation * third,
                m_controlPt3 = rotation * last
            };
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            CalculateBoundsAxisAligned(ref min, ref max, numSteps, local);
            box[0] = new Vector3(min.x, min.y, min.z);
            box[1] = new Vector3(min.x, min.y, max.z);
            box[2] = new Vector3(max.x, min.y, max.z);
            box[3] = new Vector3(max.x, min.y, min.z);
            box[4] = new Vector3(min.x, max.y, min.z);
            box[5] = new Vector3(min.x, max.y, max.z);
            box[6] = new Vector3(max.x, max.y, max.z);
            box[7] = new Vector3(max.x, max.y, min.z);
            for (int i = 0; i < 8; ++i)
            {
                box[i] = Quaternion.Inverse(rotation) * box[i];
                box[i] += controls.m_controlPt0;
            }
        }

        // Original 0600033f, ARM64 01a8122c: fixed 16-root scratch storage, X/Z extrema only.
        public static void CalculateBoundsAxisAligned(ref Vector3 min, ref Vector3 max, int numSteps, SplineControlPoints controls)
        {
            float[] roots = new float[16];
            int rootCount = 0;
            for (int i = 0; i < unchecked(numSteps + 1); ++i)
            {
                float start = (float)i / numSteps;
                float root = NewtonRaphsonIterationX(start, controls);
                if (root > 0f && root < 1f)
                {
                    bool found = false;
                    for (int j = 0; j < rootCount; ++j)
                        if ((double)Mathf.Abs(root - roots[j]) <= 0.001) { found = true; break; }
                    if (!found) roots[rootCount++] = root;
                }
                root = NewtonRaphsonIterationZ(start, controls);
                if (root > 0f && root < 1f)
                {
                    bool found = false;
                    for (int j = 0; j < rootCount; ++j)
                        if ((double)Mathf.Abs(root - roots[j]) <= 0.001) { found = true; break; }
                    if (!found) roots[rootCount++] = root;
                }
            }
            min = Vector3.Min(controls.m_controlPt0, controls.m_controlPt3);
            max = Vector3.Max(controls.m_controlPt0, controls.m_controlPt3);
            for (int i = 0; i < rootCount; ++i)
            {
                Vector3 point = PointAlongSpline(roots[i], ref controls);
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }
        }
    }
}
