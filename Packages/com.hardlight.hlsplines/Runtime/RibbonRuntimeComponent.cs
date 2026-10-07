using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    internal static class RibbonRuntimeComponent
    {
        private const float EndEdgeTolerance = .001f;

        // Original HLSplines.Runtime 0x060001ae: Project the position into the ribbon frame before resolving its spline ratio and metadata.
        public static RibbonLocation FindAdjacentRibbonLocation(IRibbon ribbon, IRibbonRuntimeHandle ribbonHandle, ISplineRuntimeHandle subSpline, Transform transform, Vector3 worldPosition, Vector3 forward, RibbonAlignment alignment)
        {
            Vector3 localPosition = transform.worldToLocalMatrix.MultiplyPoint3x4(worldPosition);
            Vector3 localForward = transform.rotation * forward;
            FindAdjacentRibbonPosition(ribbonHandle, localPosition, localForward, out Vector3 ribbonPosition, out int knotIndex);
            FindAdjacentResult adjacent = SplineRuntimeComponent.FindAdjacentKnotPosition(subSpline, localPosition, localForward, knotIndex);
            LinearRatio ratio = ribbonHandle.GetLinearRatioFromKnotT(adjacent.KnotT, alignment);
            SurfaceKnotMetadata metadata = ribbon.GetKnotMetadataFromLinearRatio(ratio);
            RibbonLocation result = new RibbonLocation { m_ribbon = ribbon, m_alignment = alignment, m_knotLinearRatio = ratio, m_metadata = metadata };
            LightweightTransform frame = result.GetLocalTransform(true);
            result.m_localOffset = Quaternion.Inverse(frame.Orientation) * (ribbonPosition - frame.Location);
            return result;
        }

        // Original HLSplines.Runtime 0x060001af: Retain nearest-block bounds and measure the offset in the selected ribbon frame.
        public static RibbonLocation FindNearestRibbonLocation(IRibbon ribbon, IRibbonRuntimeHandle ribbonHandle, ISplineRuntimeHandle subSpline, List<SortKnots> sortedKnots, Vector3 localPosition, RibbonAlignment alignment)
        {
            FindNearestRibbonPosition(ribbonHandle, localPosition, sortedKnots, out Vector3 ribbonPosition, out int knotIndex, out PositionBoundsInfo boundsInfo);
            subSpline.FindNearestKnotPosition(localPosition, knotIndex, out Vector3 knotPosition, out KnotT knotT);
            LinearRatio ratio = ribbon.GetLinearRatioFromKnotT(knotT, alignment);
            SurfaceKnotMetadata metadata = ribbon.GetKnotMetadataFromLinearRatio(ratio);
            RibbonLocation result = new RibbonLocation { m_ribbon = ribbon, m_alignment = alignment, m_positionBoundsInfo = boundsInfo, m_knotLinearRatio = ratio, m_metadata = metadata };
            LightweightTransform frame = result.GetLocalTransform(true);
            result.m_localOffset = Quaternion.Inverse(frame.Orientation) * (localPosition - frame.Location);
            return result;
        }

        // Original HLSplines.Runtime 0x060001b0: Convert distance through the supplied spline, then attach the corresponding ribbon metadata.
        public static RibbonLocation GetRibbonLocationFromDistance(IRibbon ribbon, ISplineRuntimeHandle subSpline, float distance, RibbonAlignment alignment) { LinearRatio ratio; SplineRuntimeComponent.GetSplineLocationFromDistance(subSpline, distance, out ratio); SurfaceKnotMetadata metadata = ribbon.GetKnotMetadataFromLinearRatio(ratio); return new RibbonLocation { m_ribbon = ribbon, m_alignment = alignment, m_knotLinearRatio = ratio, m_metadata = metadata }; }

        // Original HLSplines.Runtime 0x060001b1: Project ribbon-local coordinates through its center frame before resolving the surface location.
        public static SurfaceLocation FindNearestSurfaceLocationFromLocal(IRibbon ribbon, Transform transform, Vector3 localPosition) { RibbonLocation distanceLocation = ribbon.GetRibbonLocationFromDistance(localPosition.z, RibbonAlignment.Center); LightweightTransform local = distanceLocation.GetLocalTransform(); float distance = distanceLocation.RibbonDistance; Vector3 adjusted = local.Location + local.Orientation * new Vector3(localPosition.x, localPosition.y, 0f); Vector3 world = transform.localToWorldMatrix.MultiplyPoint3x4(adjusted); RibbonLocation nearest = ribbon.FindNearestRibbonLocation(world, RibbonAlignment.Center); SurfaceLocation result = new SurfaceLocation(); result.m_surface = ribbon; result.m_worldPosition = world; result.m_worldRotation = transform.rotation * local.Orientation; result.m_localPosition = new Vector3(nearest.m_localOffset.x, 0f, distance); result.m_positionBoundsInfo = nearest.m_positionBoundsInfo; result.m_metadata = nearest.m_metadata; return result; }

        // Original HLSplines.Runtime 0x060001b2: Keep the nearest frame, world orientation and accumulated ribbon distance.
        public static SurfaceLocation FindNearestSurfaceLocationFromWorld(IRibbon ribbon, Transform transform, Vector3 worldPosition) { RibbonLocation nearest = ribbon.FindNearestRibbonLocation(worldPosition, RibbonAlignment.Center); LightweightTransform local = nearest.GetLocalTransform(); Vector3 world = transform.localToWorldMatrix.MultiplyPoint3x4(local.Location); SurfaceLocation result = new SurfaceLocation(); result.m_surface = ribbon; result.m_worldPosition = world; result.m_worldRotation = transform.rotation * local.Orientation; result.m_localPosition = new Vector3(nearest.m_localOffset.x, 0f, nearest.m_localOffset.z + nearest.RibbonDistance); result.m_positionBoundsInfo = nearest.m_positionBoundsInfo; result.m_metadata = nearest.m_metadata; return result; }

        // Original HLSplines.Runtime 0x060001b3: Prefer forward-plane proximity; ties within .005 use squared distance.
        internal static bool FindAdjacentRibbonPosition(IRibbonRuntimeHandle ribbonHandle, Vector3 worldPosition, Vector3 forward, out Vector3 ribbonPosition, out int knotIndex)
        {
            int knotCount = ribbonHandle.KnotCount;
            Vector3 bestPosition = Vector3.zero;
            int bestIndex = -1;
            bool bestHit = true;
            float bestAlong = float.MaxValue;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < knotCount - 1; ++i)
            {
                bool hit = FindAdjacentBlockKnotPosition(ribbonHandle, worldPosition, forward, i, out Vector3 candidate);
                Vector3 difference = worldPosition - candidate;
                float along = Mathf.Abs(Vector3.Dot(forward, difference));
                float distance = difference.sqrMagnitude;
                bool better = MathUtilities.WithinTolerance(along, bestAlong, .005f) ? distance < bestDistance : along < bestAlong;
                if (better)
                {
                    bestHit = hit;
                    bestIndex = i;
                    bestPosition = candidate;
                    bestAlong = along;
                    bestDistance = distance;
                }
            }
            knotIndex = bestIndex;
            ribbonPosition = bestPosition;
            return bestHit;
        }

        // Original HLSplines.Runtime 0x060001b4: Resolve left then right; clamp missed intersections against the border before the final line projection.
        private static bool FindAdjacentBlockKnotPosition(IRibbonRuntimeHandle ribbonHandle, Vector3 position, Vector3 forward, int knotIndex, out Vector3 blockPosition)
        {
            ISplineRuntimeHandle leftSpline = ribbonHandle.GetSubSplineRuntimeHandle(RibbonAlignment.Left);
            FindAdjacentResult left = SplineRuntimeComponent.FindAdjacentKnotPosition(leftSpline, position, forward, knotIndex);
            Vector3 leftPosition = left.KnotPosition;
            bool leftHit = left.HitOnKnot;
            if (!leftHit)
            {
                AdjustAdjacentPositionWithBorders(ribbonHandle, position, forward, left.KnotPosition, left.KnotT, out leftPosition);
                leftHit = Mathf.Abs(Vector3.Dot(forward, leftPosition - position)) <= .05f;
            }
            ISplineRuntimeHandle rightSpline = ribbonHandle.GetSubSplineRuntimeHandle(RibbonAlignment.Right);
            FindAdjacentResult right = SplineRuntimeComponent.FindAdjacentKnotPosition(rightSpline, position, forward, knotIndex);
            Vector3 rightPosition = right.KnotPosition;
            bool rightHit = right.HitOnKnot;
            if (!rightHit)
            {
                AdjustAdjacentPositionWithBorders(ribbonHandle, position, forward, right.KnotPosition, right.KnotT, out rightPosition);
                rightHit = Mathf.Abs(Vector3.Dot(forward, rightPosition - position)) <= .05f;
            }
            SplineRuntimeComponent.FindNearestLinePosition(position, leftPosition, rightPosition, out blockPosition);
            return leftHit || rightHit;
        }

        // Original HLSplines.Runtime 0x060001b5: The default bounds include the origin before all six samples are encapsulated.
        private static Bounds GetKnotBounds(IRibbonRuntimeHandle ribbonHandle, int knotIndex)
        {
            ISplineRuntimeHandle left = ribbonHandle.GetSubSplineRuntimeHandle(RibbonAlignment.Left);
            ISplineRuntimeHandle right = ribbonHandle.GetSubSplineRuntimeHandle(RibbonAlignment.Right);
            Vector3 leftStart = left.GetKnotRuntimeHandle(knotIndex).Transform.Location;
            Vector3 leftMiddle = left.GetLocalPosFromLinearRatio(new LinearRatio(knotIndex, .5f));
            Vector3 leftEnd = left.GetKnotRuntimeHandle(knotIndex + 1).Transform.Location;
            Vector3 rightStart = right.GetKnotRuntimeHandle(knotIndex).Transform.Location;
            Vector3 rightMiddle = right.GetLocalPosFromLinearRatio(new LinearRatio(knotIndex, .5f));
            Vector3 rightEnd = right.GetKnotRuntimeHandle(knotIndex + 1).Transform.Location;
            Bounds result = default;
            result.Encapsulate(leftStart);
            result.Encapsulate(leftMiddle);
            result.Encapsulate(leftEnd);
            result.Encapsulate(rightStart);
            result.Encapsulate(rightMiddle);
            result.Encapsulate(rightEnd);
            result.Expand(.1f);
            return result;
        }

        // Original HLSplines.Runtime 0x060001b6: Visit pre-sorted blocks until their bounds exceed the current distance; retain the first strict minimum.
        internal static void FindNearestRibbonPosition(IRibbonRuntimeHandle ribbonHandle, Vector3 localPosition, List<SortKnots> sortedKnots, out Vector3 ribbonPosition, out int knotIndex, out PositionBoundsInfo positionBoundsInfo)
        {
            int knotCount = ribbonHandle.KnotCount;
            positionBoundsInfo = default;
            int bestIndex = -1;
            Vector3 bestPosition = Vector3.zero;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < knotCount - 1; ++i)
            {
                SortKnots sorted = sortedKnots[i];
                if (bestDistance < sorted.SqrDistance) break;
                Vector3 candidate;
                PositionBoundsInfo candidateBounds;
                if (ribbonHandle.GetSubSplineRuntimeHandle(RibbonAlignment.Center).SplineType == SplineType.CatmullRom)
                    FindNearestBlockKnotPositionFromCentre(ribbonHandle, localPosition, sorted.KnotIndex, out candidate, out candidateBounds);
                else
                    FindNearestBlockKnotPosition(ribbonHandle, localPosition, sorted.KnotIndex, out candidate, out candidateBounds);
                float distance = (localPosition - candidate).sqrMagnitude;
                if (distance < bestDistance)
                {
                    positionBoundsInfo = candidateBounds;
                    bestIndex = sorted.KnotIndex;
                    bestPosition = candidate;
                    bestDistance = distance;
                }
            }
            knotIndex = bestIndex;
            ribbonPosition = bestPosition;
        }

        // Original HLSplines.Runtime 0x060001b7: Sort only by squared distance using Single.CompareTo, including its unordered-value behavior.
        internal static void SortKnotBounds(IRibbonRuntimeHandle ribbonHandle, Vector3 localPosition, List<Bounds> knotBounds, List<SortKnots> sortKnots) { int count = ribbonHandle.KnotCount - 1; for (int i = 0; i < count; i++) { Bounds bounds = knotBounds[i]; sortKnots[i] = new SortKnots { KnotIndex = i, SqrDistance = bounds.SqrDistance(localPosition) }; } sortKnots.Sort((a, b) => a.SqrDistance.CompareTo(b.SqrDistance)); }

        // Original HLSplines.Runtime 0x060001b8: Refresh each stored block bound in original knot order.
        internal static void FillKnotBounds(IRibbonRuntimeHandle ribbonHandle, List<Bounds> knotBounds) { int count = ribbonHandle.KnotCount - 1; for (int i = 0; i < count; i++) knotBounds[i] = GetKnotBounds(ribbonHandle, i); }

        // Original HLSplines.Runtime 0x060001b9: Allocate actual list elements for knotCount-1 blocks before filling and sorting.
        internal static void FindNearestRibbonPosition(IRibbonRuntimeHandle ribbonHandle, Vector3 localPosition, out Vector3 ribbonPosition, out int knotIndex, out PositionBoundsInfo positionBoundsInfo)
        {
            int knotCount = ribbonHandle.KnotCount;
            List<Bounds> bounds = new List<Bounds>(new Bounds[knotCount - 1]);
            List<SortKnots> sorted = new List<SortKnots>(new SortKnots[knotCount - 1]);
            FillKnotBounds(ribbonHandle, bounds);
            SortKnotBounds(ribbonHandle, localPosition, bounds, sorted);
            FindNearestRibbonPosition(ribbonHandle, localPosition, sorted, out ribbonPosition, out knotIndex, out positionBoundsInfo);
        }

        // Original HLSplines.Runtime 0x060001ba: Preserve border projections and the original end-edge index comparison.
        private static void FindNearestBlockKnotPosition(IRibbonRuntimeHandle ribbonHandle, Vector3 position, int knotIndex, out Vector3 blockPosition, out PositionBoundsInfo positionBoundsInfo)
        {
            ISplineRuntimeHandle left = ribbonHandle.GetSubSplineRuntimeHandle(RibbonAlignment.Left);
            ISplineRuntimeHandle right = ribbonHandle.GetSubSplineRuntimeHandle(RibbonAlignment.Right);
            left.FindNearestKnotPosition(position, knotIndex, out Vector3 leftPosition, out KnotT leftT);
            right.FindNearestKnotPosition(position, knotIndex, out Vector3 rightPosition, out KnotT rightT);
            Vector3 leftAdjacent;
            if (!(leftT.T <= EndEdgeTolerance || leftT.T >= 1f - EndEdgeTolerance))
            {
                PositionAndTangent frame = ribbonHandle.GetLocalPosAndTangentFromKnotT(new KnotT(knotIndex, leftT.T), RibbonAlignment.Left);
                FindAdjacentResult adjacent = SplineRuntimeComponent.FindAdjacentKnotPosition(right, leftPosition, frame.Tangent, knotIndex);
                if (!adjacent.HitOnKnot)
                    AdjustAdjacentPositionWithBorders(ribbonHandle, leftPosition, frame.Tangent, adjacent.KnotPosition, adjacent.KnotT, out leftAdjacent);
                else leftAdjacent = adjacent.KnotPosition;
            }
            else leftAdjacent = leftPosition;
            Vector3 rightAdjacent;
            if (!(rightT.T <= EndEdgeTolerance || rightT.T >= 1f - EndEdgeTolerance))
            {
                PositionAndTangent frame = ribbonHandle.GetLocalPosAndTangentFromKnotT(new KnotT(knotIndex, rightT.T), RibbonAlignment.Right);
                FindAdjacentResult adjacent = SplineRuntimeComponent.FindAdjacentKnotPosition(left, rightPosition, frame.Tangent, knotIndex);
                if (!adjacent.HitOnKnot)
                    AdjustAdjacentPositionWithBorders(ribbonHandle, rightPosition, frame.Tangent, adjacent.KnotPosition, adjacent.KnotT, out rightAdjacent);
                else rightAdjacent = adjacent.KnotPosition;
            }
            else rightAdjacent = rightPosition;
            SplineRuntimeComponent.FindNearestLinePosition(position, leftPosition, leftAdjacent, out Vector3 leftClosest);
            SplineRuntimeComponent.FindNearestLinePosition(position, rightPosition, rightAdjacent, out Vector3 rightClosest);
            SplineRuntimeComponent.FindNearestLinePosition(position, leftClosest, rightClosest, out blockPosition);
            bool endEdge = knotIndex == 0
                ? rightT.T <= EndEdgeTolerance || leftT.T <= EndEdgeTolerance
                : ribbonHandle.KnotCount == knotIndex && (rightT.T >= 1f - EndEdgeTolerance || leftT.T >= 1f - EndEdgeTolerance);
            positionBoundsInfo = new PositionBoundsInfo((blockPosition - leftPosition).sqrMagnitude, (blockPosition - rightPosition).sqrMagnitude, endEdge, leftPosition, rightPosition);
        }

        // Original HLSplines.Runtime 0x060001bb: Interpolate between the center and nearest side using the original distance ratio.
        private static void FindNearestBlockKnotPositionFromCentre(IRibbonRuntimeHandle ribbonHandle, Vector3 position, int knotIndex, out Vector3 blockPosition, out PositionBoundsInfo positionBoundsInfo)
        {
            ISplineRuntimeHandle centre = ribbonHandle.GetSubSplineRuntimeHandle(RibbonAlignment.Center);
            ISplineRuntimeHandle left = ribbonHandle.GetSubSplineRuntimeHandle(RibbonAlignment.Left);
            ISplineRuntimeHandle right = ribbonHandle.GetSubSplineRuntimeHandle(RibbonAlignment.Right);
            centre.FindNearestKnotPosition(position, knotIndex, out Vector3 centrePosition, out KnotT centreT);
            left.FindNearestKnotPosition(position, knotIndex, out Vector3 leftPosition, out KnotT leftT);
            right.FindNearestKnotPosition(position, knotIndex, out Vector3 rightPosition, out KnotT rightT);
            Vector3 chosen = (leftPosition - position).sqrMagnitude <= (rightPosition - position).sqrMagnitude ? leftPosition : rightPosition;
            float amount = 1f - (chosen - position).magnitude / (centrePosition - chosen).magnitude;
            blockPosition = Vector3.Lerp(centrePosition, chosen, amount);
            bool endEdge = IsEndEdge(ribbonHandle, centreT);
            positionBoundsInfo = new PositionBoundsInfo((blockPosition - leftPosition).sqrMagnitude, (blockPosition - rightPosition).sqrMagnitude, endEdge, leftPosition, rightPosition);
        }

        // Original HLSplines.Runtime 0x060001bc: The first edge compares only T; other edges query the supplied knot count.
        private static bool IsEndEdge(IRibbonRuntimeHandle ribbonHandle, KnotT knotT) { if (knotT.KnotIndex == 0) return knotT.T <= EndEdgeTolerance; int count = ribbonHandle.KnotCount; return knotT.T >= 1f - EndEdgeTolerance && count - 2 == knotT.KnotIndex; }

        // Original HLSplines.Runtime 0x060001bd: Unordered T also advances the knot index; retain border selection and strict improvement.
        private static bool AdjustAdjacentPositionWithBorders(IRibbonRuntimeHandle ribbonHandle, Vector3 position, Vector3 forward, Vector3 adjacentPosition, KnotT adjacentKnot, out Vector3 adjustedAdjacentPosition)
        {
            int knotIndex = adjacentKnot.KnotIndex;
            if (!(adjacentKnot.T <= .5f)) ++knotIndex;
            Vector3 left = ribbonHandle.GetKnotRuntimeHandle(knotIndex, RibbonAlignment.Left).Transform.Location;
            Vector3 right = ribbonHandle.GetKnotRuntimeHandle(knotIndex, RibbonAlignment.Right).Transform.Location;
            Vector3 direction = forward;
            if (Vector3.Dot(direction, right - left) <= 0f) direction = -direction;
            SplineRuntimeComponent.FindAdjacentLinePosition(position, direction, left, right, out Vector3 candidate, out float lineT);
            bool better = Mathf.Abs(Vector3.Dot(forward, candidate - position)) < Mathf.Abs(Vector3.Dot(forward, adjacentPosition - position));
            adjustedAdjacentPosition = better ? candidate : adjacentPosition;
            return better;
        }

        // Original HLSplines.Runtime 0x060001be: Unordered T also advances the knot index; retain the nearest position unless distance strictly improves.
        private static bool AdjustNearestPositionWithBorders(IRibbonRuntimeHandle ribbonHandle, Vector3 position, Vector3 nearestPosition, int nearestKnot, float nearestT, out Vector3 adjustedNearestPosition)
        {
            if (!(nearestT <= .5f)) ++nearestKnot;
            Vector3 left = ribbonHandle.GetKnotRuntimeHandle(nearestKnot, RibbonAlignment.Left).Transform.Location;
            Vector3 right = ribbonHandle.GetKnotRuntimeHandle(nearestKnot, RibbonAlignment.Right).Transform.Location;
            SplineRuntimeComponent.FindNearestLinePosition(position, left, right, out Vector3 candidate);
            float oldDistance = (nearestPosition - position).sqrMagnitude;
            float newDistance = (candidate - position).sqrMagnitude;
            bool better = newDistance < oldDistance;
            adjustedNearestPosition = better ? candidate : nearestPosition;
            return better;
        }

        // Original HLSplines.Runtime 0x060001bf: Sample each segment at quarter intervals; the final knot contributes only its stored position.
        private static Bounds EncapsulatePositions(IRibbonRuntimeHandle ribbonHandle, int knotIndex, RibbonAlignment alignment, Bounds bounds, bool lastKnot)
        {
            Vector3 start = ribbonHandle.GetKnotRuntimeHandle(knotIndex, alignment).Transform.Location;
            bounds.Encapsulate(start);
            if (!lastKnot)
            {
                Vector3 quarter = ribbonHandle.GetLocalPosAndTangentFromKnotT(new KnotT(knotIndex, .25f), alignment).Position;
                Vector3 half = ribbonHandle.GetLocalPosAndTangentFromKnotT(new KnotT(knotIndex, .5f), alignment).Position;
                Vector3 threeQuarter = ribbonHandle.GetLocalPosAndTangentFromKnotT(new KnotT(knotIndex, .75f), alignment).Position;
                bounds.Encapsulate(quarter);
                bounds.Encapsulate(half);
                bounds.Encapsulate(threeQuarter);
            }
            return bounds;
        }

        // Original HLSplines.Runtime 0x060001c0: Visit left, center and right for every knot, then expand the complete bounds by .1.
        public static Bounds RecalculateBounds(IRibbonRuntimeHandle ribbonHandle)
        {
            if (ribbonHandle.KnotCount == 0) return default;
            Vector3 first = ribbonHandle.GetKnotRuntimeHandle(0, RibbonAlignment.Center).Transform.Location;
            Bounds bounds = new Bounds(first, Vector3.zero);
            for (int i = 0; i < ribbonHandle.KnotCount; ++i)
            {
                bool last = i == ribbonHandle.KnotCount - 1;
                bounds = EncapsulatePositions(ribbonHandle, i, RibbonAlignment.Left, bounds, last);
                bounds = EncapsulatePositions(ribbonHandle, i, RibbonAlignment.Center, bounds, last);
                bounds = EncapsulatePositions(ribbonHandle, i, RibbonAlignment.Right, bounds, last);
            }
            bounds.Expand(.1f);
            return bounds;
        }

        // Original HLSplines.Runtime 0x060001c1: Keep the original editor-only diagnostic switch and draw call.
        [System.Diagnostics.Conditional("UNITY_EDITOR")] private static void FindAdjacentBlockKnotPosition_DebugDraw(Vector3 startPosition, Vector3 leftPosition, Vector3 rightPosition, Vector3 blockPosition) { if (!RibbonEditorDefinition.Default.DisplayAdjacentBlockKnotPositionCalculation) return; Debug.DrawLine(startPosition, leftPosition, Color.blue); }

        // Original HLSplines.Runtime 0x060001c2: Keep the original editor-only diagnostic switch and line order.
        [System.Diagnostics.Conditional("UNITY_EDITOR")] private static void FindNearestBlockKnotPosition_DebugDraw(Vector3 startPosition, Vector3 leftPosition, Vector3 rightPosition, Vector3 leftAdjacentPosition, Vector3 rightAdjacentPosition, Vector3 leftClosestPosition, Vector3 rightClosestPosition, Vector3 blockPosition) { if (!RibbonEditorDefinition.Default.DisplayNearestBlockKnotPositionCalculation) return; Debug.DrawLine(startPosition, leftPosition, Color.blue); Debug.DrawLine(leftPosition, leftAdjacentPosition, Color.blue); Debug.DrawLine(startPosition, rightPosition, Color.red); Debug.DrawLine(rightPosition, rightAdjacentPosition, Color.red); Debug.DrawLine(leftClosestPosition, rightClosestPosition, Color.gray); Debug.DrawLine(startPosition, blockPosition); }
    }
}
