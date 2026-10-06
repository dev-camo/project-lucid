using UnityEngine;

namespace Hardlight
{
    public static class SplineCommon
    {
        public delegate Vector3 PointAlongSpline(float t, ref SplineControlPoints controls);

        // Original 0600035c, ARM64 01a7fda0: evaluate start, end, midpoint in order.
        public static float CalculateSegmentLengthRecursive(float startT, float endT, ref SplineControlPoints controls, PointAlongSpline pointAlongSpline)
        {
            Vector3 startPos = pointAlongSpline(startT, ref controls);
            Vector3 endPos = pointAlongSpline(endT, ref controls);
            float length = (endPos - startPos).magnitude;
            float midT = (startT + endT) * 0.5f;
            Vector3 midPos = pointAlongSpline(midT, ref controls);
            float firstLength = (startPos - midPos).magnitude;
            float secondLength = (endPos - midPos).magnitude;
            float sum = firstLength + secondLength;
            if ((double)sum > 0.1 && (double)Mathf.Abs(length - sum) > 0.000001)
                return CalculateSegmentLengthRecursiveInner(startT, startPos, midT, midPos, firstLength, ref controls, pointAlongSpline)
                     + CalculateSegmentLengthRecursiveInner(midT, midPos, endT, endPos, secondLength, ref controls, pointAlongSpline);
            return sum;
        }

        // Original 0600035d, ARM64 01a830e0: carry endpoint values, left recursion first.
        private static float CalculateSegmentLengthRecursiveInner(float startT, Vector3 startPos, float endT, Vector3 endPos, float length, ref SplineControlPoints controls, PointAlongSpline pointAlongSpline)
        {
            float midT = (startT + endT) * 0.5f;
            Vector3 midPos = pointAlongSpline(midT, ref controls);
            float firstLength = (startPos - midPos).magnitude;
            float secondLength = (endPos - midPos).magnitude;
            float sum = firstLength + secondLength;
            if ((double)sum > 0.1 && (double)Mathf.Abs(length - sum) > 0.000001)
                return CalculateSegmentLengthRecursiveInner(startT, startPos, midT, midPos, firstLength, ref controls, pointAlongSpline)
                     + CalculateSegmentLengthRecursiveInner(midT, midPos, endT, endPos, secondLength, ref controls, pointAlongSpline);
            return sum;
        }
    }
}
