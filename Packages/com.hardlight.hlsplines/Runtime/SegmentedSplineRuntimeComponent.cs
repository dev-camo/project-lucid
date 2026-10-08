using UnityEngine;

namespace Hardlight
{
    internal static class SegmentedSplineRuntimeComponent
    {
        internal static LinearRatio GetLinearRatioFromKnotT(ISegmentedSplineRuntimeHandle splineHandle, KnotT knotT) =>
            new LinearRatio(knotT.KnotIndex, knotT.T);

        internal static KnotT GetKnotTFromLinearRatio(ISegmentedSplineRuntimeHandle splineHandle, LinearRatio linearRatio) =>
            new KnotT(linearRatio.KnotIndex, linearRatio.T);

        internal static FindAdjacentResult FindAdjacentKnotPosition(ISegmentedSplineRuntimeHandle splineHandle, Vector3 position, Vector3 forward, int knotIndex)
        {
            ISegmentedSplineKnotRuntimeHandle knot = splineHandle.GetKnotRuntimeHandle(knotIndex);
            int last = knot.SegmentSplinePoints.Length - 1;
            float positionDot = Vector3.Dot(position, forward);
            Vector3 knotPosition = Vector3.zero;
            float traversedRatio = 0f;
            float t = 0f;
            bool hit = false;
            for (int i = 0; i < last; i++)
            {
                Vector3 start = knot.SegmentSplinePoints[i].m_position;
                Vector3 end = knot.SegmentSplinePoints[i + 1].m_position;
                float startDot = Vector3.Dot(forward, start);
                float endDot = Vector3.Dot(forward, end);
                if ((positionDot - startDot) * (positionDot - endDot) <= 0f)
                {
                    float segmentT = (positionDot - startDot) / (endDot - startDot);
                    t = traversedRatio + segmentT * knot.SegmentSplinePoints[i].m_length / knot.Length;
                    hit = true;
                    break;
                }
                traversedRatio += knot.SegmentSplinePoints[i].m_length / knot.Length;
            }
            if (!hit)
            {
                Vector3 start = knot.SegmentSplinePoints[0].m_position;
                Vector3 end = knot.SegmentSplinePoints[last].m_position;
                t = Mathf.Abs(positionDot - Vector3.Dot(forward, start)) <= Mathf.Abs(positionDot - Vector3.Dot(forward, end)) ? 0f : 1f;
            }
            KnotT knotT = GetKnotTFromLinearRatio(splineHandle, new LinearRatio(knotIndex, t));
            // Original 0x060004c3 performs this call but publishes the zero local.
            PointAlongSpline(splineHandle, knotT);
            return new FindAdjacentResult { KnotPosition = knotPosition, KnotT = knotT, HitOnKnot = hit };
        }

        internal static void FindNearestSplinePosition(ISegmentedSplineRuntimeHandle splineHandle, Vector3 position, Vector3? planeNormal, int startKnot, out Vector3 splinePosition, out LinearRatio knotLinearRatio)
        {
            int last = splineHandle.KnotCount - 1;
            int start = startKnot > 0 ? Mathf.Min(startKnot, last) : 0;
            KnotT bestKnotT = new KnotT(-1, 0f);
            Vector3 bestPosition = Vector3.zero;
            float bestDistance = float.MaxValue;
            for (int i = start - 1; i >= 0; i--)
            {
                FindNearestKnotPosition(splineHandle, position, planeNormal, i, out Vector3 candidate, out KnotT candidateT, out float distance);
                if (startKnot >= 0 && distance > bestDistance) break;
                if (!(distance >= bestDistance))
                {
                    bestPosition = candidate;
                    bestKnotT = candidateT;
                    bestDistance = distance;
                }
            }
            float previousT = 1f;
            for (int i = start; i < last; i++)
            {
                FindNearestKnotPosition(splineHandle, position, planeNormal, i, out Vector3 candidate, out KnotT candidateT, out float distance);
                if (distance > bestDistance && startKnot >= 0 && !(previousT >= 1f) && !Mathf.Approximately(previousT, 1f)) break;
                if (!(distance >= bestDistance))
                {
                    bestPosition = candidate;
                    bestKnotT = candidateT;
                    bestDistance = distance;
                }
                previousT = candidateT.T;
            }
            splinePosition = bestPosition;
            knotLinearRatio = GetLinearRatioFromKnotT(splineHandle, bestKnotT);
        }

