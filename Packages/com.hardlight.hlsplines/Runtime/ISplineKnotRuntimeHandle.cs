using UnityEngine;

namespace Hardlight
{
    public interface ISplineKnotRuntimeHandle : ISplineKnotHandle
    {
        LightweightTransform Transform { get; }
        float Length { get; }
        float LengthInverse { get; }
        MetadataGroups Metadata { get; }
        Vector3 TangentNext { get; }
        Vector3 TangentPrev { get; }
        void SetLengthDirty();
        bool IsLengthDirty();
        void RecalculateLength();
    }
}
