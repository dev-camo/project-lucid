using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class RibbonUtilities
    {
        // Original HLSplines.Runtime 0x0600035a: output distance before the single count query;
        // all six vertices precede all four ordered triangle calls, without hit short circuit.
        public static bool TryGetRaycastHit(IRibbon ribbon, Ray ray, float distance, Transform transform, out RaycastHit raycastHit)
        {
            raycastHit = new RaycastHit { distance = distance + 1f };
            int count = ribbon.KnotCount;
            bool hasHit = false;
            for (int i = 1; i < count; ++i)
            {
                Vector3 left0 = GetSubKnotWorldPosition(RibbonAlignment.Left, ribbon, i - 1, transform);
                Vector3 center0 = GetSubKnotWorldPosition(RibbonAlignment.Center, ribbon, i - 1, transform);
                Vector3 right0 = GetSubKnotWorldPosition(RibbonAlignment.Right, ribbon, i - 1, transform);
                Vector3 left1 = GetSubKnotWorldPosition(RibbonAlignment.Left, ribbon, i, transform);
                Vector3 center1 = GetSubKnotWorldPosition(RibbonAlignment.Center, ribbon, i, transform);
                Vector3 right1 = GetSubKnotWorldPosition(RibbonAlignment.Right, ribbon, i, transform);
                hasHit |= SurfacePhysics.TryGetCloserHitFromTriangle(ray, left1, center0, left0, false, distance, ref raycastHit)
                    | SurfacePhysics.TryGetCloserHitFromTriangle(ray, left1, center1, center0, false, distance, ref raycastHit)
                    | SurfacePhysics.TryGetCloserHitFromTriangle(ray, center0, center1, right0, false, distance, ref raycastHit)
                    | SurfacePhysics.TryGetCloserHitFromTriangle(ray, center1, right1, right0, false, distance, ref raycastHit);
            }
            return hasHit;
        }
        // Original 0x0600035b: real knot call before matrix getter, then knot.Transform after the matrix.
        public static Vector3 GetSubKnotWorldPosition(RibbonAlignment ribbonAlignment, IRibbon ribbon, int knotIndex, Transform transform)
        {
            IRibbonKnot knot = ribbon.GetKnot(knotIndex, ribbonAlignment);
            return transform.localToWorldMatrix.MultiplyPoint3x4(knot.Transform.Location);
        }
    }
}