        public static void FindNearestKnotPosition(ISegmentedSplineRuntimeHandle splineHandle, Vector3 position, Vector3? planeNormal, int knotIndex, out Vector3 knotPosition, out KnotT knotT, out float sqrDistance)
        {
            ISegmentedSplineKnotRuntimeHandle knot = splineHandle.GetKnotRuntimeHandle(knotIndex);
            int count = knot.SegmentSplinePoints.Length;
            float inverse = knot.LengthInverse;
            SegmentSplinePoint previous = knot.SegmentSplinePoints[0];
            Vector3 bestPosition = position;
            float bestDistance = float.MaxValue;
            float bestRatio = 0f;
            float traversed = 0f;
            for (int i = 1; i < count; i++)
            {
                SegmentSplinePoint next = knot.SegmentSplinePoints[i];
                float dot = Vector3.Dot(position, previous.m_normalisedDirection);
                float minimum = Mathf.Min(previous.m_startDotDirection, previous.m_endDotDirection);
                float maximum = Mathf.Max(previous.m_startDotDirection, previous.m_endDotDirection);
                float projected = Mathf.Clamp(dot, minimum, maximum);
                float t = (projected - previous.m_startDotDirection) / (previous.m_endDotDirection - previous.m_startDotDirection);
                Vector3 candidate = previous.m_position + (next.m_position - previous.m_position) * t;
                float distance;
                if (planeNormal.HasValue)
                {
                    Vector3 projectedCandidate = Vector3.ProjectOnPlane(candidate, planeNormal.Value);
                    Vector3 projectedPosition = Vector3.ProjectOnPlane(position, planeNormal.Value);
                    distance = (projectedCandidate - projectedPosition).sqrMagnitude;
                }
                else distance = (candidate - position).sqrMagnitude;
                if (distance > bestDistance) break;
                bestPosition = candidate;
                bestRatio = (traversed + previous.m_length * t) * inverse;
                bestDistance = distance;
                traversed += previous.m_length;
                previous = next;
            }
            knotPosition = bestPosition;
            knotT = GetKnotTFromLinearRatio(splineHandle, new LinearRatio(knotIndex, bestRatio));
            sqrDistance = bestDistance;
        }

        internal static void GetSplineLocationFromDistance(ISegmentedSplineRuntimeHandle splineHandle, float distance, out LinearRatio knotLinearRatio)
        {
            int last = splineHandle.KnotCount - 1;
            int i = 0;
            float traversed = 0f;
            float start = 0f;
            float inverse = 0f;
            for (; i < last; i++)
            {
                ISegmentedSplineKnotRuntimeHandle knot = splineHandle.GetKnotRuntimeHandle(i);
                inverse = knot.LengthInverse;
                start = traversed;
                if (start + knot.Length > distance) break;
                traversed += knot.Length;
            }
            float t = last > 0 ? (distance - start) * inverse : 0f;
            knotLinearRatio = new LinearRatio(i, Mathf.Clamp01(t));
        }

        internal static Vector3 PointAlongSpline(ISegmentedSplineRuntimeHandle splineHandle, KnotT knotT)
        {
            ISegmentedSplineKnotRuntimeHandle knot = splineHandle.GetKnotRuntimeHandle(knotT.KnotIndex);
            float length = knot.Length;
            SegmentSplinePoint[] points = knot.SegmentSplinePoints;
            float distance = length * knotT.T;
            for (int i = 0; i < points.Length - 1; i++)
            {
                if (points[i].m_length > distance)
                    return points[i].m_position + points[i].m_normalisedDirection * distance;
                distance -= points[i].m_length;
            }
            return points[points.Length - 1].m_position;
        }

