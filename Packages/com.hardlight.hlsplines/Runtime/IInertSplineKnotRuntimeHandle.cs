using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime 02000095/060003db: the whole abstract contract.
    public interface IInertSplineKnotRuntimeHandle : ISplineKnotRuntimeHandle, ISplineKnotHandle
    {
        void SetTransform(Vector3 location, Quaternion orientation);
    }
}
