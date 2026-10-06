using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class BoundsUtilities
    {
        // HLSplines.Runtime 0x06000343; original ARM64 0x1a81638.
        public static Bounds LocalToWorldAlloc(this Bounds bounds, Transform transform)
        {
            var corners = new Vector3[8];
            bounds.UpdateBoundsCorners(corners);
            return GeometryUtility.CalculateBounds(corners, transform.localToWorldMatrix);
        }

        // HLSplines.Runtime 0x06000344; original ARM64 0x1a81804.
        public static Bounds WorldToLocalAlloc(this Bounds bounds, Transform transform)
        {
            var corners = new Vector3[8];
            bounds.UpdateBoundsCorners(corners);
            return GeometryUtility.CalculateBounds(corners, transform.worldToLocalMatrix);
        }

        // HLSplines.Runtime 0x06000345; original ARM64 0x1a81744.
        public static Bounds LocalToWorld(this Bounds bounds, Transform transform, Vector3[] corners)
        {
            bounds.UpdateBoundsCorners(corners);
            return GeometryUtility.CalculateBounds(corners, transform.localToWorldMatrix);
        }

        // HLSplines.Runtime 0x06000346; original ARM64 0x1a81910.
        public static Bounds WorldToLocal(this Bounds bounds, Transform transform, Vector3[] corners)
        {
            bounds.UpdateBoundsCorners(corners);
            return GeometryUtility.CalculateBounds(corners, transform.worldToLocalMatrix);
        }

        // HLSplines.Runtime 0x06000347; original ARM64 0x1a819d0.
        public static void UpdateBoundsCorners(this Bounds bounds, Vector3[] corners)
        {
            Vector3 max = bounds.max;
            Vector3 min = bounds.min;
            corners[0] = new Vector3(max.x, max.y, max.z);
            corners[1] = new Vector3(max.x, max.y, min.z);
            corners[2] = new Vector3(max.x, min.y, max.z);
            corners[3] = new Vector3(max.x, min.y, min.z);
            corners[4] = new Vector3(min.x, max.y, max.z);
            corners[5] = new Vector3(min.x, max.y, min.z);
            corners[6] = new Vector3(min.x, min.y, max.z);
            corners[7] = new Vector3(min.x, min.y, min.z);
        }
    }
}
