using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    public interface ISplineRuntimeHandle
    {
        SplineType SplineType { get; }
        int KnotCount { get; }
        float Length { get; }
        float LengthInverse { get; }
        int LUTCount { get; }
        MetadataGroups Metadata { get; }
        List<KnotBounds> KnotBoundsList { get; }
        SplineControlPoints GetControlsForKnots(int knot0Index, int knot1Index);
        LinearRatio GetLinearRatioFromKnotT(KnotT knotT);
        KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio);
        void FindNearestKnotPosition(Vector3 localPosition, int knotIndex,
            out Vector3 knotPosition, out KnotT knotT);
        PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT);
        Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio);
        void RecalculateLength(bool force = false);
        ISplineKnotRuntimeHandle GetKnotRuntimeHandle(int knotIndex);
        int GetKnotRuntimeHandleIndex(ISplineKnotRuntimeHandle knotHandle);

        public struct KnotBounds
        {
            public int KnotIndex;
            public float SqrDistanceToBounds;
            public Bounds Bounds;
        }
    }
}
