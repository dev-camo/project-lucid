using UnityEngine;

namespace Hardlight
{
    internal interface ISegmentedSplineKnotRuntimeHandle
    {
        LightweightTransform Transform { get; }
        float Length { get; }
        float LengthInverse { get; }
        SegmentSplinePoint[] SegmentSplinePoints { get; }
    }
}
