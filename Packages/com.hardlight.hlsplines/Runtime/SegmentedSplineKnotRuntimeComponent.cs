using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    internal static class SegmentedSplineKnotRuntimeComponent
    {
        internal static SegmentSplinePoint[] CalculateSegments(ISplineKnotRuntimeHandle knotHandle, ISplineRuntimeHandle splineHandle, out float knotLength)
        {
            knotLength = 0f;
            int knotIndex = splineHandle.GetKnotRuntimeHandleIndex(knotHandle);
            if (knotIndex < 0) throw new UnityException("knot handle is not part of given spline");
            int segmentCount = Mathf.CeilToInt(knotHandle.Length);
            if (segmentCount == 0) return null;
            SegmentSplinePoint[] points = new SegmentSplinePoint[segmentCount + 1];
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 start = splineHandle.GetLocalPosFromLinearRatio(new LinearRatio(knotIndex, (float)i / segmentCount));
                Vector3 end = splineHandle.GetLocalPosFromLinearRatio(new LinearRatio(knotIndex, (float)(i + 1) / segmentCount));
                Vector3 direction = (end - start).normalized;
                float length = (end - start).magnitude;
                points[i] = new SegmentSplinePoint
                {
                    m_position = start,
                    m_normalisedDirection = direction,
                    m_startDotDirection = Vector3.Dot(start, direction),
                    m_endDotDirection = Vector3.Dot(end, direction),
                    m_length = length,
                    m_lengthInverse = 1f / length
                };
                knotLength += points[i].m_length;
                if (i == points.Length - 2)
                    points[i + 1] = new SegmentSplinePoint { m_position = end, m_normalisedDirection = Vector3.zero };
            }
            return OptimiseSegments(points, out knotLength);
        }

        private static SegmentSplinePoint[] OptimiseSegments(SegmentSplinePoint[] points, out float length)
        {
            List<SegmentSplinePoint> optimised = new List<SegmentSplinePoint>(points);
            int i = 0;
            float accumulatedAngle = 0f;
            while (i < optimised.Count - 2)
            {
                Vector3 start = optimised[i].m_position;
                Vector3 middle = optimised[i + 1].m_position;
                Vector3 end = optimised[i + 2].m_position;
                accumulatedAngle += Vector3.Angle((middle - start).normalized, (end - middle).normalized);
                if (accumulatedAngle < 5f)
                {
                    optimised.RemoveAt(i + 1);
                    SegmentSplinePoint point = optimised[i];
                    point.m_normalisedDirection = (end - start).normalized;
                    point.m_length = (end - start).magnitude;
                    point.m_lengthInverse = 1f / point.m_length;
                    optimised[i] = point;
                }
                else
                {
                    i++;
                    accumulatedAngle = 0f;
                }
            }
            length = 0f;
            for (i = 0; i < optimised.Count - 1; i++)
            {
                SegmentSplinePoint point = optimised[i];
                Vector3 next = optimised[i + 1].m_position;
                point.m_startDotDirection = Vector3.Dot(point.m_position, point.m_normalisedDirection);
                point.m_endDotDirection = Vector3.Dot(next, point.m_normalisedDirection);
                optimised[i] = point;
                length += (optimised[i].m_position - optimised[i + 1].m_position).magnitude;
            }
            return optimised.ToArray();
        }
    }
}
