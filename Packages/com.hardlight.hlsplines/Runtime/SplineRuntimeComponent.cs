using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    // HLSplines.Runtime 0x0200005a: shared spline queries and LUT/bounds calculations.
    // Calls preserve the original spline and knot contracts; complete provider execution is still unverified.
    internal static class SplineRuntimeComponent
    {
        private const int SampleLUTSize = 2048;
        private static readonly float[] s_lutCache = new float[SampleLUTSize];
        private const float EndEdgeDistanceTolerance = .1f;

        // Original 0x060001c9.
        public static SplineLocation FindAdjacentSplineLocation(ISpline spline, ISplineRuntimeHandle splineHandle, Transform transform, Vector3 worldPosition, Vector3 forward, Func<Vector3, Vector3, int, FindAdjacentResult> findAdjacentCallback)
        {
            Vector3 localPosition = transform.worldToLocalMatrix.MultiplyPoint3x4(worldPosition);
            Vector3 localForward = transform.rotation * forward;
            Vector3 adjacent;
            LinearRatio ratio;
            FindAdjacentSplinePosition(splineHandle, localPosition, localForward, out adjacent, out ratio, findAdjacentCallback);
            SurfaceKnotMetadata metadata = GetKnotMetadataFromLinearRatio(splineHandle, ratio);
            return new SplineLocation { m_spline = spline, m_knotLinearRatio = ratio, m_metadata = metadata };
        }

        // Original 0x060001ca.
        internal static int GetKnotIndex(ISplineRuntimeHandle splineHandle, ISplineKnotRuntimeHandle knotHandle) { int count = splineHandle.KnotCount; for (int i = 0; i < count; i++) if (splineHandle.GetKnotRuntimeHandle(i) == knotHandle) return i; return -1; }

        // Original 0x060001cb.
        internal static ISplineKnotRuntimeHandle GetKnot(ISplineRuntimeHandle splineHandle, int knotIndex) { if (splineHandle.KnotCount < 1) return null; return splineHandle.GetKnotRuntimeHandle(knotIndex); }

        // Original 0x060001cc.
        internal static SurfaceKnotMetadata GetKnotMetadataFromDistance(ISplineRuntimeHandle splineHandle, float distance)
        {
            int knotCount = splineHandle.KnotCount;
            if (knotCount == 0) return new SurfaceKnotMetadata(splineHandle.Metadata);
            float accumulated = 0f;
            for (int i = 0; i < knotCount; ++i)
            {
                ISplineKnotRuntimeHandle knot = splineHandle.GetKnotRuntimeHandle(i);
                if (accumulated + knot.Length > distance)
                    return GetKnotMetadataFromLinearRatio(splineHandle,
                        new LinearRatio(i, (distance - accumulated) * knot.LengthInverse));
                accumulated += knot.Length;
            }
            return new SurfaceKnotMetadata(splineHandle.Metadata,
                splineHandle.GetKnotRuntimeHandle(knotCount - 1).Metadata);
        }

        // Original 0x060001cd.
        internal static SurfaceKnotMetadata GetKnotMetadataFromKnotT(ISplineRuntimeHandle splineHandle, KnotT knotT)
        {
            if (knotT.KnotIndex == splineHandle.KnotCount - 1)
                return new SurfaceKnotMetadata(splineHandle.Metadata,
                    splineHandle.GetKnotRuntimeHandle(knotT.KnotIndex).Metadata);
            ISplineKnotRuntimeHandle knot = splineHandle.GetKnotRuntimeHandle(knotT.KnotIndex);
            LinearRatio ratio = GetLinearRatioFromKnotT(knot, knotT);
            return GetKnotMetadataFromLinearRatio(splineHandle, ratio);
        }

        // Original 0x060001ce.
        internal static SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(ISplineRuntimeHandle splineHandle, LinearRatio linearRatio) { if (linearRatio.KnotIndex == splineHandle.KnotCount - 1) return new SurfaceKnotMetadata(splineHandle.Metadata, splineHandle.GetKnotRuntimeHandle(linearRatio.KnotIndex).Metadata); return new SurfaceKnotMetadata(splineHandle.Metadata, splineHandle.GetKnotRuntimeHandle(linearRatio.KnotIndex).Metadata, splineHandle.GetKnotRuntimeHandle(linearRatio.KnotIndex + 1).Metadata, linearRatio.T); }

        // Original 0x060001cf.
        private static LinearRatio GetLinearRatioFromLerpT(ISplineKnotHandle knotHandle, KnotT knotT, int lutIndex) { int count = knotHandle.TLUT.Length; float left = knotHandle.GetLUTValue(lutIndex); float right = knotHandle.GetLUTValue(lutIndex + 1); return new LinearRatio(knotT.KnotIndex, Mathf.Lerp((float)lutIndex / count, (float)(lutIndex + 1) / count, (knotT.T - left) / (right - left))); }

        // Original 0x060001d0.
        internal static LinearRatio GetLinearRatioFromKnotT(ISplineKnotHandle knotHandle, KnotT knotT) { int count = knotHandle.TLUT.Length; float t = knotT.T; if (knotHandle.GetLUTValue(0) <= t && knotHandle.GetLUTValue(1) > t) return GetLinearRatioFromLerpT(knotHandle, knotT, 0); int index = count / 2; int step = index; while (index >= 1 && index < count) { if (step >= 2) step /= 2; float left = knotHandle.GetLUTValue(index); float right = knotHandle.GetLUTValue(index + 1); if (left <= t && right > t) return GetLinearRatioFromLerpT(knotHandle, knotT, index); index += left < t ? step : -step; } return new LinearRatio(knotT.KnotIndex, 1f); }

        // Original 0x060001d1.
        internal static KnotT GetKnotTFromLinearRatio(ISplineRuntimeHandle splineHandle, LinearRatio linearRatio) { ISplineKnotRuntimeHandle knot = splineHandle.GetKnotRuntimeHandle(linearRatio.KnotIndex); float scaled = linearRatio.T * splineHandle.LUTCount; int index = (int)scaled; float left = knot.GetLUTValue(index); float right = knot.GetLUTValue(index + 1); return new KnotT(linearRatio.KnotIndex, Mathf.Lerp(left, right, scaled - index)); }

        // Original 0x060001d2.
        internal static Vector3 GetApproximateMidPoint(ISplineRuntimeHandle splineHandle) { int count = splineHandle.KnotCount; if (count == 0) return Vector3.zero; Vector3 first = splineHandle.GetKnotRuntimeHandle(0).Transform.Location; Vector3 last = splineHandle.GetKnotRuntimeHandle(count - 1).Transform.Location; return (first + last) * 0.5f; }

        // Original 0x060001d3.
        internal static float RecalculateKnotsLength(ISplineRuntimeHandle splineHandle, bool force = false) { float length = 0f; bool nextDirty = false; for (int i = splineHandle.KnotCount - 1; i >= 0; i--) { ISplineKnotRuntimeHandle knot = splineHandle.GetKnotRuntimeHandle(i); bool dirty = knot.IsLengthDirty(); if (nextDirty || force || dirty) knot.RecalculateLength(); nextDirty = dirty; length += knot.Length; } return length; }

        // Original 0x060001d4.
        internal static float RecalculateKnotLength(ISplineRuntimeHandle splineHandle, int knotIndex, ref float[] lut, int lutCount = -1)
        {
            if (knotIndex >= splineHandle.KnotCount - 1) return 0f;
            SplineControlPoints controls = splineHandle.GetControlsForKnots(knotIndex, knotIndex + 1);
            float length = splineHandle.SplineType == SplineType.Bezier
                ? Bezier.CalculateLength(controls) : CatmullRom.CalculateLength(ref controls);
            // Keep the shared 2048-sample cache and original sampling denominators.
            // The output table stores the sample index fraction, rather than interpolating within a sample.
            s_lutCache[0] = 0f;
            if (lutCount == -1) lutCount = splineHandle.LUTCount;
            if (lut == null || lut.Length != lutCount) lut = new float[lutCount];
            for (int i = 1; i < SampleLUTSize; ++i)
                s_lutCache[i] = splineHandle.SplineType == SplineType.Bezier
                    ? Bezier.CalculateSegmentLengthRecursive((i - 1) / 2047f, i / 2047f, ref controls)
                    : CatmullRom.CalculateSegmentLengthRecursive((i - 1) / 2047f, i / 2047f, ref controls);
            for (int i = 0; i < SampleLUTSize - 1; ++i)
                s_lutCache[i + 1] += s_lutCache[i];
            int sampleIndex = 0;
            for (int i = 0; i < lut.Length; ++i)
            {
                float distance = length * i / lut.Length;
                for (int j = sampleIndex; j < SampleLUTSize - 1; ++j)
                {
                    if (s_lutCache[j] <= distance && s_lutCache[j + 1] > distance)
                    {
                        lut[i] = j * (1f / 2048f);
                        sampleIndex = j;
                        break;
                    }
                }
            }
            return length;
        }

        // Original 0x060001d5.
        internal static SplineControlPoints GetControlsForKnots(ISplineRuntimeHandle splineHandle, int knot0Index, int knot1Index)
        {
            if (splineHandle.SplineType == SplineType.Bezier)
            {
                ISplineKnotRuntimeHandle knot0 = splineHandle.GetKnotRuntimeHandle(knot0Index);
                ISplineKnotRuntimeHandle knot1 = splineHandle.GetKnotRuntimeHandle(knot1Index);
                Vector3 position0 = knot0.Transform.Location;
                Vector3 tangentNext = knot0.TangentNext;
                Vector3 position1 = knot1.Transform.Location;
                Vector3 tangentPrev = knot1.TangentPrev;
                return new SplineControlPoints {
                    m_controlPt0 = position0, m_controlPt1 = position0 + tangentNext,
                    m_controlPt2 = position1 + tangentPrev, m_controlPt3 = position1 };
            }
            ISplineKnotRuntimeHandle control0 = splineHandle.GetKnotRuntimeHandle(Mathf.Max(knot0Index - 1, 0));
            ISplineKnotRuntimeHandle control1 = splineHandle.GetKnotRuntimeHandle(knot0Index);
            ISplineKnotRuntimeHandle control2 = splineHandle.GetKnotRuntimeHandle(knot1Index);
            ISplineKnotRuntimeHandle control3 = splineHandle.GetKnotRuntimeHandle(Mathf.Min(knot1Index + 1, splineHandle.KnotCount - 1));
            return new SplineControlPoints { m_controlPt0 = control0.Transform.Location,
                m_controlPt1 = control1.Transform.Location, m_controlPt2 = control2.Transform.Location,
                m_controlPt3 = control3.Transform.Location };
        }

        // Original 0x060001d6.
        internal static Vector3 PointAlongSpline(ISplineRuntimeHandle splineHandle, float t, ref SplineControlPoints controls) { return splineHandle.SplineType == SplineType.Bezier ? Bezier.PointAlongSpline(t, ref controls) : CatmullRom.PointAlongSpline(t, ref controls); }

        // Original 0x060001d7.
        internal static LightweightTransform PointAndRotationAlongSpline(ISplineRuntimeHandle splineHandle, KnotT knotT)
        {
            int nextIndex = knotT.KnotIndex + 1;
            SplineControlPoints controls = GetControlsForKnots(splineHandle, knotT.KnotIndex, nextIndex);
            float tangentMagnitude = 0f;
            PositionAndTangent point = PointAndTangentAlongSpline(splineHandle, ref tangentMagnitude, knotT.T, ref controls);
            int knotCount = splineHandle.KnotCount;
            Vector3 up = Vector3.zero;
            if (nextIndex < knotCount)
            {
                ISplineKnotRuntimeHandle knot0 = splineHandle.GetKnotRuntimeHandle(knotT.KnotIndex);
                ISplineKnotRuntimeHandle knot1 = splineHandle.GetKnotRuntimeHandle(nextIndex);
                Vector3 up0 = knot0.Transform.Up;
                Vector3 up1 = knot1.Transform.Up;
                LinearRatio ratio = splineHandle.GetLinearRatioFromKnotT(knotT);
                up = Vector3.Lerp(up0, up1, ratio.T);
            }
            else up = splineHandle.GetKnotRuntimeHandle(knotCount - 1).Transform.Up;
            Quaternion rotation = Quaternion.LookRotation(point.Tangent, up);
            return new LightweightTransform(point.Position, rotation);
        }

        // Original 0x060001d8.
        internal static PositionAndTangent PointAndTangentAlongSpline(ISplineRuntimeHandle splineHandle, KnotT knotT, bool reverse) { PositionAndTangent result = splineHandle.GetLocalPosAndTangentFromKnotT(knotT); return new PositionAndTangent(result.Position, reverse ? -result.Tangent : result.Tangent); }

        // Original 0x060001d9.
        internal static PositionAndTangent PointAndTangentAlongSpline(ISplineRuntimeHandle splineHandle, ref float tangentMagnitude, float t, ref SplineControlPoints controls)
        {
            return splineHandle.SplineType == SplineType.Bezier
                ? Bezier.PointAndTangentAlongSpline(ref tangentMagnitude, t, ref controls)
                : CatmullRom.PointAndTangentAlongSpline(ref tangentMagnitude, t, ref controls);
        }

        // Original 0x060001da.
        private static float FindNearestPointOnSpline(ISplineRuntimeHandle splineHandle, Vector3 point, ref SplineControlPoints controls)
        {
            return splineHandle.SplineType == SplineType.Bezier
                ? Bezier.FindNearestPointOnSpline(point, controls)
                : CatmullRom.FindNearestPointOnSpline(point, ref controls);
        }

        // Original 0x060001db.
        internal static void FindNearestKnotPositionOnSpline(ISplineRuntimeHandle splineHandle, Vector3 localPosition, int knotIndex, out Vector3 knotPosition, out KnotT knotT) { SplineControlPoints controls = GetControlsForKnots(splineHandle, knotIndex, knotIndex + 1); float t = FindNearestPointOnSpline(splineHandle, localPosition, ref controls); knotPosition = PointAlongSpline(splineHandle, t, ref controls); knotT = new KnotT(knotIndex, t); }

        // Original 0x060001dc.
        private static bool FindAdjacentSplinePosition(ISplineRuntimeHandle splineHandle, Vector3 position, Vector3 forward, out Vector3 splinePosition, out LinearRatio knotLinearRatio, Func<Vector3, Vector3, int, FindAdjacentResult> findAdjacentCallback)
        {
            int knotCount = splineHandle.KnotCount;
            Vector3 bestPosition = Vector3.zero;
            KnotT bestKnotT = new KnotT(-1, 0f);
            bool bestHit = false;
            float bestAlong = float.MaxValue;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < knotCount - 1; ++i)
            {
                Vector3 start = splineHandle.GetKnotRuntimeHandle(i).Transform.Location;
                Vector3 end = splineHandle.GetKnotRuntimeHandle(i + 1).Transform.Location;
                float startDot = Vector3.Dot(forward, position - start);
                float endDot = Vector3.Dot(forward, position - end);
                FindAdjacentResult result;
                if ((startDot > 0f && endDot > 0f) || (startDot < 0f && endDot < 0f))
                {
                    bool useEnd = Mathf.Abs(endDot) <= Mathf.Abs(startDot);
                    result = new FindAdjacentResult { KnotPosition = useEnd ? end : start,
                        KnotT = new KnotT(i, useEnd ? 1f : 0f), HitOnKnot = false };
                }
                else result = findAdjacentCallback(position, forward, i);
                Vector3 delta = result.KnotPosition - position;
                float along = Mathf.Abs(Vector3.Dot(forward, delta));
                float sqrDistance = delta.sqrMagnitude;
                bool replace = MathUtilities.WithinTolerance(along, bestAlong, .005f)
                    ? sqrDistance < bestDistance : along < bestAlong;
                if (replace)
                {
                    bestKnotT = result.KnotT;
                    bestPosition = result.KnotPosition;
                    bestAlong = along;
                    bestDistance = sqrDistance;
                    bestHit = result.HitOnKnot;
                }
            }
            knotLinearRatio = splineHandle.GetLinearRatioFromKnotT(bestKnotT);
            splinePosition = bestPosition;
            return bestHit;
        }

        // Original 0x060001dd.
        internal static FindAdjacentResult FindAdjacentKnotPosition(ISplineRuntimeHandle splineHandle, Vector3 position, Vector3 forward, int knotIndex)
        {
            ISplineKnotRuntimeHandle knot0 = splineHandle.GetKnotRuntimeHandle(knotIndex);
            ISplineKnotRuntimeHandle knot1 = splineHandle.GetKnotRuntimeHandle(knotIndex + 1);
            Vector3 end = knot1.Transform.Location;
            Vector3 start = knot0.Transform.Location;
            if (Vector3.Dot(forward, end - start) <= 0f) forward = -forward;
            SplineControlPoints controls = GetControlsForKnots(splineHandle, knotIndex, knotIndex + 1);
            float t = .5f;
            float step = .5f;
            Vector3 point;
            float dot;
            do
            {
                point = PointAlongSpline(splineHandle, t, ref controls);
                step *= .5f;
                dot = Vector3.Dot(forward, point - position);
                t += dot >= 0f ? -step : step;
            } while (step > .0001f);
            return new FindAdjacentResult { KnotPosition = point, KnotT = new KnotT(knotIndex, t),
                HitOnKnot = Mathf.Abs(dot) <= .05f };
        }

        // Original 0x060001de.
        internal static bool FindAdjacentLinePosition(Vector3 position, Vector3 forward, Vector3 startLine, Vector3 endLine, out Vector3 linePosition, out float lineTValue)
        {
            if (Vector3.Dot(forward, endLine - startLine) <= 0f) forward = -forward;
            float t = .5f;
            float step = .5f;
            Vector3 point;
            float dot;
            do
            {
                point = Vector3.Lerp(startLine, endLine, t);
                step *= .5f;
                dot = Vector3.Dot(forward, point - position);
                t += dot >= 0f ? -step : step;
            } while (step > .0001f);
            linePosition = point;
            lineTValue = t;
            return Mathf.Abs(dot) <= .05f;
        }

        // Original 0x060001df.
        internal static List<ISplineRuntimeHandle.KnotBounds> CreateKnotBounds(ISplineRuntimeHandle splineHandle)
        {
            int knotCount = splineHandle.KnotCount;
            List<ISplineRuntimeHandle.KnotBounds> result = new List<ISplineRuntimeHandle.KnotBounds>(knotCount);
            for (int i = 0; i < knotCount - 1; ++i)
            {
                Bounds bounds = new Bounds(PointAndRotationAlongSpline(splineHandle, new KnotT(i, 0f)).Location, Vector3.zero);
                bounds.Encapsulate(PointAndRotationAlongSpline(splineHandle, new KnotT(i, 1f)).Location);
                for (int sample = 0; sample < 100; ++sample)
                    bounds.Encapsulate(PointAndRotationAlongSpline(splineHandle, new KnotT(i, sample * .01f)).Location);
                Vector3 size = bounds.size;
                bounds.Expand(Math.Max(size.x, Math.Max(size.y, size.z)) * .1f);
                result.Add(new ISplineRuntimeHandle.KnotBounds { KnotIndex = i, Bounds = bounds });
            }
            return result;
        }

        // Original 0x060001e0.
        internal static void FindNearestSplinePosition(ISplineRuntimeHandle splineHandle, Vector3 localPosition, out Vector3 splinePosition, out LinearRatio knotLinearRatio)
        {
            int knotCount = splineHandle.KnotCount;
            Vector3 bestPosition = Vector3.zero;
            KnotT bestKnotT = new KnotT(-1, 0f);
            List<ISplineRuntimeHandle.KnotBounds> bounds = splineHandle.KnotBoundsList;
            for (int i = 0; i < knotCount - 1; ++i)
            {
                ISplineRuntimeHandle.KnotBounds row = bounds[i];
                row.SqrDistanceToBounds = row.Bounds.SqrDistance(localPosition);
                bounds[i] = row;
            }
            bounds.Sort((a, b) => {
                float delta = a.SqrDistanceToBounds - b.SqrDistanceToBounds;
                if (delta < -.0001f) return -1;
                if (delta > .0001f) return 1;
                return unchecked(a.KnotIndex - b.KnotIndex);
            });
            float bestDistance = float.MaxValue;
            for (int i = 0; i < knotCount - 1; ++i)
            {
                ISplineRuntimeHandle.KnotBounds row = bounds[i];
                if (bestDistance < row.SqrDistanceToBounds) break;
                Vector3 position;
                KnotT knotT;
                splineHandle.FindNearestKnotPosition(localPosition, row.KnotIndex, out position, out knotT);
                float distance = (position - localPosition).sqrMagnitude;
                // Native skips only an ordered greater distance: ties and NaN replace the current choice.
                if (!(distance > bestDistance))
                {
                    bestKnotT = knotT;
                    bestPosition = position;
                    bestDistance = distance;
                    if (distance < .0001f) break;
                }
            }
            knotLinearRatio = splineHandle.GetLinearRatioFromKnotT(bestKnotT);
            splinePosition = bestPosition;
        }

        // Original 0x060001e1.
        internal static void FindNearestKnotPosition(ISplineRuntimeHandle splineHandle, Vector3 localPosition, int knotIndex, out Vector3 knotPosition, out KnotT knotT)
        {
            splineHandle.FindNearestKnotPosition(localPosition, knotIndex, out knotPosition, out knotT);
        }

        // Original 0x060001e2.
        internal static void FindNearestLinePosition(Vector3 position, Vector3 origin, Vector3 end, out Vector3 linePosition)
        {
            if (origin == end)
            {
                linePosition = origin;
                return;
            }
            Vector3 delta = end - origin;
            float length = delta.magnitude;
            Vector3 direction = delta / length;
            float along = Mathf.Clamp(Vector3.Dot(position - origin, direction), 0f, length);
            linePosition = origin + direction * along;
        }

        // Original 0x060001e3.
        // The initial native branch also treats NaN as the start location; retain the negated greater comparison.
        internal static void GetSplineLocationFromDistance(ISplineRuntimeHandle splineHandle, float distance, out LinearRatio knotLinearRatio) { int count = splineHandle.KnotCount; if (!(distance > 0f) || count < 2) { knotLinearRatio = new LinearRatio(0, 0f); return; } float traversed = 0f; for (int i = 0; i < count - 1; i++) { ISplineKnotRuntimeHandle knot = splineHandle.GetKnotRuntimeHandle(i); if (traversed + knot.Length > distance) { knotLinearRatio = new LinearRatio(i, (distance - traversed) * knot.LengthInverse); return; } traversed += knot.Length; } knotLinearRatio = new LinearRatio(count - 2, 1f); }

        // Original 0x060001e4.
        public static Bounds RecalculateBounds(ISplineRuntimeHandle splineHandle)
        {
            if (splineHandle.KnotCount == 0) return new Bounds();
            Bounds bounds = new Bounds(splineHandle.GetKnotRuntimeHandle(0).Transform.Location, Vector3.zero);
            for (int i = 0; i < splineHandle.KnotCount; ++i)
            {
                int lastIndex = splineHandle.KnotCount - 1;
                bounds.Encapsulate(splineHandle.GetKnotRuntimeHandle(i).Transform.Location);
                if (i != lastIndex)
                {
                    Vector3 quarter = splineHandle.GetLocalPosAndTangentFromKnotT(new KnotT(i, .25f)).Position;
                    Vector3 half = splineHandle.GetLocalPosAndTangentFromKnotT(new KnotT(i, .5f)).Position;
                    Vector3 threeQuarter = splineHandle.GetLocalPosAndTangentFromKnotT(new KnotT(i, .75f)).Position;
                    bounds.Encapsulate(quarter);
                    bounds.Encapsulate(half);
                    bounds.Encapsulate(threeQuarter);
                }
            }
            bounds.Expand(.1f);
            return bounds;
        }

        // Original 0x060001e5.
        public static SurfaceLocation GetSurfaceLocation(SplineLocation splineLocation, Transform splineTransform)
        {
            LightweightTransform local = splineLocation.GetLocalTransform();
            Vector3 world = splineTransform.localToWorldMatrix.MultiplyPoint3x4(local.Location);
            PositionBoundsInfo info = CalculatePositionBoundsInfo(world, splineLocation);
            SurfaceLocation result = new SurfaceLocation();
            result.m_surface = splineLocation.m_spline;
            result.m_worldPosition = world;
            result.m_worldRotation = splineTransform.rotation * local.Orientation;
            result.m_localPosition = new Vector3(0f, 0f, splineLocation.SplineDistance);
            result.m_positionBoundsInfo = info;
            result.m_metadata = splineLocation.m_metadata;
            return result;
        }

        // Original 0x060001e6.
        private static PositionBoundsInfo CalculatePositionBoundsInfo(Vector3 worldPosition, SplineLocation splineLocation) { bool end = splineLocation.SplineDistance < EndEdgeDistanceTolerance || splineLocation.SplineDistance > splineLocation.m_spline.Length - EndEdgeDistanceTolerance; return new PositionBoundsInfo(0f, 0f, end, worldPosition, worldPosition); }

    }
}