        internal static LightweightTransform PointAndRotationAlongSpline(ISegmentedSplineRuntimeHandle splineHandle, KnotT knotT)
        {
            int count = splineHandle.KnotCount;
            PositionAndTangent point = PointAndTangentAlongSpline(splineHandle, knotT);
            ISegmentedSplineKnotRuntimeHandle knot = splineHandle.GetKnotRuntimeHandle(knotT.KnotIndex);
            ISegmentedSplineKnotRuntimeHandle next = null;
            if (knotT.KnotIndex < count - 1) next = splineHandle.GetKnotRuntimeHandle(knotT.KnotIndex + 1);
            Vector3 up = next != null ? Vector3.Slerp(knot.Transform.Up, next.Transform.Up, knotT.T) : knot.Transform.Up;
            return new LightweightTransform(point.Position, Quaternion.LookRotation(point.Tangent, up));
        }

        internal static PositionAndTangent PointAndTangentAlongSpline(ISegmentedSplineRuntimeHandle splineHandle, KnotT knotT)
        {
            int count = splineHandle.KnotCount;
            int knotIndex = knotT.KnotIndex;
            ISegmentedSplineKnotRuntimeHandle knot = splineHandle.GetKnotRuntimeHandle(knotIndex);
            if (knot.SegmentSplinePoints == null || knot.SegmentSplinePoints.Length == 0)
            {
                knotIndex += knotIndex > 0 ? -1 : 1;
                knot = splineHandle.GetKnotRuntimeHandle(knotIndex);
            }
            float distance = knot.Length * knotT.T;
            for (int i = 0; i < knot.SegmentSplinePoints.Length - 1; i++)
            {
                SegmentSplinePoint point = knot.SegmentSplinePoints[i];
                if (distance > point.m_length)
                {
                    distance -= point.m_length;
                    continue;
                }
                Vector3 position = point.m_position + point.m_normalisedDirection * distance;
                Vector3 tangent = point.m_normalisedDirection;
                Vector3 nextTangent = tangent;
                if (i < knot.SegmentSplinePoints.Length - 2)
                    nextTangent = knot.SegmentSplinePoints[i + 1].m_normalisedDirection;
                else if (knotIndex < count - 1)
                {
                    ISegmentedSplineKnotRuntimeHandle next = splineHandle.GetKnotRuntimeHandle(knotIndex + 1);
                    if (next.SegmentSplinePoints != null)
                    {
                        if (next.SegmentSplinePoints.Length > 1)
                            nextTangent = next.SegmentSplinePoints[0].m_normalisedDirection;
                        else nextTangent = knot.SegmentSplinePoints[knot.SegmentSplinePoints.Length - 2].m_normalisedDirection;
                    }
                    else
                    {
                        // Both native versions retain the calculated position and tangent here;
                        // the null branch bypasses copying their cleared scratch registers back.
                        nextTangent = knot.SegmentSplinePoints[knot.SegmentSplinePoints.Length - 2].m_normalisedDirection;
                    }
                }
                return new PositionAndTangent(position, Vector3.Slerp(tangent, nextTangent, distance * point.m_lengthInverse));
            }
            if (knot.SegmentSplinePoints.Length >= 2)
                return new PositionAndTangent(knot.SegmentSplinePoints[knot.SegmentSplinePoints.Length - 1].m_position,
                    knot.SegmentSplinePoints[knot.SegmentSplinePoints.Length - 2].m_normalisedDirection);
            return default;
        }

        private static float GetCumulativeDistanceToKnot(ISegmentedSplineRuntimeHandle splineHandle, int knotIndex)
        {
            float distance = 0f;
            for (int i = 0; i <= knotIndex; i++) distance += splineHandle.GetKnotRuntimeHandle(i).Length;
            return distance;
        }
    }
}
