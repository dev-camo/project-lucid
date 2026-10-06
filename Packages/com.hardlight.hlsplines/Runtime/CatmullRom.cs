using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class CatmullRom
    {
        // Original 06000348, ARM64 01a81a28; cached coefficients are intentionally not invalidated.
        public static Vector3 PointAlongSpline(float t, ref SplineControlPoints controls)
        {
            if (!controls.m_cacheSet)
            {
                controls.m_cacheVector1 = controls.m_controlPt1 * 2f;
                controls.m_cacheVector2 = controls.m_controlPt2 - controls.m_controlPt0;
                controls.m_cacheVector3 = controls.m_controlPt0 * 2f + controls.m_controlPt1 * -5f + controls.m_controlPt2 * 4f - controls.m_controlPt3;
                controls.m_cacheVector4 = controls.m_controlPt1 * 3f - controls.m_controlPt0 - controls.m_controlPt2 * 3f + controls.m_controlPt3;
                controls.m_cacheSet = true;
            }
            float tSquared = t * t;
            float tCubed = tSquared * t;
            return (controls.m_cacheVector1 + controls.m_cacheVector2 * t + controls.m_cacheVector3 * tSquared + controls.m_cacheVector4 * tCubed) * 0.5f;
        }

        // Original 06000349, ARM64 01a81b6c: the half factor is absent from the raw tangent.
        public static Vector3 TangentAlongSpline(float t, ref SplineControlPoints controls)
        {
            if (!controls.m_cacheSet)
            {
                controls.m_cacheVector1 = controls.m_controlPt1 * 2f;
                controls.m_cacheVector2 = controls.m_controlPt2 - controls.m_controlPt0;
                controls.m_cacheVector3 = controls.m_controlPt0 * 2f + controls.m_controlPt1 * -5f + controls.m_controlPt2 * 4f - controls.m_controlPt3;
                controls.m_cacheVector4 = controls.m_controlPt1 * 3f - controls.m_controlPt0 - controls.m_controlPt2 * 3f + controls.m_controlPt3;
                controls.m_cacheSet = true;
            }
            return (controls.m_cacheVector2 + controls.m_cacheVector3 * (t * 2f) + controls.m_cacheVector4 * ((t * t) * 3f)).normalized;
        }

        // Original 0600034a, ARM64 01a81d70: write the unscaled tangent magnitude before returning.
        public static PositionAndTangent PointAndTangentAlongSpline(ref float tangentMagnitude, float t, ref SplineControlPoints controls)
        {
            if (!controls.m_cacheSet)
            {
                controls.m_cacheVector1 = controls.m_controlPt1 * 2f;
                controls.m_cacheVector2 = controls.m_controlPt2 - controls.m_controlPt0;
                controls.m_cacheVector3 = controls.m_controlPt0 * 2f + controls.m_controlPt1 * -5f + controls.m_controlPt2 * 4f - controls.m_controlPt3;
                controls.m_cacheVector4 = controls.m_controlPt1 * 3f - controls.m_controlPt0 - controls.m_controlPt2 * 3f + controls.m_controlPt3;
                controls.m_cacheSet = true;
            }
            // Native caches coefficient values before the output ref write; the output can alias a cache component.
            Vector3 coefficient1 = controls.m_cacheVector1;
            Vector3 coefficient2 = controls.m_cacheVector2;
            Vector3 coefficient3 = controls.m_cacheVector3;
            Vector3 coefficient4 = controls.m_cacheVector4;
            float tSquared = t * t;
            Vector3 tangent = coefficient2 + coefficient3 * (t * 2f) + coefficient4 * (tSquared * 3f);
            tangentMagnitude = tangent.magnitude;
            tangent.Normalize();
            Vector3 position = (coefficient1 + coefficient2 * t + coefficient3 * tSquared + coefficient4 * (tSquared * t)) * 0.5f;
            return new PositionAndTangent(position, tangent);
        }

        // Original 0600034b/06000351: natural cached lambda at original outer ordinal 3.
        public static float CalculateSegmentLengthRecursive(float startT, float endT, ref SplineControlPoints controls)
        {
            return SplineCommon.CalculateSegmentLengthRecursive(startT, endT, ref controls, (float f, ref SplineControlPoints points) => PointAlongSpline(f, ref points));
        }

        // Original 0600034c, ARM64 01a82208.
        public static float CalculateLength(ref SplineControlPoints controls)
        {
            return CalculateSegmentLengthRecursive(0f, 1f, ref controls);
        }

        // Original 0600034d, ARM64 01a82214.
        public static float FindNearestPointOnSpline(Vector3 point, ref SplineControlPoints controls)
        {
            return FindNearestPointOnSpline(point, ref controls, 0.001f);
        }

        // Original 0600034e, ARM64 01a82224: keep the best distance and stop on equal neighbours.
        public static float FindNearestPointOnSpline(Vector3 point, ref SplineControlPoints controls, float epsilon)
        {
            float currentT = 0.5f;
            float currentDistance = (PointAlongSpline(currentT, ref controls) - point).sqrMagnitude;
            float step = 0.5f;
            while (step > epsilon)
            {
                step *= 0.5f;
                float leftT = currentT - step;
                float leftDistance = (PointAlongSpline(leftT, ref controls) - point).sqrMagnitude;
                float rightT = currentT + step;
                float rightDistance = (PointAlongSpline(rightT, ref controls) - point).sqrMagnitude;
                if (currentDistance <= leftDistance && currentDistance <= rightDistance) continue;
                if (leftDistance < rightDistance)
                {
                    currentDistance = leftDistance;
                    currentT = leftT;
                }
                else if (leftDistance > rightDistance)
                {
                    currentDistance = rightDistance;
                    currentT = rightT;
                }
                else break;
            }
            return currentT;
        }
    }
}
