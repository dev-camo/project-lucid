using UnityEngine;

namespace Hardlight
{
    public interface ISplineKnot
    {
        LightweightTransform Transform { get; }
        MetadataGroups Metadata { get; }
        Vector3 LocalControlPointNext { get; }
        Vector3 WorldControlPointNext { get; }
        Vector3 LocalControlPointPrev { get; }
        Vector3 WorldControlPointPrev { get; }
        Vector3 TangentNext { get; }
        Vector3 TangentPrev { get; }
        float Length { get; }
        float LengthInverse { get; }
    }
}
