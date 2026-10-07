using UnityEngine;

namespace Hardlight
{
    internal static class SplineKnotRuntimeComponent
    {
        // Original HLSplines.Runtime 060001c6. Capture TLUT once; a missing array
        // returns zero. The upper bound is tested before the negative-index branch.
        internal static float GetLUTValue(ISplineKnotRuntimeHandle knotHandle, int index)
        {
            float[] lut = knotHandle.TLUT;
            if (lut == null) return 0f;
            if (lut.Length <= index) return 1f;
            if (index < 0) return 0f;
            return lut[index];
        }

        // Original 060001c7 copies one actual knot transform before invoking the
        // original Unity matrix API; no scale from an owning Transform is applied.
        internal static Vector3 TransformPositionLocalToWorld(ISplineKnotRuntimeHandle knotHandle, Vector3 position)
        {
            LightweightTransform transform = knotHandle.Transform;
            Vector3 origin = transform.Location;
            Quaternion rotation = transform.Orientation;
            Matrix4x4 matrix = Matrix4x4.TRS(origin, rotation, Vector3.one);
            return matrix.MultiplyPoint3x4(position);
        }

        // Original 060001c8 builds that same unit-scale matrix, then takes its
        // genuine Unity inverse before multiplying the requested world position.
        internal static Vector3 TransformPositionWorldToLocal(ISplineKnotRuntimeHandle knotHandle, Vector3 position)
        {
            LightweightTransform transform = knotHandle.Transform;
            Vector3 origin = transform.Location;
            Quaternion rotation = transform.Orientation;
            Matrix4x4 matrix = Matrix4x4.TRS(origin, rotation, Vector3.one);
            Matrix4x4 inverse = matrix.inverse;
            return inverse.MultiplyPoint3x4(position);
        }
    }
}
