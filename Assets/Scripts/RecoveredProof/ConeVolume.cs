using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Hardlight;

namespace HardlightProject
{
    // Original Game020009f6: four static declarations and original nested data.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class ConeVolume
    {
        public struct ConeDescription
        {
            public Vector3 Origin;
            public Quaternion Rotation;
            public float Distance;
            public float Step;
            public Vector2 Tangents;
        }
        private const int ConeSegmentCount = 16;
        private static Vector2[] s_coneSegmentAngles;
        public static Vector2[] ConeSegmentAngles
        {
            get { CalculateCachedValues(); return s_coneSegmentAngles; }
        }

        //06003944: the elliptical test uses the supplied distance to scale both
        //tangents. It does not test the transformed point's depth or axial bounds.
        public static bool IsPositionInsideCone(Vector3 position, Vector3 coneOrigin,
            Vector2 coneTangents, float coneDistance, Quaternion worldToLocal)
        {
            Vector3 local = worldToLocal * (position - coneOrigin);
            float radiusX = coneTangents.x * coneDistance;
            float radiusY = coneTangents.y * coneDistance;
            return local.x * local.x / (radiusX * radiusX) +
                local.y * local.y / (radiusY * radiusY) <= 1f;
        }

        public static Bounds CalculateConeBounds(ConeDescription cone)
        {
            CalculateCachedValues();
            Vector3 displacement = (cone.Rotation * Vector3.forward) * cone.Distance;
            Bounds bounds = new Bounds(cone.Origin + displacement * 0.5f, displacement.Abs());
            Vector3 end = cone.Origin + displacement;
            float radiusX = cone.Distance * cone.Tangents.x;
            float radiusY = cone.Distance * cone.Tangents.y;
            for (int i = 0; i < ConeSegmentCount; ++i)
            {
                Vector2 angle = s_coneSegmentAngles[i];
                bounds.Encapsulate(end + cone.Rotation * new Vector3(radiusX * angle.x, radiusY * angle.y, 0f));
            }
            return bounds;
        }

        //06003946: literal IEEE754 table bytes are identical in both original
        //architectures. A correctly sized existing array is retained as-is.
        //The replacement array is published before its ordered element writes.
        private static void CalculateCachedValues()
        {
            if (s_coneSegmentAngles != null && s_coneSegmentAngles.Length == ConeSegmentCount) return;
            s_coneSegmentAngles = new Vector2[ConeSegmentCount];
            s_coneSegmentAngles[0] = new Vector2(1f, 0f);
            s_coneSegmentAngles[1] = new Vector2(0.9238795042037964f, 0.3826834559440613f);
            s_coneSegmentAngles[2] = new Vector2(0.7071067690849304f, 0.7071067690849304f);
            s_coneSegmentAngles[3] = new Vector2(0.3826834261417389f, 0.9238795042037964f);
            s_coneSegmentAngles[4] = new Vector2(-4.371138828673793e-08f, 1f);
            s_coneSegmentAngles[5] = new Vector2(-0.38268351554870605f, 0.9238795042037964f);
            s_coneSegmentAngles[6] = new Vector2(-0.7071067690849304f, 0.7071067690849304f);
            s_coneSegmentAngles[7] = new Vector2(-0.9238796234130859f, 0.38268327713012695f);
            s_coneSegmentAngles[8] = new Vector2(-1f, -8.742277657347586e-08f);
            s_coneSegmentAngles[9] = new Vector2(-0.9238795042037964f, -0.3826834261417389f);
            s_coneSegmentAngles[10] = new Vector2(-0.7071066498756409f, -0.70710688829422f);
            s_coneSegmentAngles[11] = new Vector2(-0.382683128118515f, -0.9238796830177307f);
            s_coneSegmentAngles[12] = new Vector2(1.1924880638503055e-08f, -1f);
            s_coneSegmentAngles[13] = new Vector2(0.3826836049556732f, -0.9238794445991516f);
            s_coneSegmentAngles[14] = new Vector2(0.7071070075035095f, -0.7071065306663513f);
            s_coneSegmentAngles[15] = new Vector2(0.9238795638084412f, -0.3826834261417389f);

        }
    }
}
