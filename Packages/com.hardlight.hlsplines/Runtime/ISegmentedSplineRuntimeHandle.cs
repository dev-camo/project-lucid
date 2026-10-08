using UnityEngine;

namespace Hardlight
{
    internal interface ISegmentedSplineRuntimeHandle
    {
        int KnotCount { get; }
        SplineType SplineType { get; }
        Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio);
        ISegmentedSplineKnotRuntimeHandle GetKnotRuntimeHandle(int knotIndex);
        int GetKnotRuntimeHandleIndex(ISegmentedSplineKnotRuntimeHandle knotHandle);
    }
}
